using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dicom;
using Dicom.Imaging;
using Dicom.Imaging.Codec;
using Dicom.Imaging.Render;

namespace OpenPmsDentalBusiness {
	///<summary></summary>
	public class BitmapDicom {
		///<summary>This is the raw bitmap in 16 bit, prior to windowing.  Displays only support 8 bits, so conversion must still be performed. Does not get disposed because it's not actually a bitmap.</summary>
		public ushort[] ArrayDataRaw;
		public int Width;
		public int Height;
		public ushort BitsAllocated;
		public ushort BitsStored;
		///<summary>Usually empty. Only used to temporarily store on import.</summary>
		public int WindowingMin;
		///<summary>Usually empty. Only used to temporarily store on import.</summary>
		public int WindowingMax;
		public EnumDicomPhotometricInterp PhotometricInterp;
	}

	public class DicomHelper{
		///<summary>The in-house reader of uncompressed Explicit VR Little Endian files (design 7.8, CAP-DR): the subset the capture writer emits.</summary>
		public static readonly IDicomPixelReader SubsetReader=new DicomSubsetReader();
		///<summary>The in-house reader of every other DICOM file (docs/imaging/dicom-reader.md): every encoding, the photometric interpretations
		///the program shows, RLE Lossless and JPEG Baseline, and a clear reason for what it declines.</summary>
		public static readonly DicomImageReader FullReader=new DicomImageReader();
		///<summary>fo-dicom (MS-PL), the reader Open Dental used. No longer used by the program; kept only until the tests have compared the
		///in-house reader with it.</summary>
		public static readonly IDicomPixelReader FoDicomReader=new FoDicomPixelReader();
		///<summary>What GetFromFile, GetFromStream and GetFromBase64 read with: the subset reader first, for the files the capture writes, then
		///the full reader for every file the subset reader declines.</summary>
		public static readonly IDicomPixelReader PixelReader=new DicomPixelReaderChain(SubsetReader,FullReader);

		///<summary>Reads the file as GetFromFile does (the subset reader, then the full reader), and says why when there is no image. Never
		///throws.</summary>
		public static DicomImageReadResult ReadFileResult(string path){
			if(!File.Exists(path)){
				return new DicomImageReadResult(DicomImageReadStatus.FileError,"The file does not exist.",null,null);
			}
			DicomSubsetReadResult resultSubset=DicomSubsetReader.ReadFileResult(path);
			if(resultSubset.Status==DicomSubsetReadStatus.Read){
				return new DicomImageReadResult(DicomImageReadStatus.Read,"",resultSubset.BitmapDicom,DicomSubsetReader.TransferSyntaxExplicitVrLittleEndian);
			}
			return FullReader.ReadFileResult(path);
		}

		///<summary>Reads the stream as GetFromStream does (the subset reader, then the full reader from the same position), and says why when
		///there is no image. Never throws.</summary>
		public static DicomImageReadResult ReadStreamResult(Stream stream){
			if(stream==null || !stream.CanRead){
				return new DicomImageReadResult(DicomImageReadStatus.FileError,"No stream to read.",null,null);
			}
			try{
				Stream streamReplay=DicomPixelReaderChain.GetReplayableStream(stream,out bool isFirstReaderPossible);
				if(isFirstReaderPossible){
					long position=streamReplay.Position;
					DicomSubsetReadResult resultSubset=DicomSubsetReader.ReadStreamResult(streamReplay);
					if(resultSubset.Status==DicomSubsetReadStatus.Read){
						return new DicomImageReadResult(DicomImageReadStatus.Read,"",resultSubset.BitmapDicom,DicomSubsetReader.TransferSyntaxExplicitVrLittleEndian);
					}
					streamReplay.Position=position;
				}
				return FullReader.ReadStreamResult(streamReplay);
			}
			catch(Exception ex){
				return new DicomImageReadResult(DicomImageReadStatus.FileError,"The file could not be read: "+ex.Message,null,null);
			}
		}

