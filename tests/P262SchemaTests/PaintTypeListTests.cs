// P262-SCHEMA owner question 2 (answered on 2026-11-07): the paint type TextBelow (18) is hidden from the procedure code paint type lists until
// the tooth chart draws it; a procedure code that already has it keeps it. The rule without a database, and the two windows read from the sources.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262SchemaTests {
	[TestClass]
	public class PaintTypeListTests {
		[TestMethod]
		public void PaintTypes_TextBelowHidden_EveryOtherInNumberOrder() {
			List<ToothPaintingType> listPaintTypes=ProcedureCodes.GetPaintTypesForList();
			CollectionAssert.DoesNotContain(listPaintTypes,ToothPaintingType.TextBelow);
			List<ToothPaintingType> listOthers=Enum.GetValues(typeof(ToothPaintingType)).Cast<ToothPaintingType>()
				.Where(x => x!=ToothPaintingType.TextBelow).OrderBy(x => (int)x).ToList();
			CollectionAssert.AreEqual(listOthers,listPaintTypes);
			for(int i=0;i<listPaintTypes.Count;i++) {
				Assert.AreEqual(i,(int)listPaintTypes[i],"The new code window casts the list index to the paint type.");
			}
			CollectionAssert.AreEqual(listPaintTypes,ProcedureCodes.GetPaintTypesForList(ToothPaintingType.Text));
		}

		[TestMethod]
		public void PaintTypes_ACodeThatHasTextBelowKeepsIt() {
			List<ToothPaintingType> listPaintTypes=ProcedureCodes.GetPaintTypesForList(ToothPaintingType.TextBelow);
			Assert.AreEqual(ToothPaintingType.TextBelow,listPaintTypes.Last());
			Assert.AreEqual(Enum.GetValues(typeof(ToothPaintingType)).Length,listPaintTypes.Count);
		}

		[TestMethod]
		public void ProcCodeWindows_UseTheList() {
			string edit=Read("OpenPmsDental","Forms","FormProcCodeEdit.cs");
			StringAssert.Contains(edit,"listPaintType.Items.AddList(ProcedureCodes.GetPaintTypesForList(_procedureCode.PaintType),");
			Assert.IsFalse(edit.Contains("AddEnums<ToothPaintingType>()"));
			string codeNew=Read("OpenPmsDental","Forms","FormProcCodeNew.cs");
			StringAssert.Contains(codeNew,"List<ToothPaintingType> listPaintTypes=ProcedureCodes.GetPaintTypesForList();");
			Assert.IsFalse(codeNew.Contains("Enum.GetNames(typeof(ToothPaintingType))"));
		}

		private static string Read(params string[] arrayParts) {
			string path=Path.GetDirectoryName(typeof(PaintTypeListTests).Assembly.Location);
			while(path!=null && !(File.Exists(Path.Combine(path,"LICENSE")) && File.Exists(Path.Combine(path,"NOTICE")))) {
				path=Path.GetDirectoryName(path);
			}
			Assert.IsNotNull(path,"Repository root not found.");
			return File.ReadAllText(Path.Combine(new[] { path }.Concat(arrayParts).ToArray()));
		}
	}
}
