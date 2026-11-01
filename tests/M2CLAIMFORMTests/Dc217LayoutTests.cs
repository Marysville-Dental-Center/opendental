// Layout of the built-in Denti-Cal DC217_2019 claim form (Open Dental 25.4; backlog package M2-CLAIMFORM): what it prints and where, checked
// without a database or a printer. The form prints onto preprinted DC-217 stock, so it has no background image.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CLAIMFORMTests {
	[TestClass]
	public class Dc217LayoutTests {
		///<summary>Items that share one box on purpose: either condition marks it.</summary>
		private static readonly string[][] _arraySharedBoxes=new[] { new[] { "IsAutoAccident","IsOtherAccident" } };

		[TestInitialize]
		public void TestInitialize() {
			ClaimFormDb.UseUsEnglish();
		}

		[TestMethod]
		public void EmbeddedForm_IsTheFileAndListedOnce() {
			ClaimForm claimFormFile=Dc217.FromFile();
			ClaimForm claimFormEmbedded=Dc217.FromResource();
			Assert.IsNotNull(claimFormEmbedded,"OpenDentBusiness ships the form as an internal claim form.");
			Assert.IsTrue(claimFormEmbedded.IsInternal);
			Assert.AreEqual("Denti-Cal DC217_2019",ClaimForms.DescriptionDentiCalDC217_2019);
			Assert.AreEqual(ClaimForms.DescriptionDentiCalDC217_2019,claimFormFile.Description);
			CollectionAssert.AreEqual(Dc217.Describe(claimFormFile),Dc217.Describe(claimFormEmbedded));
			List<string> listDescriptions=ClaimForms.GetInternalClaims().Select(x => x.Description).ToList();
			Assert.AreEqual(1,listDescriptions.Count(x => x==ClaimForms.DescriptionDentiCalDC217_2019));
			Assert.AreEqual(listDescriptions.Count,listDescriptions.Distinct().Count(),"Internal claim forms are told apart by description.");
			Assert.IsTrue(listDescriptions.Contains("Denti-Cal-13"),"The older Denti-Cal form stays.");
			Assert.IsNull(ClaimForms.GetInternalClaim("No such claim form"));
		}

		[TestMethod]
		public void Form_PrintsOnPreprintedStockWithoutArtwork() {
			ClaimForm claimForm=Dc217.FromResource();
			Assert.IsFalse(claimForm.PrintImages);
			Assert.IsTrue(claimForm.Items.All(x => x.ImageFileName==""),"No background image item.");
			Assert.IsTrue(claimForm.Items.All(x => x.FieldName!=""),"Every item prints a field.");
			Assert.IsFalse(claimForm.Items.Any(x => x.FieldName=="FixedText"),"No printed text of the form itself.");
			Assert.AreEqual(850,claimForm.Width);
			Assert.AreEqual(1100,claimForm.Height);
			Assert.AreEqual("Arial",claimForm.FontName);
			Assert.AreEqual(9f,claimForm.FontSize);
			Assert.IsFalse(claimForm.IsHidden);
			Assert.AreEqual("",claimForm.UniqueID);
			Assert.AreEqual(0,claimForm.OffsetX,"Offsets are the office's printer calibration.");
			Assert.AreEqual(0,claimForm.OffsetY);
		}

		[TestMethod]
		public void Repository_HasNoDc217Artwork() {
			//The file is <root>\OpenDentBusiness\Resources\ClaimForms\..., four levels below the repository root.
			string root=Path.GetFullPath(TestDb.FindRepoFile(Dc217.RelativePath));
			for(int i=0;i<4;i++) {
				root=Path.GetDirectoryName(root);
			}
			Assert.IsTrue(File.Exists(Path.Combine(root,"LICENSE")) && File.Exists(Path.Combine(root,"NOTICE")),root);
			string[] arrayExtensions=new[] { ".gif",".png",".jpg",".jpeg",".bmp",".emf",".tif",".tiff",".pdf" };
			foreach(string folder in new[] { Path.Combine(root,"OpenDentBusiness"),Path.Combine(root,"OpenDental") }) {
				List<string> listArtwork=Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories)
					.Where(x => !x.Contains(@"\bin\") && !x.Contains(@"\obj\"))
					.Where(x => arrayExtensions.Contains(Path.GetExtension(x).ToLowerInvariant()))
					.Where(x => Path.GetFileName(x).Replace("-","").Replace("_","").ToLowerInvariant().Contains("dc217"))
					.ToList();
				Assert.AreEqual(0,listArtwork.Count,string.Join("\r\n",listArtwork));
			}
		}

		[TestMethod]
		public void EveryItem_IsAClaimFormField() {
			HashSet<string> setFieldNames=Dc217.GetClaimFormFieldNames();
			List<string> listUnknown=Dc217.FromResource().Items.Select(x => x.FieldName).Where(x => !setFieldNames.Contains(x)).Distinct().ToList();
			Assert.AreEqual(0,listUnknown.Count,"Not in the claim form editor's field list: "+string.Join(", ",listUnknown));
		}

		[TestMethod]
		public void Form_MapsTheMediCalFields() {
			ClaimForm claimForm=Dc217.FromResource();
			//Patient: name, sex, birth date, Medi-Cal ID (BIC), address.
			foreach(string fieldName in new[] { "PatientLastFirst","PatientIsMale","PatientIsFemale","PatientDOB","PatientMedicaidID","PatientAddress",
				"PatientCity","PatientST","PatientZip",
				//Billing provider: name, NPI, address, phone.
				"BillingDentist","BillingDentistNPI","PayToDentistAddress","PayToDentistCity","PayToDentistST","PayToDentistZip",
				"BillingDentistPh123","BillingDentistPh456","BillingDentistPh78910",
				//Questions and place of service.
				"IsRadiographsAttached","RadiographsNumAttached","OtherInsExists","IsOccupational","IsOrtho","PlaceIsOffice",
				//Totals, remarks, signature.
				"TotalFee","Remarks","TreatingDentistSignature" })
			{
				Assert.AreEqual(1,claimForm.Items.Count(x => x.FieldName==fieldName),fieldName);
			}
			Assert.AreEqual(2,claimForm.Items.Count(x => x.FieldName=="TreatingDentistSigDate"),"The signature date prints in both date boxes.");
			Assert.IsFalse(claimForm.Items.Any(x => x.FieldName.StartsWith("Subscr") || x.FieldName.StartsWith("PriIns")),
				"The DC-217 identifies the beneficiary by the Medi-Cal ID, not by a subscriber or carrier.");
			Assert.AreEqual(142,claimForm.Items.Count);
		}

		[TestMethod]
		public void ServiceLines_FifteenPerForm() {
			ClaimForm claimForm=Dc217.FromResource();
			for(int line=1;line<=Dc217.LINE_COUNT;line++) {
				foreach(string field in Dc217.ArrayLineFields) {
					Assert.AreEqual(1,claimForm.Items.Count(x => x.FieldName=="P"+line+field),"P"+line+field);
				}
			}
			Assert.AreEqual(Dc217.LINE_COUNT*Dc217.ArrayLineFields.Length,claimForm.Items.Count(x => x.FieldName.StartsWith("P")
				&& x.FieldName.Length>1 && char.IsDigit(x.FieldName[1])));
			ClaimPrintHarness claimPrintHarness=new ClaimPrintHarness(claimForm);
			Assert.AreEqual(15,claimPrintHarness.ProcLimit(),"The printout puts 15 procedures on one form.");
			Assert.AreEqual(1,claimPrintHarness.PageCount(15));
			Assert.AreEqual(2,claimPrintHarness.PageCount(16),"Line 16 starts a new form.");
			Assert.AreEqual(3,claimPrintHarness.PageCount(31));
		}

		[TestMethod]
		public void ServiceLines_AlignInRowsAndColumns() {
			ClaimForm claimForm=Dc217.FromResource();
			List<float> listRowY=new List<float>();
			for(int line=1;line<=Dc217.LINE_COUNT;line++) {
				List<ClaimFormItem> listItems=Dc217.ArrayLineFields.Select(x => Dc217.Item(claimForm,"P"+line+x)).ToList();
				Assert.AreEqual(1,listItems.Select(x => x.YPos).Distinct().Count(),"Line "+line+" prints on one row.");
				Assert.IsTrue(listItems.All(x => x.Height==14),"Line "+line);
				listRowY.Add(listItems[0].YPos);
			}
			//Six lines per inch (1/100 inch units): each step is 16 or 17, and 14 steps span 233 to 234.
			for(int i=1;i<listRowY.Count;i++) {
				float step=listRowY[i]-listRowY[i-1];
				Assert.IsTrue(step==16 || step==17,"Line "+(i+1)+" is "+step+" below line "+i+".");
			}
			float span=listRowY.Last()-listRowY.First();
			Assert.IsTrue(Math.Abs(span-14*100/6.0)<=1,"Lines 1 to 15 span "+span+".");
			//Every column keeps its position and width on every line, left to right in the DC-217 order.
			int rightEdgePrevious=0;
			foreach(string field in Dc217.ArrayLineFields) {
				List<ClaimFormItem> listColumn=Enumerable.Range(1,Dc217.LINE_COUNT).Select(x => Dc217.Item(claimForm,"P"+x+field)).ToList();
				Assert.AreEqual(1,listColumn.Select(x => x.XPos+","+x.Width+","+x.FormatString).Distinct().Count(),field);
				RectangleF rect=GetPrintRect(listColumn[0]);
				Assert.IsTrue(rect.Left>=rightEdgePrevious,field+" is right of the column before it.");
				rightEdgePrevious=(int)Math.Ceiling(rect.Right);
			}
			Assert.AreEqual("MM/dd/yy",Dc217.Item(claimForm,"P1Date").FormatString);
			Assert.AreEqual("10/24/26",new DateTime(2026,10,24).ToString(Dc217.Item(claimForm,"P1Date").FormatString));
		}

		[TestMethod]
		public void PrintedBoxes_DoNotOverlapAndStayInThePrintableArea() {
			ClaimForm claimForm=Dc217.FromResource();
			List<string> listProblems=new List<string>();
			List<ClaimFormItem> listItems=claimForm.Items;
			for(int i=0;i<listItems.Count;i++) {
				RectangleF rect=GetPrintRect(listItems[i]);
				//A quarter inch from every edge, where laser printers print.
				if(rect.Left<25 || rect.Top<25 || rect.Right>claimForm.Width-25 || rect.Bottom>claimForm.Height-25) {
					listProblems.Add(listItems[i].FieldName+" is outside the printable area: "+rect);
				}
				for(int j=i+1;j<listItems.Count;j++) {
					if(IsSharedBox(listItems[i].FieldName,listItems[j].FieldName)) {
						continue;
					}
					if(rect.IntersectsWith(GetPrintRect(listItems[j]))) {
						listProblems.Add(listItems[i].FieldName+" overlaps "+listItems[j].FieldName);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>Measured as the printout draws them: Arial 9 in 1/100 inch units, with the default string format of Graphics.DrawString, so a value
		///that does not fit would wrap and be cut off. One line of Arial 9 is about 15.5 high, a little more than the 14 of every claim form box,
		///which only trims the descenders (the same on every built-in form).</summary>
		[TestMethod]
		public void TypicalValues_FitTheirBoxes() {
			ClaimForm claimForm=Dc217.FromResource();
			Dictionary<string,string> dictSamples=new Dictionary<string,string> {
				{"PatientLastFirst","Synthetic-Patient, Alexandra Q"},
				{"PatientDOB",new DateTime(2016,12,31).ToString(Dc217.Item(claimForm,"PatientDOB").FormatString)},
				{"PatientMedicaidID","90000000A95001"},//The 14-character Medi-Cal benefits ID.
				{"PatientAddress","12345 Synthetic Boulevard Apt 678"},
				{"PatientCity","Synthetic Valley"},
				{"PatientST","CA"},
				{"PatientZip","95901-1234"},
				{"RadiographsNumAttached","12"},
				{"BillingDentist","Synthetic Dental Group Inc"},
				{"BillingDentistNPI","1234567890"},
				{"PayToDentistAddress","500 Synthetic Way Suite 200"},
				{"BillingDentistPh123","530"},
				{"BillingDentistPh456","555"},
				{"BillingDentistPh78910","0100"},
				{"PayToDentistCity","Synthetic Valley"},
				{"PayToDentistST","CA"},
				{"PayToDentistZip","95901-1234"},
				{"TotalFee","99999.99"},
				{"TreatingDentistSignature","Alexandra Q Synthetic-Dentist"},
				{"TreatingDentistSigDate",new DateTime(2026,12,31).ToString("MM/dd/yyyy")},
			};
			Dictionary<string,string> dictLineSamples=new Dictionary<string,string> {
				{"ToothNumOrArea","32"},
				{"Surface","MODBL"},
				{"Description","Synthetic service with a long description"},
				{"Date",new DateTime(2026,12,31).ToString("MM/dd/yy")},
				{"Code","Z9999"},
				{"Fee","9999.99"},
				{"TreatProvNPI","1234567890"},
			};
			using(Bitmap bitmap=new Bitmap(10,10))
			using(Font font=new Font(claimForm.FontName,claimForm.FontSize)) {
				bitmap.SetResolution(100,100);//As a printer page: 1/100 inch units.
				using(Graphics g=Graphics.FromImage(bitmap)) {
					List<string> listProblems=new List<string>();
					foreach(ClaimFormItem item in claimForm.Items) {
						string sample;
						if(!dictSamples.TryGetValue(item.FieldName,out sample)) {
							string lineField=Dc217.ArrayLineFields.FirstOrDefault(x => item.FieldName.EndsWith(x) && char.IsDigit(item.FieldName[1]));
							if(lineField==null || !dictLineSamples.TryGetValue(lineField,out sample)) {
								continue;//Check marks and remarks.
							}
						}
						SizeF size=g.MeasureString(sample,font);
						if(size.Width>item.Width) {//Wider wraps onto a second line, which the 14-high box cuts off.
							listProblems.Add(item.FieldName+": '"+sample+"' needs "+size+", box is "+item.Width+"x"+item.Height);
						}
					}
					Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
				}
			}
		}

		///<summary>Where the printout draws the item: fee fields end at XPos (FormClaimPrint right-aligns them); a check mark (no size) is an "X" of
		///about 10 by 14.</summary>
		private static RectangleF GetPrintRect(ClaimFormItem item) {
			if(item.Width==0 || item.Height==0) {
				return new RectangleF(item.XPos,item.YPos,10,14);
			}
			if(Dc217.IsRightAligned(item)) {
				return new RectangleF(item.XPos-item.Width,item.YPos,item.Width,item.Height);
			}
			return new RectangleF(item.XPos,item.YPos,item.Width,item.Height);
		}

		private static bool IsSharedBox(string fieldName1,string fieldName2) {
			if(fieldName1==fieldName2) {
				return false;
			}
			return _arraySharedBoxes.Any(x => x.Contains(fieldName1) && x.Contains(fieldName2));
		}
	}
}