		///<summary>What to tell the user when a DICOM file is not shown, from ReadFileResult or ReadStreamResult. "" when it was read. A compressed
		///format this version has no decoder for is named, with the note that this version cannot open it yet.</summary>
		public static string GetNotReadMessage(DicomImageReadResult result){
			if(result==null){
				return Lans.g("DicomHelper","The DICOM file could not be read.");
			}
			switch(result.Status){
				case DicomImageReadStatus.Read:
					return "";
				case DicomImageReadStatus.UnsupportedCompression:
					string format=result.TransferSyntaxName;
					if(format==""){
						format=Lans.g("DicomHelper","a format the file does not name");
					}
					else if(DicomTransferSyntaxes.Find(result.TransferSyntaxUid)==null){
						format=Lans.g("DicomHelper","an unknown format (transfer syntax")+" "+result.TransferSyntaxUid+")";
					}
					return Lans.g("DicomHelper","This DICOM image is stored as")+" "+format+". "
						+Lans.g("DicomHelper","This version of the program cannot open that format yet.");
				case DicomImageReadStatus.TooLarge:
					return Lans.g("DicomHelper","This DICOM image is too large to open.")+" "+result.Reason;
				case DicomImageReadStatus.NotDicom:
					return Lans.g("DicomHelper","This file is not a DICOM file the program can open.")+" "+result.Reason;
				case DicomImageReadStatus.Unsupported:
					return Lans.g("DicomHelper","This DICOM image cannot be shown by this version of the program.")+" "+result.Reason;
				case DicomImageReadStatus.Malformed:
					return Lans.g("DicomHelper","This DICOM file is damaged or not valid DICOM, so it cannot be opened.")+" "+result.Reason;
				default:
					return Lans.g("DicomHelper","The DICOM file could not be read.")+" "+result.Reason;
			}
		}

		///<summary>Windowing values (document.WindowingMin and WindowingMax) are always stored from 0 to 255, whatever the bit depth of the image.
		///Returns how many raw pixel values one windowing step covers: 1 for 8 bit data, 16 for 12 bit data (4095/255, as before), and 256 for true
		///16 bit data, which uses its 12 most significant bits the way Open Dental 25.4 documents for document.WindowingMax. Decided by BitsStored,
		///not BitsAllocated, because 12 bit images are stored in 16 bit words. Other depths in 16 bit words keep the 12 bit factor they always had,
		///so their stored windowing still shows the same.</summary>
		public static float GetRawPerWindowingStep(BitmapDicom bitmapDicom){
			if(bitmapDicom.BitsAllocated!=16) {
				return 1f;
			}
			if(bitmapDicom.BitsStored==16) {
				return 256f;
			}
			return 16f;
		}

		///<summary>Limits a windowing value to 0-255. The 8 bit routines of ImageHelper use it for ordinary images, where a larger value means
		///the top of the range, as the windowing slider shows it. DICOM windowing reads stored values with GetWindowingOnDisplayScale instead.</summary>
		public static int ClampWindowing(int windowingValue){
			if(windowingValue<0) {
				return 0;
			}
			if(windowingValue>255) {
				return 255;
			}
			return windowingValue;
		}

		///<summary>A stored document.WindowingMin or WindowingMax of a DICOM image on the 0-255 scale the windowing uses (design 7.8, CAP-W2).
		///Values from 0 to 255 are used as they are. A value above 255 is a legacy value on the 12 bit scale (0-4095): the import wrote 4095 for
		///true 16 bit images before 25.4, the 12 bit import still writes 4095 when it finds no edge (GetWindowingMax), and older versions or
		///other programs may have written other 12 bit values. It is divided by 16, so 4095 reads as 255, and a 12 bit window on a 16 bit image
		///shows what the old divide-by-16 showed. This is for display only; nothing is written back to the database. Negative values count as 0,
		///and the result is never above 255.</summary>
		public static int GetWindowingOnDisplayScale(int windowingValue){
			if(windowingValue>255) {
				windowingValue=windowingValue/16;
			}
			return ClampWindowing(windowingValue);
		}

