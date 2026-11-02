// The inventory windows built in memory and never shown: each one builds, and every control lies inside it. And the source rule that makes the
// Materials used panel reach every completion path: the program's windows change procedures only through Procedures.Insert and Procedures.Update.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;

namespace InventoryTests {
	[TestClass]
	public class WindowTests {
		///<summary>WinForms controls are created on a single-threaded apartment thread, as the program does.</summary>
		private static void RunSta(Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if(exception!=null) {
				throw new AssertFailedException("Failed on the STA thread: "+exception,exception);
			}
		}

		[TestMethod]
		public void InventoryWindows_BuildWithEveryControlInside() {
			List<Func<Form>> listFuncs=new List<Func<Form>> {
				() => new FormMaterialsUsed(),() => new FormInventorySetup(),() => new FormSupplyAdjust(),() => new FormSupplyCount(),
				() => new FormSupplyProcCodes(),() => new FormSupplyProcPendings(),() => new FormSupplyReceive(),() => new FormSupplyStock(),
				() => new FormSupplyUsageReport(),() => new FormSupplyEdit(),
			};
			RunSta(() => {
				foreach(Func<Form> func in listFuncs) {
					using Form form=func();
					foreach(Control control in form.Controls) {
						if(control is MenuStrip) {
							continue;
						}
						Assert.IsTrue(form.ClientRectangle.Contains(control.Bounds),form.Name+"."+control.Name+" at "+control.Bounds+" is outside "+form.ClientSize);
					}
					List<Control> listButtons=form.Controls.Cast<Control>().Where(x => x is OpenPmsDental.UI.Button).ToList();
					for(int i=0;i<listButtons.Count;i++) {
						for(int j=i+1;j<listButtons.Count;j++) {
							Assert.IsFalse(listButtons[i].Bounds.IntersectsWith(listButtons[j].Bounds),form.Name+": "+listButtons[i].Name+" overlaps "+listButtons[j].Name);
						}
					}
				}
			});
		}

		///<summary>MaterialsUsedL relies on InventoryTracking.ProcSetComplete, which Procedures.Insert and Procedures.Update raise. A window that
		///wrote procedurelog itself would complete procedures without the panel.</summary>
		[TestMethod]
		public void ProgramWindows_ChangeProceduresOnlyThroughTheBusinessLayer() {
			string dirProgram=Path.GetDirectoryName(InvDb.FindRepoFile(Path.Combine("OpenPmsDental","OpenPmsDental.csproj")));
			Regex regex=new Regex(@"ProcedureCrud\.(Insert|Update|Sync)\(|UPDATE procedurelog SET[^""]*ProcStatus",RegexOptions.IgnoreCase);
			List<string> listProblems=new List<string>();
			foreach(string path in Directory.GetFiles(dirProgram,"*.cs",SearchOption.AllDirectories)) {
				if(path.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar) || path.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar)) {
					continue;
				}
				string text=File.ReadAllText(path);
				if(regex.IsMatch(text)) {
					listProblems.Add(path.Substring(dirProgram.Length));
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			string sourceMain=File.ReadAllText(Path.Combine(dirProgram,"Main Modules","FormOpenDental.cs"));
			StringAssert.Contains(sourceMain,"MaterialsUsedL.Start(this);");
			string sourceProcedures=File.ReadAllText(InvDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","Data Interface","Procedures.cs")));
			Assert.AreEqual(4,Regex.Matches(sourceProcedures,@"InventoryTracking\.NotifyProcSetComplete\(").Count,"Insert and Update raise it, on both sides of the Middle Tier check.");
		}
	}
}
