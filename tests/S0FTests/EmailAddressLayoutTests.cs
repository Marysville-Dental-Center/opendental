// S0-F (4): the Email Authentication box of Edit Email Address. With the Gmail and Microsoft 365 program links off (the usual case) it showed
// only Password in a 962x308 box made for both sign-in boxes, which the window check found mostly empty. Now the box is sized to what it
// shows (FormEmailAddressEdit.CalculateAuthenticationLayout, in 96 dpi pixels): the shown choices stack from the top, the password or the chosen
// sign-in's box goes to their right, the box ends just below the taller of the two, and the settings below and the window follow. No option is
// removed. The sizes come from the designer file, so the tests follow it; with Gmail chosen the layout is exactly the designer's. No window is
// shown.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using Layout=OpenPmsDental.FormEmailAddressEdit.EmailAuthenticationLayout;

namespace S0FTests {
	[TestClass]
	public class EmailAddressLayoutTests {
		private static Dictionary<string,Rectangle> _dictBounds;

		///<summary>Location and size of every control in FormEmailAddressEdit.designer.cs, by name.</summary>
		private static Dictionary<string,Rectangle> GetDesignerBounds() {
			if(_dictBounds!=null) {
				return _dictBounds;
			}
			string source=File.ReadAllText(S0FDb.FindRepoFile(Path.Combine("OpenPmsDental","Forms","FormEmailAddressEdit.designer.cs")));
			Dictionary<string,Point> dictLocations=new Dictionary<string,Point>();
			foreach(Match match in Regex.Matches(source,@"this\.(\w+)\.Location = new System\.Drawing\.Point\((\d+), (\d+)\);")) {
				dictLocations[match.Groups[1].Value]=new Point(int.Parse(match.Groups[2].Value),int.Parse(match.Groups[3].Value));
			}
			Dictionary<string,Rectangle> dictBounds=new Dictionary<string,Rectangle>();
			foreach(Match match in Regex.Matches(source,@"this\.(\w+)\.Size = new System\.Drawing\.Size\((\d+), (\d+)\);")) {
				string name=match.Groups[1].Value;
				if(dictLocations.ContainsKey(name)) {
					dictBounds[name]=new Rectangle(dictLocations[name],new Size(int.Parse(match.Groups[2].Value),int.Parse(match.Groups[3].Value)));
				}
			}
			Match matchClient=Regex.Match(source,@"this\.ClientSize = new System\.Drawing\.Size\((\d+), (\d+)\);");
			Assert.IsTrue(matchClient.Success);
			dictBounds["ClientSize"]=new Rectangle(0,0,int.Parse(matchClient.Groups[1].Value),int.Parse(matchClient.Groups[2].Value));
			_dictBounds=dictBounds;
			return dictBounds;
		}

		private static Rectangle Bounds(string name) {
			Dictionary<string,Rectangle> dictBounds=GetDesignerBounds();
			Assert.IsTrue(dictBounds.ContainsKey(name),"In the designer: "+name);
			return dictBounds[name];
		}

		private static Layout Calculate(bool isGmailShown,bool isMicrosoftShown,string nameDetails) {
			return FormEmailAddressEdit.CalculateAuthenticationLayout(isGmailShown,isMicrosoftShown,isPasswordDetails:nameDetails=="panelPassword",
				Bounds("radioPassword").Height,Bounds(nameDetails).Size);
		}

		///<summary>What every layout must give: the choices stacked without gaps, the details right of the widest choice and inside the box, and the
		///box ending MarginBottom below its lowest control (so no empty area).</summary>
		private static void AssertTidy(Layout layout,int countRadios,string nameDetails,string state) {
			Rectangle rectangleGroup=Bounds("groupAuthentication");
			Assert.AreEqual(countRadios,layout.ListYRadios.Count,state);
			for(int i=0;i<countRadios;i++) {
				Assert.AreEqual(Layout.TopRadios+i*Layout.StepRadios,layout.ListYRadios[i],state+": choice "+i+" right under the one above it.");
			}
			int rightRadios=0;
			foreach(string name in new[] { "radioPassword","radioGmail","radioMicrosoft" }) {
				rightRadios=Math.Max(rightRadios,Bounds(name).Right);
			}
			Assert.IsTrue(layout.PointDetails.X>rightRadios,state+": the details start right of the choices ("+rightRadios+").");
			Rectangle rectangleDetails=new Rectangle(layout.PointDetails,Bounds(nameDetails).Size);
			Assert.IsTrue(rectangleDetails.Right<=rectangleGroup.Width-Layout.MarginBottom,state+": the details fit the box's width.");
			int bottomRadios=layout.ListYRadios[countRadios-1]+Bounds("radioPassword").Height;
			int bottomContent=Math.Max(bottomRadios,rectangleDetails.Bottom);
			Assert.AreEqual(bottomContent+Layout.MarginBottom,layout.HeightGroup,state+": the box ends just below its lowest control.");
		}