		///<summary>Converts the 16 bit bitmap data to an 8 bit bitmap by using the supplied windowing range.  Windowing values are on the 0-255 scale; stored values above 255 are read as the 12 bit scale (GetWindowingOnDisplayScale).  Can return null.</summary>
		public static Bitmap ApplyWindowing(BitmapDicom bitmapDicom,int windowingMin,int windowingMax){
			Bitmap bitmap=new Bitmap(bitmapDicom.Width,bitmapDicom.Height);
			BitmapData bitmapData=bitmap.LockBits(new Rectangle(0,0,bitmapDicom.Width,bitmapDicom.Height),ImageLockMode.ReadWrite,bitmap.PixelFormat);
			//The windowing values stay on the 0-255 scale; the raw values are scaled down to it instead, which does mean slightly rough increments.
			//This is not an issue because no data degradation. 12 bit: 4096 values, divided by 16. True 16 bit: 65536 values, divided by 256.
			float rawPerWindowingStep=GetRawPerWindowingStep(bitmapDicom);
			windowingMin=GetWindowingOnDisplayScale(windowingMin);
			windowingMax=GetWindowingOnDisplayScale(windowingMax);
			if(windowingMax==0) {//0 means no windowing was ever set (document.WindowingMax default), so show the full range, as the slider does.
				windowingMin=0;
				windowingMax=255;
			}
			unsafe {
				//rawData count should be same as pixels.  4 bytes per pixel
				byte* pBytes=(byte*)bitmapData.Scan0.ToPointer();
				for(int i=0;i<bitmapDicom.ArrayDataRaw.Length;i++) {
					byte byteVal=0;
					float floatDataRaw=bitmapDicom.ArrayDataRaw[i];
					if(bitmapDicom.BitsAllocated==16) {//12 or 16 bits stored
						floatDataRaw=floatDataRaw/rawPerWindowingStep;
					}
					if(floatDataRaw<windowingMin) {
						byteVal=0;//black
					}
					else if(floatDataRaw>windowingMax) {
						byteVal=255;//white
					}
					else {
						if(bitmapDicom.BitsAllocated==8) {
							byteVal=(byte)floatDataRaw;
						}
						else if(windowingMax==windowingMin) {//a window of one value: 0/0 below would be NaN, which the checked cast refuses
							byteVal=255;
						}
						else {//16 bit
							byteVal=(byte)Math.Round(255f * (floatDataRaw - windowingMin) / (windowingMax - windowingMin));
							//Examples using bytes/255: if window is .3 to .5 
							//.301->.005, .4->.5, .499->.995,
						}
					}
					//ARGB is stored as BGRA on little-endian machine. Alpha most significant, blue least.  Pointer to integer is stored the same.
					pBytes[i*4]=byteVal;//B
					pBytes[i*4+1]=byteVal;//G
					pBytes[i*4+2]=byteVal;//R
					pBytes[i*4+3]=255;//A
				}
			}
			//Marshal.Copy(byteArray, 0, bitmapData.Scan0, byteArray.Length);// Bulk copy pixel data from a byte array
			//Marshal.WriteInt16(bitmapData.Scan0, offsetInBytes, shortValue);// Or, for one pixel at a time
			bitmap.UnlockBits(bitmapData);
			return bitmap;
		}

		///<summary>Returns null if something goes wrong.</summary>
		public static BitmapDicom GetFromBase64(string rawBase64){
			byte[] bytesRaw;
			try{
				bytesRaw=Convert.FromBase64String(rawBase64);
			}
			catch(Exception){//null or not base64
				return null;
			}
			using MemoryStream memoryStream=new MemoryStream(bytesRaw);
			return GetFromStream(memoryStream);
		}

		///<summary>Returns null if something goes wrong. Reads with PixelReader: the in-house subset reader first, then the in-house full reader. Neither throws; ReadStreamResult says why a stream gave no image.</summary>
		public static BitmapDicom GetFromStream(Stream stream){
			return PixelReader.ReadStream(stream);
		}

		///<summary>Returns null if something goes wrong. Reads with PixelReader: the in-house subset reader first, then the in-house full reader. Neither throws; ReadFileResult says why a file gave no image.</summary>
		public static BitmapDicom GetFromFile(string filename){
			if(!File.Exists(filename)){
				return null;
			}
			return PixelReader.ReadFile(filename);
		}

		///<summary>The fo-dicom path of DicomHelper as it was before the subset reader (design 7.8, CAP-DR): it opens the file with fo-dicom, decodes
		///any transfer syntax fo-dicom has a codec for, and reads frame 0. ReadFile returns null when the file does not exist or the pixels
		///cannot be read; opening a file fo-dicom cannot parse throws, as before. ReadStream does not catch.</summary>
		private sealed class FoDicomPixelReader : IDicomPixelReader {
			public BitmapDicom ReadFile(string path){
				if(!File.Exists(path)){
					return null;
				}
				DicomFile dicomFile=DicomFile.Open(path);
				try{
					return GetFromDicomFile(dicomFile);
				}
				catch{
					return null;
				}
			}

