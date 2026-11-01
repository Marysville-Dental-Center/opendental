// B-PRINT: e-mailed statements carry the patient's last name (Open Dental 25.3). Statements.GetEmailPdfDisplayName.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BPRINTTests {
	[TestClass]
	public class StatementEmailNameTests {
		private static Patient Pat(string lName,string fName="Synthetic") {
			return new Patient { PatNum=42,LName=lName,FName=fName,Birthdate=new DateTime(1980,5,6) };
		}

		[TestMethod]
		public void Attachment_IsStatementUnderscoreLastName() {
			Assert.AreEqual("Statement_Smith.pdf",Statements.GetEmailPdfDisplayName(Pat("Smith")));
			Assert.AreEqual("Statement_O'Brien.pdf",Statements.GetEmailPdfDisplayName(Pat("O'Brien")));
			Assert.AreEqual("Statement_Muñoz.pdf",Statements.GetEmailPdfDisplayName(Pat("Muñoz")),"Accented letters are allowed in file names.");
			Assert.AreEqual("Statement_de la Cruz.pdf",Statements.GetEmailPdfDisplayName(Pat("  de   la\tCruz ")),"Spaces collapse to one and are trimmed.");
		}

		[TestMethod]
		public void Attachment_NeverCarriesFirstNameBirthDateOrAccount() {
			string name=Statements.GetEmailPdfDisplayName(Pat("Smith","Johnathan"));
			Assert.IsFalse(name.Contains("Johnathan"));
			Assert.IsFalse(name.Contains("1980"));
			Assert.IsFalse(name.Contains("42"));
		}

		[TestMethod]
		public void CharactersWindowsRefuses_AreDropped() {
			Assert.AreEqual("Statement_Smith.pdf",Statements.GetEmailPdfDisplayName(Pat("Sm/i\\th:*?\"<>|")));
			Assert.AreEqual("Statement_Smith.pdf",Statements.GetEmailPdfDisplayName(Pat("Smi\u0001th")),"Control characters are dropped.");
			Assert.AreEqual("Statement_Smith.pdf",Statements.GetEmailPdfDisplayName(Pat("..Smith..")),"No leading or trailing dots.");
		}

		[TestMethod]
		public void NoUsableLastName_KeepsThe24_3Name() {
			Assert.AreEqual("Statement.pdf",Statements.GetEmailPdfDisplayName(null));
			Assert.AreEqual("Statement.pdf",Statements.GetEmailPdfDisplayName(Pat("")));
			Assert.AreEqual("Statement.pdf",Statements.GetEmailPdfDisplayName(Pat(null)));
			Assert.AreEqual("Statement.pdf",Statements.GetEmailPdfDisplayName(Pat("   ")));
			Assert.AreEqual("Statement.pdf",Statements.GetEmailPdfDisplayName(Pat("/:*?")));
		}

		[TestMethod]
		public void LongLastNames_AreCut() {
			string lName=new string('A',30)+" "+new string('B',30);
			string safe=Statements.GetFileNameSafeLastName(lName);
			Assert.AreEqual(Statements.EMAIL_PDF_LNAME_MAX_LENGTH,safe.Length);
			Assert.AreEqual("Statement_"+safe+".pdf",Statements.GetEmailPdfDisplayName(Pat(lName)));
			Assert.IsFalse(Statements.GetFileNameSafeLastName(new string('A',39)+" B").EndsWith(" "),"No trailing space after the cut.");
		}
	}
}
