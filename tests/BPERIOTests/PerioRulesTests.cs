// Unit tests for the Perio Chart rules in OpenDentBusiness (PerioMeasures): attached gingiva, calculated rows, the Recession label, implants
// after removal and the birthdate line. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BPERIOTests {
	[TestClass]
	public class PerioRulesTests {
		#region Attached gingiva (v24_4-chart-perio-attached-gingiva)
		[TestMethod]
		public void AttGing_IsSequenceType8() {
			//Open Dental's published 24.4 database documentation: PerioSequenceType.AttGing = 8, calculated and never saved.
			Assert.AreEqual(8,(int)PerioSequenceType.AttGing);
		}

		[TestMethod]
		public void CalcAttachedGingiva_IsMgjMinusProbingDepth() {
			Assert.AreEqual(2,PerioMeasures.CalcAttachedGingiva(mgj:5,probingDepth:3));
			Assert.AreEqual(7,PerioMeasures.CalcAttachedGingiva(mgj:9,probingDepth:2));
			Assert.AreEqual(4,PerioMeasures.CalcAttachedGingiva(mgj:4,probingDepth:0));
		}

		[TestMethod]
		public void CalcAttachedGingiva_ZeroAndBelowShowAsCalculated() {
			Assert.AreEqual(0,PerioMeasures.CalcAttachedGingiva(mgj:3,probingDepth:3),"Zero means no attached gingiva.");
			Assert.AreEqual(-2,PerioMeasures.CalcAttachedGingiva(mgj:2,probingDepth:4),"Below zero is shown as calculated, not clamped.");
		}

		[TestMethod]
		public void CalcAttachedGingiva_BlankWhenEitherValueIsMissing() {
			Assert.IsNull(PerioMeasures.CalcAttachedGingiva(mgj:-1,probingDepth:3));
			Assert.IsNull(PerioMeasures.CalcAttachedGingiva(mgj:5,probingDepth:-1));
			Assert.IsNull(PerioMeasures.CalcAttachedGingiva(mgj:-1,probingDepth:-1));
		}

		[TestMethod]
		public void GetAttachedGingivaText_FollowsTheCells() {
			Assert.AreEqual("2",PerioMeasures.GetAttachedGingivaText("5","3"));
			Assert.AreEqual("-1",PerioMeasures.GetAttachedGingivaText("3","4"));
			Assert.AreEqual("0",PerioMeasures.GetAttachedGingivaText("3","3"));
			Assert.AreEqual("12",PerioMeasures.GetAttachedGingivaText("15","3"),"Two-digit values (entered with +10) work too.");
			Assert.AreEqual("",PerioMeasures.GetAttachedGingivaText("","3"));
			Assert.AreEqual("",PerioMeasures.GetAttachedGingivaText("5",""));
			Assert.AreEqual("",PerioMeasures.GetAttachedGingivaText(null,"3"));
			Assert.AreEqual("",PerioMeasures.GetAttachedGingivaText("5",null));
		}

		[TestMethod]
		public void CalculatedSequences_AreCalAndAttGingOnly() {
			foreach(PerioSequenceType perioSequenceType in Enum.GetValues(typeof(PerioSequenceType))) {
				bool isCalculated=perioSequenceType==PerioSequenceType.CAL || perioSequenceType==PerioSequenceType.AttGing;
				Assert.AreEqual(isCalculated,PerioMeasures.IsCalculatedSequence(perioSequenceType),perioSequenceType.ToString());
			}
		}
		#endregion Attached gingiva (v24_4-chart-perio-attached-gingiva)

		#region Recession term (v24_4-chart-perio-recession-term)
		[TestMethod]
		public void GingMarginTerm_OffIsGingMarg() {
			Assert.AreEqual("Ging Marg",PerioMeasures.GetGingMarginTerm(useRecessionTerm:false,hasHyperplasia:false));
			Assert.AreEqual("Ging Marg",PerioMeasures.GetGingMarginTerm(useRecessionTerm:false,hasHyperplasia:true));
		}

		[TestMethod]
		public void GingMarginTerm_OnIsRecession_UnlessTheExamHasHyperplasia() {
			Assert.AreEqual("Recession",PerioMeasures.GetGingMarginTerm(useRecessionTerm:true,hasHyperplasia:false));
			Assert.AreEqual("Ging Marg",PerioMeasures.GetGingMarginTerm(useRecessionTerm:true,hasHyperplasia:true));
		}

		[TestMethod]
		public void Hyperplasia_IsAStoredValueOver100() {
			//Gingival margins are stored as 0 to 19 (apical, i.e. recession) or 101 to 119 for coronal margins (hyperplasia, shown as +1 to +19).
			Assert.IsFalse(PerioMeasures.IsHyperplasia(0));
			Assert.IsFalse(PerioMeasures.IsHyperplasia(3));
			Assert.IsFalse(PerioMeasures.IsHyperplasia(19));
			Assert.IsFalse(PerioMeasures.IsHyperplasia(100));
			Assert.IsTrue(PerioMeasures.IsHyperplasia(101));
			Assert.IsTrue(PerioMeasures.IsHyperplasia(109));
			Assert.IsFalse(PerioMeasures.IsHyperplasia(-1),"-1 is no measurement.");
		}

		[TestMethod]
		public void HasHyperplasia_ChecksEveryCellAndIgnoresBlanks() {
			Assert.IsFalse(PerioMeasures.HasHyperplasia(new List<string>()));
			Assert.IsFalse(PerioMeasures.HasHyperplasia(new List<string> { null,"","2","0","11" }));
			Assert.IsTrue(PerioMeasures.HasHyperplasia(new List<string> { "2",null,"102","" }));
		}

		[TestMethod]
		public void RecessionPreferenceName_IsNotRegisteredInPrefName() {
			//The name is only the spec's proposal; RegistryTests keeps it out of PrefName until it is confirmed, so the option is stored by name.
			Assert.AreEqual("PerioUseRecessionTerm",PerioMeasures.PrefNameUseRecessionTerm);
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),PerioMeasures.PrefNameUseRecessionTerm));
		}
		#endregion Recession term (v24_4-chart-perio-recession-term)

		#region Implant marker (v24_4-chart-perio-implant-removed)
		private const long CODE_NUM_IMPLANT=1;
		private const long CODE_NUM_CROWN=2;

		private static ProcedureCode GetProcCode(long codeNum) {
			if(codeNum==CODE_NUM_IMPLANT) {
				return new ProcedureCode { CodeNum=CODE_NUM_IMPLANT,ProcCode="Z9001",PaintType=ToothPaintingType.Implant };
			}
			if(codeNum==CODE_NUM_CROWN) {
				return new ProcedureCode { CodeNum=CODE_NUM_CROWN,ProcCode="Z9002",PaintType=ToothPaintingType.CrownLight };
			}
			return new ProcedureCode();
		}

		private static Procedure MakeProc(string toothNum,long codeNum,ProcStat procStat,bool hideGraphics=false) {
			//Procedure's constructor reads a preference (DxIcdVersion) from the cache. These tests need no database, so skip the constructor.
			Procedure procedure=(Procedure)FormatterServices.GetUninitializedObject(typeof(Procedure));
			procedure.ToothNum=toothNum;
			procedure.CodeNum=codeNum;
			procedure.ProcStatus=procStat;
			procedure.HideGraphics=hideGraphics;
			return procedure;
		}

		[TestMethod]
		public void Implant_CompletedOrExisting_IsAnImplant() {
			foreach(ProcStat procStat in new[] { ProcStat.C,ProcStat.EC,ProcStat.EO }) {
				List<Procedure> listProcedures=new List<Procedure> { MakeProc("3",CODE_NUM_IMPLANT,procStat) };
				Assert.IsTrue(PerioMeasures.IsImplantTooth(3,listProcedures,GetProcCode),procStat.ToString());
			}
		}

		[TestMethod]
		public void Implant_PlannedOrOtherCodeOrOtherTooth_IsNotAnImplant() {
			Assert.IsFalse(PerioMeasures.IsImplantTooth(3,new List<Procedure> { MakeProc("3",CODE_NUM_IMPLANT,ProcStat.TP) },GetProcCode));
			Assert.IsFalse(PerioMeasures.IsImplantTooth(3,new List<Procedure> { MakeProc("3",CODE_NUM_CROWN,ProcStat.C) },GetProcCode));
			Assert.IsFalse(PerioMeasures.IsImplantTooth(3,new List<Procedure> { MakeProc("30",CODE_NUM_IMPLANT,ProcStat.C) },GetProcCode));
			Assert.IsFalse(PerioMeasures.IsImplantTooth(3,new List<Procedure>(),GetProcCode));
			Assert.IsFalse(PerioMeasures.IsImplantTooth(3,null,GetProcCode));
		}

		[TestMethod]
		public void Implant_WithHiddenGraphics_IsRemoved() {
			List<Procedure> listProcedures=new List<Procedure> { MakeProc("3",CODE_NUM_IMPLANT,ProcStat.C,hideGraphics:true) };
			Assert.IsFalse(PerioMeasures.IsImplantTooth(3,listProcedures,GetProcCode),"Hiding the implant's graphics marks the implant removed.");
			listProcedures=new List<Procedure> { MakeProc("3",CODE_NUM_IMPLANT,ProcStat.EO,hideGraphics:true) };
			Assert.IsFalse(PerioMeasures.IsImplantTooth(3,listProcedures,GetProcCode));
		}

		[TestMethod]
		public void Implant_NewerVisibleImplantAfterARemovedOne_IsAnImplant() {
			List<Procedure> listProcedures=new List<Procedure> {
				MakeProc("3",CODE_NUM_IMPLANT,ProcStat.EO,hideGraphics:true),//the removed implant
				MakeProc("3",CODE_NUM_IMPLANT,ProcStat.C),//the replacement
			};
			Assert.IsTrue(PerioMeasures.IsImplantTooth(3,listProcedures,GetProcCode));
		}

		[TestMethod]
		public void Implant_HiddenCrownDoesNotMatter() {
			List<Procedure> listProcedures=new List<Procedure> {
				MakeProc("3",CODE_NUM_IMPLANT,ProcStat.C),
				MakeProc("3",CODE_NUM_CROWN,ProcStat.C,hideGraphics:true),
			};
			Assert.IsTrue(PerioMeasures.IsImplantTooth(3,listProcedures,GetProcCode));
		}
		#endregion Implant marker (v24_4-chart-perio-implant-removed)

		#region Birthdate on the Graphical Perio Chart (v25_2-chart-perio-print-birthdate)
		[TestMethod]
		public void Birthdate_UsesTheShortDateOfTheCurrentCulture() {
			CultureInfo cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			try {
				DateTime birthdate=new DateTime(1985,3,14);
				Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
				Assert.AreEqual("3/14/1985",PerioMeasures.GetBirthdateForPrint(birthdate));
				Thread.CurrentThread.CurrentCulture=new CultureInfo("en-GB");
				Assert.AreEqual("14/03/1985",PerioMeasures.GetBirthdateForPrint(birthdate));
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureInfoOld;
			}
		}

		[TestMethod]
		public void Birthdate_BlankBefore1880() {
			Assert.AreEqual("",PerioMeasures.GetBirthdateForPrint(DateTime.MinValue),"No birthdate entered.");
			Assert.AreEqual("",PerioMeasures.GetBirthdateForPrint(new DateTime(1879,12,31)));
			Assert.AreNotEqual("",PerioMeasures.GetBirthdateForPrint(new DateTime(1880,1,1)));
		}
		#endregion Birthdate on the Graphical Perio Chart (v25_2-chart-perio-print-birthdate)
	}
}
