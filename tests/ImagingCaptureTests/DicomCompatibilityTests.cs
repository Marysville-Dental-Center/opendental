// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ImagingCapture.Dicom;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>The one compatibility test of design 7.8: while fo-dicom still ships, a file from DicomIntraOralWriter opens through Open
	///Dental's own DICOM loading (DicomHelper, which ImageStore.OpenBitmapDicom calls; read-only use, no database) with identical pixels.
	///It also checks every tag and VR the writer emits against fo-dicom's copy of the PS3.6 dictionary. Delete this file when fo-dicom
	///goes; the writer's own tests do not depend on it.</summary>
	[TestClass]
	public class DicomCompatibilityTests {
		[TestMethod]
		public void WrittenFile_OpensThroughOpenDentalsDicomLoader() {
			string folder=Path.Combine(Path.GetTempPath(),"ImagingCaptureTests_dicom_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				foreach(int bitsStoredExpected in new[] { 16,12 }) {
					int width=37;//odd, so rows are not word-aligned by accident
					int height=23;
					ushort[] arrayPixels=new ushort[width*height];
					int maxValue=bitsStoredExpected==12 ? 4095 : 65535;
					for(int i=0;i<arrayPixels.Length;i++) {
						arrayPixels[i]=(ushort)((i*7919L)%(maxValue+1));
					}
					DicomIntraOralImage image=new DicomIntraOralImage {
						Width=width,
						Height=height,
						Pixels=arrayPixels,
						XDpi=1270,
						YDpi=1270,
						PixelFlavor=AcquiredImage.PixelFlavorChocolate,
						Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="2,3,30,31",DegreesRotated=0 },
						Metadata=ImagingCapture.Metadata.CaptureMetadataParser.Parse(@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example"","
							+@"""serial"":""SN-1"",""acquiredAt"":""2026-10-31T14:25:30.123+01:00"",""effectiveBits"":"
							+(bitsStoredExpected==12 ? "12" : "null")+@",""redelivered"":false}"),
						StudyDateTime=new DateTime(2026,10,31),
						InstanceNumber=1,
					};
					DicomWriteResult result=DicomIntraOralWriter.Write(image);
					Assert.IsTrue(result.IsWritten,result.Error);
					Assert.AreEqual(bitsStoredExpected,result.BitsStored);
					string path=Path.Combine(folder,"image"+bitsStoredExpected+".dcm");
					File.WriteAllBytes(path,result.Bytes);
					BitmapDicom bitmapDicomStream;
					try {
						using(MemoryStream memoryStream=new MemoryStream(result.Bytes)) {
							bitmapDicomStream=DicomHelper.GetFromStream(memoryStream);//does not swallow exceptions, unlike GetFromFile
						}
					}
					catch(Exception ex) when(ex is FileNotFoundException || ex is FileLoadException || ex is TypeInitializationException
						|| ex is BadImageFormatException)
					{
						Assert.Inconclusive("Open Dental's DICOM loading could not be loaded here: "+ex.Message);
						return;
					}
					BitmapDicom bitmapDicom=DicomHelper.GetFromFile(path);
					foreach(BitmapDicom bitmapDicomRead in new[] { bitmapDicomStream,bitmapDicom }) {
						Assert.IsNotNull(bitmapDicomRead,"The loader refused the file (BitsStored "+bitsStoredExpected+").");
						Assert.AreEqual(width,bitmapDicomRead.Width);
						Assert.AreEqual(height,bitmapDicomRead.Height);
						Assert.AreEqual(16,bitmapDicomRead.BitsAllocated);
						Assert.AreEqual(bitsStoredExpected,bitmapDicomRead.BitsStored);
						Assert.AreEqual(EnumDicomPhotometricInterp.MONOCHROME2,bitmapDicomRead.PhotometricInterp);
						CollectionAssert.AreEqual(arrayPixels,bitmapDicomRead.ArrayDataRaw,"Identical pixels (BitsStored "+bitsStoredExpected+").");
					}
					CheckTagsAgainstDictionary(result.Bytes);
				}
			}
			finally {
				try {
					Directory.Delete(folder,true);
				}
				catch(Exception) {
				}
			}
		}

		///<summary>Every element of the file, and of its sequence items, has a VR that fo-dicom's PS3.6 dictionary allows for its tag (an
		///unknown or mistyped tag gets UN there). Through reflection, so that the test project needs no reference to fo-dicom.</summary>
		private static void CheckTagsAgainstDictionary(byte[] bytes) {
			Assembly assemblyDicom=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name=="Dicom.Core");
			Assert.IsNotNull(assemblyDicom,"fo-dicom was loaded by the reads above.");
			Type typeDictionary=assemblyDicom.GetType("Dicom.DicomDictionary");
			Type typeTag=assemblyDicom.GetType("Dicom.DicomTag");
			object dictionary=typeDictionary?.GetProperty("Default",BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
			ConstructorInfo constructorTag=typeTag?.GetConstructor(new[] { typeof(ushort),typeof(ushort) });
			PropertyInfo propertyIndexer=typeDictionary?.GetProperty("Item",new[] { typeTag });
			if(dictionary==null || constructorTag==null || propertyIndexer==null) {
				Assert.Inconclusive("fo-dicom's dictionary API was not found; the loader checks above passed.");
			}
			TestDicomFile testDicomFile=TestDicomReader.Read(bytes);
			List<TestDicomElement> listElements=testDicomFile.ListMeta.Concat(testDicomFile.ListDataset).ToList();
			listElements.AddRange(testDicomFile.ListDataset.Where(x => x.ListItems!=null).SelectMany(x => x.ListItems).SelectMany(x => x).ToList());
			List<string> listMismatches=new List<string>();
			foreach(TestDicomElement element in listElements) {
				object tag=constructorTag.Invoke(new object[] { (ushort)(element.Tag>>16),(ushort)(element.Tag & 0xFFFF) });
				object entry=propertyIndexer.GetValue(dictionary,new[] { tag });
				Array arrayVrs=entry?.GetType().GetProperty("ValueRepresentations")?.GetValue(entry) as Array;
				Assert.IsNotNull(arrayVrs,"No dictionary entry for "+element);
				List<string> listCodes=arrayVrs.Cast<object>().Select(x => x.GetType().GetProperty("Code")?.GetValue(x) as string).ToList();
				if(!listCodes.Contains(element.Vr)) {
					listMismatches.Add(element+" (dictionary: "+string.Join("/",listCodes)+")");
				}
			}
			Assert.AreEqual(0,listMismatches.Count,string.Join("; ",listMismatches));
		}
	}
}
