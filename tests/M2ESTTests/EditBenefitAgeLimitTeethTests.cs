// Integration of M2-EST (teeth on age limitations, Open Dental 26.1) with B-INSH's redesigned Edit Benefit window (Open Dental 25.2): the check
// that names a value the benefit's type ignores, and the preview line, must treat teeth on an age limitation as used. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2ESTTests {
	[TestClass]
	public class EditBenefitAgeLimitTeethTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestMethod]
		public void IgnoredFieldCheck_AcceptsTeethOnAnAgeLimit() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Limitations,PlanNum=1,Quantity=18,QuantityQualifier=BenefitQuantity.AgeLimit,
				ToothRange="2,3,14,15" };
			Assert.AreEqual(0,BenefitEditRules.GetIgnoredFieldErrors(benefit,null).Count,"A new age limitation with teeth saves from Edit Benefit.");
		}

		[TestMethod]
		public void IgnoredFieldCheck_StillNamesTeethOnOtherLimitations() {
			foreach(BenefitQuantity benefitQuantity in new[] { BenefitQuantity.None,BenefitQuantity.NumberOfServices,BenefitQuantity.Years }) {
				Benefit benefit=new Benefit { BenefitType=InsBenefitType.Limitations,PlanNum=1,Quantity=2,QuantityQualifier=benefitQuantity,
					ToothRange="2,15" };
				List<BenefitValidationError> listErrors=BenefitEditRules.GetIgnoredFieldErrors(benefit,null);
				CollectionAssert.AreEqual(new[] { EnumBenefitField.Teeth },listErrors.Select(x => x.Field).ToArray(),benefitQuantity.ToString());
			}
			//The type alone still says limitations don't use teeth: the qualifier decides for one benefit (B-INSH's own rule, kept).
			Assert.IsFalse(BenefitEditRules.IsFieldUsed(InsBenefitType.Limitations,EnumBenefitField.Teeth));
		}

		[TestMethod]
		public void PreviewLine_ShowsTheTeethOfAnAgeLimit() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Limitations,Quantity=18,QuantityQualifier=BenefitQuantity.AgeLimit };
			Assert.AreEqual("Limitation: through age 18, teeth 2,15, Code Group: Sealants",
				BenefitEditRules.GetPreviewText(benefit,"","","Sealants","2,15",false));
			benefit=new Benefit { BenefitType=InsBenefitType.Limitations,Quantity=2,QuantityQualifier=BenefitQuantity.NumberOfServices,
				TimePeriod=BenefitTimePeriod.CalendarYear };
			Assert.AreEqual("Limitation: 2 per Calendar Year, Code Group: Sealants",BenefitEditRules.GetPreviewText(benefit,"","","Sealants","2,15",false),
				"Teeth typed in a disabled box don't show for a frequency limitation.");
		}
	}
}