			public BitmapDicom ReadStream(Stream stream){
				DicomFile dicomFile=DicomFile.Open(stream);
				return GetFromDicomFile(dicomFile);
			}
		}

		private static BitmapDicom GetFromDicomFile(DicomFile dicomFile){
			/*
			When this method was created, we used example dicom images to figure out how to display them in Open Dental. Since then, we have received additional
			examples of different dicom images that were causing errors when viewing. This method has since been modified to support the new formats each time.
			The example images that were used can be found \\opendental.od\serverfiles\Engineering\dicom_image_examples
			
			Supporting a new format: Typically, when a new format is detected, an error from this method will occur. We should get an example image so we 
			can change our code to handle the new format. Changes to our code will most like happen to this method(DicomHelper.GetFromDicomFile())
			and/or the DicomHelper.CalculateWindowingOnImport().

			After a new format has been added: After the code change for the newly supported format, make sure to add the example dicom image to the network 
			\\opendental.od\serverfiles\Engineering\dicom_image_examples. Follow the existing pattern when creating the new directory.

			Debugging:
			Make sure to run the Open Dental solution in 'x86'. Running the solution in 'ANY CPU' will cause importing and viewing errors.
			*/

			BitmapDicom bitmapDicom=new BitmapDicom();
			DicomDataset dicomDataset=dicomFile.Dataset;
			EnumDicomPhotometricInterp enumPhotometricInterp=PIn.Enum<EnumDicomPhotometricInterp>(dicomDataset.GetSingleValueOrDefault(new DicomTag(0x0028,0x0004),""),isEnumAsString:true,defaultEnumOption:EnumDicomPhotometricInterp.None);
			if(enumPhotometricInterp==EnumDicomPhotometricInterp.None){
				return null;
			}
			bitmapDicom.PhotometricInterp=enumPhotometricInterp;
			bitmapDicom.BitsAllocated=dicomDataset.GetSingleValueOrDefault<ushort>(new DicomTag(0x0028,0x0100),0);
			if(bitmapDicom.BitsAllocated!=16 && bitmapDicom.BitsAllocated!=8) {
				return null;
			}
			bitmapDicom.BitsStored=dicomDataset.GetSingleValueOrDefault<ushort>(new DicomTag(0x0028,0x0101),0);
			if(bitmapDicom.BitsStored!=12 && bitmapDicom.BitsStored!=8 && bitmapDicom.BitsStored!=16){
				return null;
			}
			bitmapDicom.Height=dicomDataset.GetSingleValueOrDefault(new DicomTag(0x0028,0x0010),0);//rows
			if(bitmapDicom.Height==0){
				return null;
			}
			bitmapDicom.Width=dicomDataset.GetSingleValueOrDefault(new DicomTag(0x0028,0x0011),0);//columns
			if(bitmapDicom.Width==0){
				return null;
			}
			DicomDataset dicomDatasetDecoded=dicomDataset.Clone(DicomTransferSyntax.ExplicitVRLittleEndian);//uses codec to decompress
			DicomPixelData dicomPixelDataDecoded=DicomPixelData.Create(dicomDatasetDecoded,false);
			IPixelData iPixelData=PixelDataFactory.Create(dicomPixelDataDecoded,0);
			if(!(iPixelData is GrayscalePixelDataU16) && !(iPixelData is GrayscalePixelDataU8) && !(iPixelData is ColorPixelData24)) {
				return null;//we cannot read other formats yet
			}
			bitmapDicom.ArrayDataRaw=new ushort[bitmapDicom.Height*bitmapDicom.Width];
			for(int r=0;r<bitmapDicom.Height;r++){
				for(int c=0;c<bitmapDicom.Width;c++){
					ushort pixelVal;
					if(iPixelData is ColorPixelData24) {
						Color colorCur=Color.FromArgb((int)iPixelData.GetPixel(c,r));
						//Get the average RGB to convert to gray scale.
						int intGrayScaleAverage=(colorCur.R+colorCur.G+colorCur.B)/3;
						pixelVal=(ushort)intGrayScaleAverage;
					}
					else {
						pixelVal=(ushort)iPixelData.GetPixel(c,r);
					}
					bitmapDicom.ArrayDataRaw[r*bitmapDicom.Width+c]=pixelVal;
				}
			}  
			return bitmapDicom;
		}

