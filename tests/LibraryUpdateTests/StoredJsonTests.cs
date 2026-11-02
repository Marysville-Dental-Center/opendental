// The JSON the program writes to the database stays byte-identical across the Newtonsoft.Json update (12.0.3 to 13.0.4): the eForm medication
// list (EFormField.ValueString) and its layout (ValueLabel), the patient list layout, the TSI transaction log (tsitranslog.TransJson, with
// TypeNameHandling.Auto) and the introspection overrides (preference IntrospectionItems). Every expected string below is what Newtonsoft.Json
// 12.0.3 wrote; rows stored by it read back and write out unchanged. The Middle Tier's JSON deep copy keeps strings, dates and tables.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OpenPmsDentalBusiness;

namespace LibraryUpdateTests {
	[TestClass]
	public class StoredJsonTests {
		///<summary>U+2028 LINE SEPARATOR, which JSON writers escape.</summary>
		private static readonly string LS=((char)0x2028).ToString();
		///<summary>Its JSON escape, as six characters.</summary>
		private const string JSON_LS="\\"+"u2028";
		///<summary>Two medications as EFormFiller and CtrlEFormFill store them: quotes, a line break, a tab, markup characters, an accented letter and
		///a line separator (U+2028) in the values.</summary>
		private const string JSON_MEDS=@"[{""MedName"":""Lisinopril 10 mg"",""StrengthFreq"":""1 daily""},{""MedName"":""Café \""Ibuprofen\"" <200mg> & more"","
			+@"""StrengthFreq"":""as needed\r\nmax 3/day\tor"+JSON_LS+@"less""}]";
		private const string JSON_MED_LIST_LAYOUT_DEFAULT=@"{""Title"":""Medication List"",""HeaderCol1"":""Medication"",""HeaderCol2"":""Strength/Freq"","
			+@"""StrDelete"":""Delete"",""StrAdd"":""Add"",""StrNone"":""None"",""WidthCol1"":0,""WidthCol2"":0,""IsCol2Visible"":true,""PrefillCol1"":true,"
			+@"""PrefillCol2"":true,""ImportCol1"":true,""ImportCol2Overwrite"":false,""ImportCol2OverwriteDate"":false,""ImportCol2Append"":false,"
			+@"""ImportCol2AppendDate"":false}";
		private const string JSON_TSI_TRANS=@"[{""KeyType"":0,""PriKey"":15,""ProcNum"":0,""PayNum"":0,""PatNum"":42,""Guarantor"":40,"
			+@"""TranDate"":""2026-10-31T00:00:00"",""TranAmt"":125.5},{""KeyType"":5,""PriKey"":16,""ProcNum"":77,""PayNum"":0,""PatNum"":42,"
			+@"""Guarantor"":40,""TranDate"":""2026-10-31T13:45:30.25"",""TranAmt"":-0.01},{""KeyType"":-1,""PriKey"":0,""ProcNum"":0,""PayNum"":9,"
			+@"""PatNum"":43,""Guarantor"":40,""TranDate"":""0001-01-01T00:00:00"",""TranAmt"":0.30000000000000004},{""KeyType"":1,""PriKey"":17,"
			+@"""ProcNum"":0,""PayNum"":0,""PatNum"":43,""Guarantor"":40,""TranDate"":""2026-12-31T23:59:59"",""TranAmt"":1234567.891}]";

		private static List<EFormMed> GetMeds() {
			return new List<EFormMed> {
				new EFormMed { MedName="Lisinopril 10 mg",StrengthFreq="1 daily" },
				new EFormMed { MedName="Café \"Ibuprofen\" <200mg> & more",StrengthFreq="as needed\r\nmax 3/day\tor"+LS+"less" },
			};
		}

		private static List<TsiTrans> GetTsiTrans() {
			return new List<TsiTrans> {
				new TsiTrans(15,TsiFKeyType.Adjustment,0,0,42,40,new DateTime(2026,10,31),125.5),
				new TsiTrans(16,(TsiFKeyType)5,77,0,42,40,new DateTime(2026,10,31,13,45,30,250),-0.01),
				new TsiTrans(0,TsiFKeyType.None,0,9,43,40,DateTime.MinValue,0.1+0.2),
				new TsiTrans(17,TsiFKeyType.Claimproc,0,0,43,40,new DateTime(2026,12,31,23,59,59),1234567.891),
			};
		}

		[TestMethod]
		public void EFormMedicationList_IsWrittenAsBefore() {
			//EFormFiller.FillFields and CtrlEFormFill write the list with JsonConvert's default settings.
			Assert.AreEqual(JSON_MEDS,JsonConvert.SerializeObject(GetMeds()));
			List<EFormMed> listMeds=JsonConvert.DeserializeObject<List<EFormMed>>(JSON_MEDS);
			Assert.AreEqual(2,listMeds.Count);
			Assert.AreEqual("Café \"Ibuprofen\" <200mg> & more",listMeds[1].MedName);
			Assert.AreEqual("as needed\r\nmax 3/day\tor"+LS+"less",listMeds[1].StrengthFreq);
			Assert.AreEqual(JSON_MEDS,JsonConvert.SerializeObject(listMeds),"A stored row reads back and writes out unchanged.");
		}

		[TestMethod]
		public void EFormMedicationListLayout_IsWrittenAsBefore() {
			//EForms and FrmEFormDefEdit write the layout into ValueLabel.
			Assert.AreEqual(JSON_MED_LIST_LAYOUT_DEFAULT,JsonConvert.SerializeObject(new EFormMedListLayout()));
			EFormMedListLayout eFormMedListLayout=new EFormMedListLayout() { Title="Current medications",WidthCol1=55,IsCol2Visible=false,
				ImportCol2AppendDate=true };
			string json=JsonConvert.SerializeObject(eFormMedListLayout);
			Assert.AreEqual(@"{""Title"":""Current medications"",""HeaderCol1"":""Medication"",""HeaderCol2"":""Strength/Freq"",""StrDelete"":""Delete"","
				+@"""StrAdd"":""Add"",""StrNone"":""None"",""WidthCol1"":55,""WidthCol2"":0,""IsCol2Visible"":false,""PrefillCol1"":true,""PrefillCol2"":true,"
				+@"""ImportCol1"":true,""ImportCol2Overwrite"":false,""ImportCol2OverwriteDate"":false,""ImportCol2Append"":false,""ImportCol2AppendDate"":true}",
				json);
			Assert.AreEqual(json,JsonConvert.SerializeObject(JsonConvert.DeserializeObject<EFormMedListLayout>(json)));
			//A row stored by an older version with fewer members still reads, with the defaults for the rest.
			EFormMedListLayout eFormMedListLayoutOld=JsonConvert.DeserializeObject<EFormMedListLayout>(@"{""Title"":""Meds"",""WidthCol1"":10}");
			Assert.AreEqual("Meds",eFormMedListLayoutOld.Title);
			Assert.AreEqual("Strength/Freq",eFormMedListLayoutOld.HeaderCol2);
			Assert.IsTrue(eFormMedListLayoutOld.PrefillCol1);
		}

		[TestMethod]
		public void EFormPatientListLayout_IsWrittenAsBefore() {
			Assert.AreEqual(@"{""Label"":""Subscriber""}",new EFormPatListLayout().Serialize());
			Assert.AreEqual(@"{""Label"":""Spouse é""}",new EFormPatListLayout() { Label="Spouse é" }.Serialize());
			Assert.AreEqual("Guarantor",EFormPatListLayout.Deserialize(@"{""Label"":""Guarantor"",""Other"":1}").Label);
			Assert.AreEqual("plain text label",EFormPatListLayout.Deserialize("plain text label").Label,"Rows that hold the label as text.");
		}

		[TestMethod]
		public void TsiTransactionLog_IsWrittenAsBefore() {
			TsiTransLog tsiTransLog=new TsiTransLog();
			tsiTransLog.DictTransByType=GetTsiTrans().ToDictionary(x => new Tuple<TsiFKeyType,long>(x.KeyType,x.PriKey));
			Assert.AreEqual(JSON_TSI_TRANS,tsiTransLog.TransJson);
			TsiTransLog tsiTransLogStored=new TsiTransLog() { TransJson=JSON_TSI_TRANS };
			Dictionary<Tuple<TsiFKeyType,long>,TsiTrans> dictTrans=tsiTransLogStored.DictTransByType;
			Assert.AreEqual(4,dictTrans.Count);
			TsiTrans tsiTrans=dictTrans[new Tuple<TsiFKeyType,long>((TsiFKeyType)5,16)];
			Assert.AreEqual(new DateTime(2026,10,31,13,45,30,250),tsiTrans.TranDate);
			Assert.AreEqual(DateTimeKind.Unspecified,tsiTrans.TranDate.Kind);
			Assert.AreEqual(-0.01,tsiTrans.TranAmt);
			Assert.AreEqual(77,tsiTrans.ProcNum);
			tsiTransLogStored.DictTransByType=dictTrans;
			Assert.AreEqual(JSON_TSI_TRANS,tsiTransLogStored.TransJson,"A stored row reads back and writes out unchanged.");
		}

		[TestMethod]
		public void IntrospectionOverrides_AreWrittenAsBefore() {
			//Introspection.SetOverride stores the dictionary indented in the IntrospectionItems preference.
			Dictionary<Introspection.IntrospectionEntity,string> dictOverrides=new Dictionary<Introspection.IntrospectionEntity,string>() {
				{ Introspection.IntrospectionEntity.DoseSpotURL,"https://dosespot.example.test/api" },
				{ Introspection.IntrospectionEntity.PayConnectRestURL,"https://payconnect.example.test/rest?a=1&b=2" },
			};
			string json=JsonConvert.SerializeObject(dictOverrides,Formatting.Indented);
			Assert.AreEqual("{\r\n  \"DoseSpotURL\": \"https://dosespot.example.test/api\",\r\n"
				+"  \"PayConnectRestURL\": \"https://payconnect.example.test/rest?a=1&b=2\"\r\n}",json);
			//The preference is read with an error handler that skips keys of a removed entity.
			JsonSerializerSettings settings=new JsonSerializerSettings { Error=Introspection.HandleDeserializationError };
			Dictionary<Introspection.IntrospectionEntity,string> dictRead=JsonConvert.DeserializeObject<Dictionary<Introspection.IntrospectionEntity,string>>(
				"{\"NoSuchEntity\":\"x\",\"DoseSpotURL\":\"https://dosespot.example.test/api\"}",settings);
			Assert.AreEqual(1,dictRead.Count);
			Assert.AreEqual("https://dosespot.example.test/api",dictRead[Introspection.IntrospectionEntity.DoseSpotURL]);
			Assert.AreEqual(json,JsonConvert.SerializeObject(JsonConvert.DeserializeObject<Dictionary<Introspection.IntrospectionEntity,string>>(json),
				Formatting.Indented));
		}

		///<summary>A Middle Tier method signature for DtoObject.ConstructArray.</summary>
		public static void MiddleTierSample(string text,List<EFormMed> listMeds,DateTime dateTime,DataTable table) {
		}

		[TestMethod]
		public void MiddleTierDeepCopy_KeepsStringsDatesAndTables() {
			DataTable table=new DataTable();
			table.Columns.Add("PatNum");
			table.Columns.Add("Note");
			table.Rows.Add("42","line 1\r\nline 2 \"quoted\" é");
			DateTime dateTimeLocal=new DateTime(2026,11,2,8,30,0,DateTimeKind.Local);
			MethodBase methodBase=typeof(StoredJsonTests).GetMethod(nameof(MiddleTierSample));
			DtoObject[] arrayDtoObjects=DtoObject.ConstructArray(methodBase,new object[] { "O'Brien \\ \"quote\"",GetMeds(),dateTimeLocal,table });
			Assert.AreEqual(4,arrayDtoObjects.Length);
			Assert.AreEqual("O'Brien \\ \"quote\"",arrayDtoObjects[0].Obj);
			List<EFormMed> listMeds=(List<EFormMed>)arrayDtoObjects[1].Obj;
			Assert.AreEqual(JSON_MEDS,JsonConvert.SerializeObject(listMeds));
			DateTime dateTimeCopied=(DateTime)arrayDtoObjects[2].Obj;
			Assert.AreEqual(dateTimeLocal,dateTimeCopied,"DateTime values pass as they are (value types are not copied).");
			DataTable tableCopied=(DataTable)arrayDtoObjects[3].Obj;
			Assert.AreEqual(1,tableCopied.Rows.Count);
			Assert.AreEqual("line 1\r\nline 2 \"quoted\" é",tableCopied.Rows[0]["Note"]);
		}
	}
}
