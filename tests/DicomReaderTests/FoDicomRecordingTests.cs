// Runs fo-dicom 4.0.7 (DicomHelper.FoDicomReader, the old path) on every synthetic case and checks it against the values recorded in
// FoDicomBaseline.cs. It exists only while fo-dicom is still in the repository; the recorded values stay when fo-dicom goes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace DicomReaderTests {
	[TestClass]
	public class FoDicomRecordingTests {
		[TestMethod]
		public void FoDicom4_ReadsEveryCaseAsRecorded() {
			List<ReaderCase> listCases=ReaderCases.All();
			Assert.AreEqual(listCases.Count,listCases.Select(x => x.Name).Distinct().Count(),"Case names are unique.");
			StringBuilder stringBuilderTable=new StringBuilder();
			List<string> listProblems=new List<string>();
			foreach(ReaderCase readerCase in listCases) {
				BitmapDicom bitmapDicom=TestFiles.ReadWithFoDicom(readerCase.Bytes,out string error);
				string actual=error!=null ? "throws" : FoDicomBaseline.Fingerprint(bitmapDicom);
				stringBuilderTable.AppendLine("\t\t\t{ \""+readerCase.Name+"\",\""+actual+"\" },"+(error==null ? "" : "//"+error.Replace("\r","").Replace("\n"," ")));
				if(!FoDicomBaseline.DictRecorded.TryGetValue(readerCase.Name,out string recorded)) {
					listProblems.Add(readerCase.Name+": not recorded");
				}
				else if(recorded!=actual) {
					listProblems.Add(readerCase.Name+": recorded "+recorded+", fo-dicom now "+actual);
				}
			}
			foreach(string name in FoDicomBaseline.DictRecorded.Keys.Where(x => !listCases.Any(y => y.Name==x))) {
				listProblems.Add(name+": recorded, but no such case");
			}
			if(FoDicomBaseline.JpegGrayBase64=="" || FoDicomBaseline.JpegRgbBase64=="") {
				listProblems.Add("JPEG streams not fixed yet:\r\nGRAY "+Convert.ToBase64String(ReaderCases.JpegGrayFixed())+"\r\nRGB "
					+Convert.ToBase64String(ReaderCases.JpegRgbFixed()));
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems)+"\r\nTABLE-START\r\n"+stringBuilderTable+"TABLE-END");
		}
	}
}