		[TestMethod]
		public void PasswordOnly_TheBoxIsOneLine() {
			//The program links are off: only Password shows, the window check's case.
			Layout layout=Calculate(false,false,"panelPassword");
			AssertTidy(layout,1,"panelPassword","Password only");
			Assert.AreEqual(new Point(Layout.LeftDetails,Layout.TopPassword),layout.PointDetails);
			Assert.IsTrue(layout.HeightGroup<=70,"Password only: a box of about one line, not "+Bounds("groupAuthentication").Height+" pixels: "+layout.HeightGroup);
			//The password's label lines up with the Password choice (centers within 2 pixels).
			Rectangle rectangleLabel=Bounds("labelPassword");
			int centerLabel=layout.PointDetails.Y+rectangleLabel.Y+rectangleLabel.Height/2;
			int centerRadio=layout.ListYRadios[0]+Bounds("radioPassword").Height/2;
			Assert.IsTrue(Math.Abs(centerLabel-centerRadio)<=2,"Label "+centerLabel+", choice "+centerRadio);
			//What the settings below move up by, and the window with them.
			int shift=Bounds("groupAuthentication").Height-layout.HeightGroup;
			Assert.IsTrue(shift>=230,"About 250 pixels of empty box are gone: "+shift);
			Assert.IsTrue(Bounds("ClientSize").Height-shift>=Bounds("groupOutgoing").Bottom-shift+40,"The buttons stay below the settings.");
		}

		[TestMethod]
		public void AllChoicesShown_PasswordChosen_StillCompact() {
			Layout layout=Calculate(true,true,"panelPassword");
			AssertTidy(layout,3,"panelPassword","All choices, Password");
			Assert.IsTrue(layout.HeightGroup<=100,"Three choices and the password: "+layout.HeightGroup);
			Layout layoutOne=Calculate(true,false,"panelPassword");
			AssertTidy(layoutOne,2,"panelPassword","Password and Gmail");
			Layout layoutOther=Calculate(false,true,"panelPassword");
			AssertTidy(layoutOther,2,"panelPassword","Password and Microsoft 365");
			Assert.AreEqual(Layout.TopRadios+Layout.StepRadios,layoutOther.ListYRadios[1],"Microsoft 365 moves up when Gmail is hidden: no gap.");
		}

		[TestMethod]
		public void SignInChosen_ItsBoxShowsBesideTheChoices() {
			Layout layoutGmail=Calculate(true,false,"groupGmail");
			AssertTidy(layoutGmail,2,"groupGmail","Gmail");
			Assert.AreEqual(new Point(Layout.LeftDetails,Layout.TopSignInBox),layoutGmail.PointDetails);
			Layout layoutMicrosoft=Calculate(false,true,"groupMicrosoft");
			AssertTidy(layoutMicrosoft,2,"groupMicrosoft","Microsoft 365");
			Assert.IsTrue(layoutMicrosoft.HeightGroup<layoutGmail.HeightGroup,"The Microsoft 365 box is shorter than the Gmail box.");
			Layout layoutBoth=Calculate(true,true,"groupMicrosoft");
			AssertTidy(layoutBoth,3,"groupMicrosoft","Microsoft 365, both shown");
		}

		[TestMethod]
		public void GmailChosen_IsTheDesignersLayout() {
			//The constants reproduce the designer: with Gmail chosen the box is the designer's height, so the settings below stay where the designer
			//has them (box bottom plus GapBelow).
			Layout layout=Calculate(true,true,"groupGmail");
			Rectangle rectangleGroup=Bounds("groupAuthentication");
			Assert.AreEqual(rectangleGroup.Height,layout.HeightGroup);
			Assert.AreEqual(Bounds("groupGmail").Y,layout.PointDetails.Y);
			Assert.AreEqual(Bounds("groupIncoming").Y,rectangleGroup.Bottom+Layout.GapBelow);
			Assert.AreEqual(Bounds("groupOutgoing").Y,rectangleGroup.Bottom+Layout.GapBelow);
			Assert.AreEqual(Bounds("radioPassword").Y,Layout.TopRadios);
			Assert.AreEqual(Bounds("radioGmail").Y-Bounds("radioPassword").Y,Layout.StepRadios);
			Assert.AreEqual(Bounds("radioMicrosoft").Y-Bounds("radioGmail").Y,Layout.StepRadios);
			Assert.AreEqual(Bounds("panelPassword").Location,new Point(Layout.LeftDetails,Layout.TopPassword),"The designer's password panel is where it shows.");
		}

		[TestMethod]
		public void Wiring_EveryChangeOfChoiceLaysTheWindowOut() {
			string source=File.ReadAllText(S0FDb.FindRepoFile(Path.Combine("OpenPmsDental","Forms","FormEmailAddressEdit.cs")));
			Match match=Regex.Match(source,@"private void RefreshEmailTextFields\(bool isEnablingEmailFields=false\) \{(?<body>.*?)\r?\n\t\t\}",RegexOptions.Singleline);
			Assert.IsTrue(match.Success);
			Assert.IsTrue(Regex.IsMatch(match.Groups["body"].Value,@"LayoutAuthentication\(\);\s*$"),"RefreshEmailTextFields ends by laying out the box.");
			Match matchLayout=Regex.Match(source,@"private void LayoutAuthentication\(\) \{(?<body>.*?)\r?\n\t\t\}",RegexOptions.Singleline);
			Assert.IsTrue(matchLayout.Success);
			string body=matchLayout.Groups["body"].Value;
			foreach(string name in new[] { "groupAuthentication","groupIncoming","groupOutgoing","panelUserod","panelPassword","groupGmail","groupMicrosoft" }) {
				StringAssert.Contains(body,name);
			}
			StringAssert.Contains(body,"Height+=shift;");
			//Every option is still there.
			string designer=File.ReadAllText(S0FDb.FindRepoFile(Path.Combine("OpenPmsDental","Forms","FormEmailAddressEdit.designer.cs")));
			foreach(string name in new[] { "radioPassword","radioGmail","radioMicrosoft","panelPassword","groupGmail","groupMicrosoft" }) {
				StringAssert.Contains(designer,"this.groupAuthentication.Controls.Add(this."+name+");");
			}
		}
	}
}
