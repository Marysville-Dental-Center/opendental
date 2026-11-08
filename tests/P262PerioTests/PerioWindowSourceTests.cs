// Source checks for the Perio Chart window (FormPerio), which these tests cannot open (the desktop client is never launched by tests): the two
// settings of Open Dental 26.2 are on the window, read and saved per user, and passed to the grid; the flag buttons say what they do with teeth
// selected. The grid itself is tested for real in the other test classes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace P262PerioTests {
	[TestClass]
	public class PerioWindowSourceTests {
		private static string FormPerio() {
			return PerioDb.ReadRepoFile("OpenPmsDental","Forms","FormPerio.cs");
		}

		private static string Designer() {
			return PerioDb.ReadRepoFile("OpenPmsDental","Forms","FormPerio.designer.cs");
		}

		[TestMethod]
		public void Window_HasBothSettings_WithTheTextOfOpenDental26_2() {
			string designer=Designer();
			StringAssert.Contains(designer,"this.checkShowPreviousExams.Text = \"Show previous exams\";");
			StringAssert.Contains(designer,"this.checkShowPreviousProbing.Text = \"Show previous probing values\";");
			StringAssert.Contains(designer,"this.Controls.Add(this.checkShowPreviousExams);");
			StringAssert.Contains(designer,"this.Controls.Add(this.checkShowPreviousProbing);");
			Assert.IsFalse(designer.Contains("Show current exam only"),"The check box before 26.2 is replaced.");
			Assert.IsFalse(Regex.IsMatch(FormPerio()+designer,@"\bcheckShowCurrent\b"));
		}

		[TestMethod]
		public void Window_ReadsAndSavesTheSettingsPerUser_AndPassesThemToTheGrid() {
			string code=FormPerio();
			StringAssert.Contains(code,"checkShowPreviousExams.Checked=PerioMeasures.GetShowPreviousExams(Security.CurUser.UserNum);");
			StringAssert.Contains(code,"checkShowPreviousProbing.Checked=PerioMeasures.GetShowPreviousProbing(Security.CurUser.UserNum);");
			StringAssert.Contains(code,"PerioMeasures.SaveShowPrevious(Security.CurUser.UserNum,checkShowPreviousExams.Checked,checkShowPreviousProbing.Checked)");
			StringAssert.Contains(code,"contrPerio.SetShowPrevious(checkShowPreviousExams.Checked,checkShowPreviousProbing.Checked);");
			Assert.IsFalse(code.Contains("SetShowCurrentExamOnly"),"The window uses the 26.2 settings, not the method kept for the grid before 26.2.");
			//The probing setting only counts while previous exams are shown, so its check box follows the other one.
			Assert.AreEqual(2,Regex.Matches(code,@"checkShowPreviousProbing\.Enabled=checkShowPreviousExams\.Checked;").Count,"On load and on click.");
			//Both check boxes show the grid again.
			StringAssert.Contains(code,"private void checkShowPreviousExams_Click(object sender,EventArgs e) {");
			StringAssert.Contains(code,"private void checkShowPreviousProbing_Click(object sender,EventArgs e) {");
			Assert.AreEqual(3,Regex.Matches(code,@"RefillGridForShowPrevious\(\)").Count,"Both handlers call it, plus its declaration.");
			string designer=Designer();
			StringAssert.Contains(designer,"this.checkShowPreviousExams.Click += new System.EventHandler(this.checkShowPreviousExams_Click);");
			StringAssert.Contains(designer,"this.checkShowPreviousProbing.Click += new System.EventHandler(this.checkShowPreviousProbing_Click);");
		}

		[TestMethod]
		public void Window_FlagButtonsAndKeys_ReachTheGrid() {
			string code=FormPerio();
			foreach(string letter in new[] { "b","c","p","s" }) {
				StringAssert.Contains(code,"contrPerio.ButtonPressedLetter(\""+letter+"\");");
			}
			string designer=Designer();
			foreach(string control in new[] { "butBleed","butCalculus","butPlaque","butPus" }) {
				Match match=Regex.Match(designer,@"this\.toolTip1\.SetToolTip\(this\."+control+@", ""([^""]*)""\);");
				Assert.IsTrue(match.Success,control);
				StringAssert.Contains(match.Groups[1].Value,"With teeth selected (click their numbers)",control);
			}
		}

		[TestMethod]
		public void Window_SettingsDoNotOverlapOtherControls() {
			string designer=Designer();
			//Controls placed on the window itself (not inside a group box), with their rectangles.
			Dictionary<string,Rectangle> dictRectangles=new Dictionary<string,Rectangle>();
			foreach(Match matchAdd in Regex.Matches(designer,@"this\.Controls\.Add\(this\.(\w+)\);")) {
				string name=matchAdd.Groups[1].Value;
				Match matchLocation=Regex.Match(designer,@"this\."+name+@"\.Location = new System\.Drawing\.Point\((\d+), (\d+)\);");
				Match matchSize=Regex.Match(designer,@"this\."+name+@"\.Size = new System\.Drawing\.Size\((\d+), (\d+)\);");
				if(!matchLocation.Success || !matchSize.Success) {
					continue;
				}
				dictRectangles[name]=new Rectangle(int.Parse(matchLocation.Groups[1].Value),int.Parse(matchLocation.Groups[2].Value),
					int.Parse(matchSize.Groups[1].Value),int.Parse(matchSize.Groups[2].Value));
			}
			Assert.IsTrue(dictRectangles.Count>20,"Controls found: "+dictRectangles.Count);
			foreach(string nameNew in new[] { "checkShowPreviousExams","checkShowPreviousProbing" }) {
				Assert.IsTrue(dictRectangles.ContainsKey(nameNew),nameNew);
				foreach(KeyValuePair<string,Rectangle> pair in dictRectangles) {
					if(pair.Key==nameNew) {
						continue;
					}
					Assert.IsFalse(dictRectangles[nameNew].IntersectsWith(pair.Value),nameNew+" overlaps "+pair.Key);
				}
			}
		}
	}
}