		public static void CalculateWindowingOnImport(BitmapDicom bitmapDicom){
			if(bitmapDicom.BitsAllocated==8) {
				bitmapDicom.WindowingMax=255;
				return;
			}
			if(bitmapDicom.BitsStored==16) {
				//Full range on the 0-255 windowing scale. Before 25.4 this was 4095, which showed the same full range because raw values were divided by 16.
				bitmapDicom.WindowingMin=0;
				bitmapDicom.WindowingMax=255;
				return;
			}
			int[] histogram=new int[4096];
			for(int i=0;i<bitmapDicom.ArrayDataRaw.Length;i++){
				histogram[bitmapDicom.ArrayDataRaw[i]]++;
			}
			int idxBlackPixels=GetIndexForValue(histogram,histogram.Max());//max value will hopefully be the black pixels.
			if(idxBlackPixels < 100) { //it's at the left, so it's almost certainly black
				histogram[idxBlackPixels]=0;//throw out the large number of black pixels
			}
			int idxPeak=GetIndexForValue(histogram,histogram.Max());
			if(bitmapDicom.PhotometricInterp==EnumDicomPhotometricInterp.MONOCHROME1) {
				//MONOCHROME1 Grayscale ranges from bright to dark. Our logic below expects the grayscale to go from dark to bright.
				//Because of that, we need to hunt up(GetWindowingMax) to find the min, and hunt down(GetWindowingMin) to find the max.
				bitmapDicom.WindowingMin=GetWindowingMax(histogram,idxPeak);
				if(bitmapDicom.WindowingMin==4095) {
					//Default value of max. Change to default value of min
					bitmapDicom.WindowingMin=0;
				}
				bitmapDicom.WindowingMax=GetWindowingMin(histogram,idxPeak);
				if(bitmapDicom.WindowingMax==0) {
					//Default value. Set to the actual max default value of 4095
					bitmapDicom.WindowingMax=4095;
				}
			}
			else {
				bitmapDicom.WindowingMin=GetWindowingMin(histogram,idxPeak);
				bitmapDicom.WindowingMax=GetWindowingMax(histogram,idxPeak);
			}
		}

		private static int GetWindowingMin(int[] histogram,int idxPeak) {
			int threshold=150;
			for(int i=idxPeak;i>=0;i--){//hunt down
				if(histogram[i]<threshold){
					return i/16;//converting from 12 bit to 8 bit
				}
			}
			return 0;
		}

		private static int GetWindowingMax(int[] histogram,int idxPeak) {
			int threshold=150;
			for(int i=idxPeak;i<histogram.Length;i++){//hunt up
				if(histogram[i]<threshold){
					return i/16;//converting from 12 bit to 8 bit
				}
			}
			return 4095;
		}

		///<summary>Returns the index of the value passed in. Return 0 if it cannot find the value.</summary>
		private static int GetIndexForValue(int[] histogram,int value) {
			if(histogram==null) {
				return 0;
			}
			int idxOfValue=0;
			for(int i=0;i<histogram.Length;i++){
				if(histogram[i]==value){
					idxOfValue=i;
					break;
				}
			}
			return idxOfValue;
		}

		/*
		///<summary>Gets the size of the bitmap at the designated file location.</summary>
		public static Size GetSizeBitmap(string filename){
			if(!File.Exists(filename)){
				return default;
			}
			DicomFile dicomFile=DicomFile.Open(filename);
			DicomDataset dicomDataset=dicomFile.Dataset;
			int height=dicomDataset.GetSingleValueOrDefault(new DicomTag(0x0028,0x0010),0);//rows
			int width=dicomDataset.GetSingleValueOrDefault(new DicomTag(0x0028,0x0011),0);//columns
			if(height==0 || width==0){
				return default;
			}
			return new Size(width,height);
		}*/
	}

	public enum EnumDicomPhotometricInterp {
		None=0,
		MONOCHROME1,
		MONOCHROME2,
		RGB
	}
}
