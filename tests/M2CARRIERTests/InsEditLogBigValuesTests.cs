// Insurance edit logs keep values longer than 255 characters (Open Dental 25.4): which column a value goes in, which one the log windows show,
// and the text column's size limit. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CARRIERTests {
	[TestClass]
	public class InsEditLogBigValuesTests {
		[TestMethod]
		public void ShortValuesStayInTheShortColumns() {
			InsEditLog insEditLog=new InsEditLog { OldValue=new string('o',255),NewValue="new" };
			InsEditLogBigValues.MoveLongValues(insEditLog);
			Assert.AreEqual(255,insEditLog.OldValue.Length);
			Assert.AreEqual("new",insEditLog.NewValue);
			Assert.AreEqual("",insEditLog.OldValueBig);
			Assert.AreEqual("",insEditLog.NewValueBig);
		}

		[TestMethod]
		public void LongValuesMoveToTheBigColumns() {
			string valueOld=new string('o',256);
			string valueNew=new string('n',5000);
			InsEditLog insEditLog=new InsEditLog { OldValue=valueOld,NewValue=valueNew };
			InsEditLogBigValues.MoveLongValues(insEditLog);
			Assert.AreEqual("",insEditLog.OldValue,"The short column is left empty.");
			Assert.AreEqual("",insEditLog.NewValue);
			Assert.AreEqual(valueOld,insEditLog.OldValueBig);
			Assert.AreEqual(valueNew,insEditLog.NewValueBig);
			Assert.AreEqual(valueOld,InsEditLogBigValues.GetOldValue(insEditLog));
			Assert.AreEqual(valueNew,InsEditLogBigValues.GetNewValue(insEditLog));
		}

		[TestMethod]
		public void EachSideIsDecidedOnItsOwn() {
			string valueNew=new string('n',300);
			InsEditLog insEditLog=new InsEditLog { OldValue="short note",NewValue=valueNew };
			InsEditLogBigValues.MoveLongValues(insEditLog);
			Assert.AreEqual("short note",insEditLog.OldValue);
			Assert.AreEqual("",insEditLog.OldValueBig);
			Assert.AreEqual("",insEditLog.NewValue);
			Assert.AreEqual(valueNew,insEditLog.NewValueBig);
			Assert.AreEqual("short note",InsEditLogBigValues.GetOldValue(insEditLog));
		}

		[TestMethod]
		public void PatientLevelLogFollowsTheSameRule() {
			string valueOld=new string('s',1000);
			InsEditPatLog insEditPatLog=new InsEditPatLog { OldValue=valueOld,NewValue="DELETED" };
			InsEditLogBigValues.MoveLongValues(insEditPatLog);
			Assert.AreEqual("",insEditPatLog.OldValue);
			Assert.AreEqual(valueOld,insEditPatLog.OldValueBig);
			Assert.AreEqual("DELETED",insEditPatLog.NewValue);
			Assert.AreEqual(valueOld,InsEditLogBigValues.GetOldValue(insEditPatLog));
			Assert.AreEqual("DELETED",InsEditLogBigValues.GetNewValue(insEditPatLog));
		}

		[TestMethod]
		public void NullsAreSafe() {
			InsEditLogBigValues.MoveLongValues((InsEditLog)null);
			InsEditLogBigValues.MoveLongValues((InsEditPatLog)null);
			InsEditLog insEditLog=new InsEditLog { OldValue=null,NewValue=null,OldValueBig=null,NewValueBig=null };
			InsEditLogBigValues.MoveLongValues(insEditLog);
			Assert.AreEqual("",insEditLog.OldValue);
			Assert.AreEqual("",insEditLog.NewValueBig);
			Assert.AreEqual("",InsEditLogBigValues.GetOldValue((InsEditLog)null));
		}

		[TestMethod]
		public void AValueAlreadyInTheBigColumnIsKept() {
			InsEditLog insEditLog=new InsEditLog { OldValue="",OldValueBig=new string('b',400),NewValue="x" };
			InsEditLogBigValues.MoveLongValues(insEditLog);
			Assert.AreEqual(400,insEditLog.OldValueBig.Length);
			Assert.AreEqual(400,InsEditLogBigValues.GetOldValue(insEditLog).Length);
		}

		[TestMethod]
		public void ValuesAreCutToWhatATextColumnHolds() {
			string valueAscii=new string('a',70000);
			Assert.AreEqual(InsEditLogBigValues.MaxTextBytes,InsEditLogBigValues.TruncateToText(valueAscii).Length);
			//Three bytes per character in UTF-8.
			string valueWide=new string('€',30000);
			string valueWideCut=InsEditLogBigValues.TruncateToText(valueWide);
			Assert.IsTrue(Encoding.UTF8.GetByteCount(valueWideCut)<=InsEditLogBigValues.MaxTextBytes);
			Assert.AreEqual(InsEditLogBigValues.MaxTextBytes/3,valueWideCut.Length);
			//A surrogate pair is never split.
			//65,532 one-byte characters and a 4-byte pair do not fit; the high surrogate alone would (it counts as 3 bytes), so it must be dropped.
			StringBuilder stringBuilder=new StringBuilder(new string('a',InsEditLogBigValues.MaxTextBytes-3));
			stringBuilder.Append("😀");
			string valueCut=InsEditLogBigValues.TruncateToText(stringBuilder.ToString());
			Assert.AreEqual(InsEditLogBigValues.MaxTextBytes-3,valueCut.Length);
			Assert.IsFalse(char.IsHighSurrogate(valueCut[valueCut.Length-1]));
			Assert.AreEqual("short",InsEditLogBigValues.TruncateToText("short"));
			InsEditLog insEditLog=new InsEditLog { NewValue=valueAscii };
			InsEditLogBigValues.MoveLongValues(insEditLog);
			Assert.AreEqual(InsEditLogBigValues.MaxTextBytes,insEditLog.NewValueBig.Length);
		}
	}
}
