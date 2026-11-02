// Source checks for the three windows of package M2-ADJ. The windows cannot be opened in a test run (OpenPmsDental.exe is not launched), so these
// tests make sure the hooks that route the windows through the tested business methods are in place, for example after a merge.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2ADJTests {
	[TestClass]
	public class WindowWiringTests {
		///<summary>Walks up from the test assembly until it finds the repository root (LICENSE and NOTICE).</summary>
		private static string GetRepoFile(params string[] arrayParts) {
			string dir=Path.GetDirectoryName(typeof(WindowWiringTests).Assembly.Location);
			while(dir!=null && !(File.Exists(Path.Combine(dir,"LICENSE")) && File.Exists(Path.Combine(dir,"NOTICE")))) {
				dir=Path.GetDirectoryName(dir);
			}
			if(dir==null) {
				Assert.Inconclusive("Could not find the repository root (LICENSE and NOTICE) above the test assembly.");
			}
			string path=dir;
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return path;
		}

		private static string ReadForm(string fileName) {
			return File.ReadAllText(GetRepoFile("OpenPmsDental","Forms",fileName));
		}

		[TestMethod]
		public void EditAdjustment_SavesTheAmountThroughPercentOfFee() {
			string source=ReadForm("FormAdjust.cs");
			//Every amount saved or checked comes from GetAmountEntered, which turns a percentage into dollars.
			Assert.IsFalse(Regex.IsMatch(source,@"AdjAmt\s*=\s*-?\s*PIn\.Double\(\s*textAmount\.Text\s*\)"),"AdjAmt is set straight from the amount box.");
			Assert.IsFalse(source.Contains("textAmount.Value!=0"),"The zero-amount check reads the box, not the dollars.");
			Assert.AreEqual(3,Regex.Matches(source,@"AdjAmt\s*=\s*-?GetAmountEntered\(\)").Count);
			Assert.AreEqual(2,Regex.Matches(source,@"\+GetPercentOfFeeLogNote\(\)").Count,"Both security log entries name the percentage.");
			StringAssert.Contains(source,"FillPercentOfFee(null);");
			StringAssert.Contains(source,"FillPercentOfFee(procedure);");
			StringAssert.Contains(source,"if(!IsPercentOfFeeValid())");
			Assert.AreEqual(4,Regex.Matches(source,@"ApplyAdjTypeDefaultPercent\(onlyIfAmountIsZero:(true|false)\);").Count,
				"Defaults apply on load, on both type lists and on attaching a procedure.");
			string designer=ReadForm("FormAdjust.designer.cs");
			foreach(string control in new[] { "radioFixedAmount","radioPercentOfFee","labelPercentOfFeeAmount" }) {
				StringAssert.Contains(designer,"this.Controls.Add(this."+control+");");
			}
			StringAssert.Contains(designer,"this.radioPercentOfFee.Enabled = false;");
			StringAssert.Contains(designer,"this.radioFixedAmount.Checked = true;");
		}

		[TestMethod]
		public void AddMultipleAdjustments_UsesTheSharedCalculationAndDefaults() {
			string source=ReadForm("FormAdjMulti.cs");
			StringAssert.Contains(source,"Adjustments.GetPercentOfFeeAmount((double)procAdjs.AccountEntryProc.AmountOriginal,(decimal)adjAmtOrPerc)");
			Assert.AreEqual(2,Regex.Matches(source,@"\bApplyAdjTypeDefaultPercent\(\);").Count,"Both type lists apply the default.");
		}

		[TestMethod]
		public void EditDefinition_SavesTheDefaultPercentOfAdjustmentTypes() {
			string source=ReadForm("FormDefEdit.cs");
			StringAssert.Contains(source,"Adjustments.TryGetSuppFromPercentInput(textDefaultPercent.Text,CultureInfo.CurrentCulture,out suppNew)");
			StringAssert.Contains(source,"_def.Supp=suppNew;");
			StringAssert.Contains(source,"FillDefaultPercent();");
			string designer=File.ReadAllText(GetRepoFile("OpenPmsDental","Forms","FormDefEdit.designer.cs"));
			StringAssert.Contains(designer,"this.Controls.Add(this.textDefaultPercent);");
		}
	}
}
