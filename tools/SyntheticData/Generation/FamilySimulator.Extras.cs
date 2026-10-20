// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using SyntheticData.Infrastructure;

namespace SyntheticData.Generation {
	public partial class FamilySimulator {
		private string _familyAddress;
		private string _familyCity;
		private string _familyZip;
		private string _familyPhone;
		private static readonly DateTime _dateEFormsStarted=new DateTime(2024,11,15);
		private static readonly DateTime _dateEFormEnglishCopies=new DateTime(2025,7,1);
		private static readonly DateTime _dateTaskCategories=new DateTime(2025,6,1);
		private static readonly DateTime _dateBetterDiag=new DateTime(2025,2,1);

		private void AssignFamilyAddress() {
			_familyAddress=NameBank.Street(_rng);
			_familyCity=NameBank.Town(_rng);
			_familyZip=NameBank.Zip(_rng);
			_familyPhone=_rng.Chance(0.5) ? NameBank.Phone(_rng) : "";
		}

		private void WriteExtras() {
			foreach(Person person in _listMembers) {
				WriteRecall(person);
				WriteMedicalLists(person);
				WriteEForms(person);
				WriteConsentSheets(person);
				WriteChartLetters(person);
				WriteTasks(person);
				WriteNotesAndPopups(person);
				WriteReferrals(person);
			}
			WriteGuardians();
			if(_guarantor.Status!=OdValues.PatProspective && Chance(0.06)) {
				AddCreditCard(_guarantor,0,0);
			}
			if(_hasPrepayment) {
				AddCommlog(_guarantor,AsOf.AddDays(-1).AddHours(9),"Financial",OdValues.CommModePhone,OdValues.CommReceived,"Called about using the prepayment.");
			}
		}

		private void WriteRecall(Person person) {
			if(person.LastHygieneVisit==null || (person.Status!=OdValues.PatPatient && person.Status!=OdValues.PatInactive)) {
				return;
			}
			int interval=person.IsPerio ? 4<<16 : 6<<16;
			DateTime dateDue=person.NextHygieneDue??person.LastHygieneVisit.Value.AddMonths(person.IsPerio ? 4 : 6);
			bool isOverdue=dateDue<AsOf && person.DateTimeNextScheduled==null;
			_sink.Insert("recall",new Row {
				["RecallNum"]=_sink.NextId("recall"),["PatNum"]=person.PatNum,["DateDueCalc"]=dateDue,["DateDue"]=dateDue,
				["DatePrevious"]=person.LastHygieneVisit.Value,["RecallInterval"]=interval,
				["RecallStatus"]=isOverdue && _rng.Chance(0.4) ? _world.Def(OdValues.DefRecallUnschedStatus,"Left message to schedule") : 0L,
				["Note"]="",["IsDisabled"]=person.Status==OdValues.PatInactive,["DateTStamp"]=AsOf.AddHours(18),
				["RecallTypeNum"]=person.IsPerio ? _world.RecallTypePerio : _world.RecallTypeProphy,["DisableUntilBalance"]=0.0,
				["DateScheduled"]=person.DateTimeNextScheduled?.Date??_minDate,["Priority"]=0,["TimePatternOverride"]="",
			});
			if(person.LastBitewings.HasValue && _rng.Chance(0.5)) {
				_sink.Insert("recall",new Row {
					["RecallNum"]=_sink.NextId("recall"),["PatNum"]=person.PatNum,["DateDueCalc"]=person.LastBitewings.Value.AddYears(1),
					["DateDue"]=person.LastBitewings.Value.AddYears(1),["DatePrevious"]=person.LastBitewings.Value,["RecallInterval"]=1<<24,["Note"]="",
					["DateTStamp"]=AsOf.AddHours(18),["RecallTypeNum"]=_world.RecallTypeBitewing,["DisableUntilBalance"]=0.0,["TimePatternOverride"]="",
				});
			}
		}

		///<summary>Medications (including ones hidden in 25.1 that patients still take), allergies and problems.</summary>
		private void WriteMedicalLists(Person person) {
			if(person.Status==OdValues.PatNonPatient || person.Status==OdValues.PatProspective) {
				return;
			}
			DateTime dateStart=person.DateFirstVisit.Year>1900 ? person.DateFirstVisit : AsOf;
			if(person.AgeOn(AsOf)>=18 && Chance(0.3)) {
				int count=_rng.Between(1,3);
				HashSet<long> setUsed=new HashSet<long>();
				for(int i=0;i<count;i++) {
					(long medicationNum,string name,bool isHidden) medication=_rng.Pick(_world.ListMedications);
					if(_isShowcase && i==0) {
						medication=_world.ListMedications.Last();
					}
					if(!setUsed.Add(medication.medicationNum)) {
						continue;
					}
					_sink.Insert("medicationpat",new Row {
						["MedicationPatNum"]=_sink.NextId("medicationpat"),["PatNum"]=person.PatNum,["MedicationNum"]=medication.medicationNum,
						["PatNote"]=medication.isHidden ? "Patient still has this from an old prescription." : "",["DateTStamp"]=dateStart.AddHours(10),
						["DateStart"]=dateStart.AddDays(-_rng.Between(30,2000)),["DateStop"]=_minDate,["ProvNum"]=person.Dentist.ProvNum,
						["MedDescript"]="",["RxCui"]=0L,["ErxGuid"]="",["IsCpoe"]=false,
					});
				}
			}
			if(_rng.Chance(0.08)) {
				_sink.Insert("allergy",new Row {
					["AllergyNum"]=_sink.NextId("allergy"),["AllergyDefNum"]=_rng.Pick(_world.ListAllergyDefNums),["PatNum"]=person.PatNum,
					["Reaction"]=_rng.Pick(new[] { "Hives","Rash","Swelling" }),["StatusIsActive"]=true,["DateTStamp"]=dateStart.AddHours(10),
					["DateAdverseReaction"]=_minDate,["SnomedReaction"]="",
				});
			}
			if(person.AgeOn(AsOf)>=30 && _rng.Chance(0.15)) {
				_sink.Insert("disease",new Row {
					["DiseaseNum"]=_sink.NextId("disease"),["PatNum"]=person.PatNum,["DiseaseDefNum"]=_rng.Pick(_world.ListDiseaseDefNums),["PatNote"]="",
					["DateTStamp"]=dateStart.AddHours(10),["ProbStatus"]=0,["DateStart"]=dateStart.AddDays(-_rng.Between(100,3000)),["DateStop"]=_minDate,
					["SnomedProblemType"]="",["FunctionStatus"]=0,
				});
			}
		}

		///<summary>A pending insurance row (inspending, 25.2) written for this family, so a later form can pick it as Existing.</summary>
		private class PendingIns {
			public long InsPendingNum;
			public Person Subscriber;
			public DateTime DateCreated;
			public string CarrierName;
			public string GroupNum;
			public string GroupName;
			public string Employer;
			public string SubscriberId;
			public string Phone;
		}

		private readonly List<PendingIns> _listPendingIns=new List<PendingIns>();

		///<summary>The relationship of a family member to a subscriber in the same family (Spouse or Child), or -1 when this simple family
		///model cannot tell.</summary>
		private int RelationshipTo(Person person,Person subscriber) {
			if(person==subscriber) {
				return OdValues.RelatSelf;
			}
			bool isSpouseOfGuarantor(Person x) => !x.IsGuarantor && x.Position==OdValues.PosMarried;
			if((subscriber.IsGuarantor && isSpouseOfGuarantor(person)) || (person.IsGuarantor && isSpouseOfGuarantor(subscriber))) {
				return OdValues.RelatSpouse;
			}
			if((subscriber.IsGuarantor || isSpouseOfGuarantor(subscriber)) && !person.IsGuarantor && !isSpouseOfGuarantor(person)
				&& person.AgeOn(AsOf)<26)
			{
				return OdValues.RelatChild;
			}
			return -1;
		}

		///<summary>eForms filled for new patients since eForms reached the desktop (24.4): registration with an Ins Pick field (25.2) whose
		///New answer reveals a Patient List field (25.1) for the subscriber and the ins1 text fields, a medical history with the medication
		///list, English copies of translated labels (25.2), statuses including the legacy Imported value, and pending insurance (inspending,
		///25.2) from the Ins Pick answer. Value formats follow the published database docs; see README.md, "eForm value formats".</summary>
		private void WriteEForms(Person person) {
			if(person.DateFirstVisit<_dateEFormsStarted || person.DateFirstVisit>AsOf || person.Status==OdValues.PatNonPatient) {
				return;
			}
			if(!Chance(0.85)) {
				return;
			}
			DateTime dateShown=person.DateFirstVisit.AddDays(-_rng.Between(1,6)).AddHours(_rng.Between(8,20));
			bool isSpanish=_rng.Chance(0.2);
			bool hasEnglishCopies=dateShown>=_dateEFormEnglishCopies;
			int status=dateShown<new DateTime(2025,6,1) && _rng.Chance(0.25) ? OdValues.EFormStatusLegacyImported
				: (AsOf-dateShown).Days<10 && _rng.Chance(0.5) ? OdValues.EFormStatusShowInEClipboard : OdValues.EFormStatusFilled;
			if(_isShowcase && status==OdValues.EFormStatusShowInEClipboard) {
				status=OdValues.EFormStatusFilled;
			}
			//Ins Pick answer: Existing names the patient's own subscription ("InsSubNum:n") or a family member's pending row ("InsPendingNum:n");
			//New ("New") reveals the subscriber and ins1 fields; None is "".
			string insPick="";
			Person subscriber=null;
			int relationship=OdValues.RelatSelf;
			PendingIns pendingPicked=null;
			InsCoverage coverage=person.ListCoverages.OrderBy(x => x.Ordinal).FirstOrDefault();
			if(coverage!=null) {
				insPick=EFormJson.InsPickExistingSub(coverage.InsSubNum);
			}
			else {
				PendingIns pendingFamily=_listPendingIns.FirstOrDefault(x => x.DateCreated<dateShown && RelationshipTo(person,x.Subscriber)>0);
				if(pendingFamily!=null && _rng.Chance(0.8)) {
					pendingPicked=pendingFamily;
					insPick=EFormJson.InsPickExistingPending(pendingFamily.InsPendingNum);
					subscriber=pendingFamily.Subscriber;
					relationship=RelationshipTo(person,subscriber);
				}
				else if(person.AgeOn(AsOf)>=18 && Chance(0.35)) {
					insPick=EFormJson.InsPickNew;
					subscriber=person;
					if(person!=_guarantor && _guarantor.AgeOn(AsOf)>=18 && RelationshipTo(person,_guarantor)>0 && _rng.Chance(0.4)) {
						subscriber=_guarantor;
					}
					relationship=RelationshipTo(person,subscriber);
				}
			}
			bool isNew=insPick==EFormJson.InsPickNew;
			string carrierName=isNew ? NameBank.CarrierName(_rng) : "";
			string groupNum=isNew ? "G"+_rng.Between(100000,999999) : "";
			string subscriberId=isNew ? "SYN"+_rng.Between(100000000,999999999) : "";
			long eFormNum=_sink.NextId("eform");
			_sink.Insert("eform",new Row {
				["EFormNum"]=eFormNum,["FormType"]=OdValues.EFormPatientForm,["PatNum"]=person.PatNum,["DateTimeShown"]=dateShown,
				["Description"]="New Patient Registration",["DateTEdited"]=dateShown.AddMinutes(_rng.Between(5,25)),["MaxWidth"]=650,
				["EFormDefNum"]=_world.EFormDefRegistration,["Status"]=status,["RevID"]=3,["ShowLabelsBold"]=true,["SpaceBelowEachField"]=8,
				["SpaceToRightEachField"]=10,["SaveImageCategory"]=_world.Def(OdValues.DefImageCats,"Forms"),
			});
			foreach((long fieldDefNum,int fieldType,string dbLink,string label,string pickList,int itemOrder) field in _world.ListRegistrationFields) {
				string value="";
				switch(field.dbLink) {
					case "FName": value=person.FName; break;
					case "LName": value=person.LName; break;
					case "Birthdate": value=person.Birthdate.ToString("MM/dd/yyyy"); break;
					case "Address": value=_familyAddress; break;
					case "City": value=_familyCity; break;
					case "HmPhone": value=_familyPhone; break;
					case "Email": value=person.Email; break;
					case "Gender": value=person.Gender==OdValues.GenderFemale ? "F" : person.Gender==OdValues.GenderMale ? "M" : "U"; break;
					case "ins1CarrierName": value=carrierName; break;
					case "ins1GroupNum": value=groupNum; break;
					case "ins1SubscriberID": value=subscriberId; break;
				}
				if(field.fieldType==OdValues.EfCheckBox) {
					value=insPick!="" ? "X" : "";
				}
				else if(field.fieldType==OdValues.EfInsPick) {
					value=insPick;
				}
				else if(field.fieldType==OdValues.EfPatientList) {
					//The picked subscriber's PatNum, shown only when the Ins Pick answer is New.
					value=isNew ? subscriber.PatNum.ToString() : "";
				}
				else if(field.fieldType==OdValues.EfSigBox) {
					value=status==OdValues.EFormStatusShowInEClipboard ? "" : "SYNSIG:"+World.Sha256Hex("sig"+eFormNum).Substring(0,40);
				}
				WriteEFormField(eFormNum,person,field,value,isSpanish,hasEnglishCopies);
			}
			if((isNew || pendingPicked!=null) && status!=OdValues.EFormStatusShowInEClipboard) {
				//Import creates pending insurance, never plans. An Existing pending row is copied for this patient (family duplicates are expected).
				PendingIns pending=new PendingIns {
					InsPendingNum=_sink.NextId("inspending"),Subscriber=subscriber,DateCreated=dateShown.AddHours(1),
					CarrierName=isNew ? carrierName : pendingPicked.CarrierName,GroupNum=isNew ? groupNum : pendingPicked.GroupNum,
					GroupName=isNew ? carrierName.Split(' ')[0]+" Group" : pendingPicked.GroupName,Employer=isNew ? NameBank.Company(_rng) : pendingPicked.Employer,
					SubscriberId=isNew ? subscriberId : pendingPicked.SubscriberId,Phone=isNew ? NameBank.Phone(_rng) : pendingPicked.Phone,
				};
				_sink.Insert("inspending",new Row {
					["InsPendingNum"]=pending.InsPendingNum,["PatNum"]=person.PatNum,["PatNumSubscriber"]=subscriber.PatNum,["Ordinal"]=1,
					["Relationship"]=relationship,["GroupNum"]=pending.GroupNum,["GroupName"]=pending.GroupName,["Employer"]=pending.Employer,
					["SubscriberID"]=pending.SubscriberId,["Phone"]=pending.Phone,["CarrierName"]=pending.CarrierName,
				});
				if(isNew) {
					_listPendingIns.Add(pending);
				}
			}
			if(Chance(0.4)) {
				long eFormMedNum=_sink.NextId("eform");
				_sink.Insert("eform",new Row {
					["EFormNum"]=eFormMedNum,["FormType"]=OdValues.EFormMedicalHistory,["PatNum"]=person.PatNum,["DateTimeShown"]=dateShown.AddMinutes(30),
					["Description"]="Medical History",["DateTEdited"]=dateShown.AddMinutes(45),["MaxWidth"]=650,["EFormDefNum"]=_world.EFormDefMedical,
					["Status"]=status,["RevID"]=2,["ShowLabelsBold"]=false,["SpaceBelowEachField"]=8,["SpaceToRightEachField"]=10,
					["SaveImageCategory"]=_world.Def(OdValues.DefImageCats,"Forms"),
				});
				string lastVisitAnswer="";
				foreach((long fieldDefNum,int fieldType,string dbLink,string label,string pickList,int itemOrder) field in _world.ListMedicalFields) {
					string value="";
					if(field.fieldType==OdValues.EfMedicationList) {
						value=EFormJson.MedicationList(_world.ListMedications.Where(x => !x.isHidden).Take(_rng.Between(0,2)).Select(x => x.name));
					}
					else if(field.fieldType==OdValues.EfRadioButtons && field.label==PracticeGenerator.LastVisitLabel) {
						lastVisitAnswer=_rng.Pick(PracticeGenerator.ArrayLastVisitAnswers);
						value=lastVisitAnswer;
					}
					else if(field.fieldType==OdValues.EfRadioButtons) {
						value=_rng.Chance(0.1) ? "Yes" : "No";
					}
					else if(field.label==PracticeGenerator.LastVisitFollowUpLabel && lastVisitAnswer!=PracticeGenerator.ArrayLastVisitAnswers[0]
						&& _rng.Chance(0.3))
					{
						value="Had some sensitivity on the lower left after my last cleaning.";
					}
					else if(field.fieldType==OdValues.EfSigBox) {
						value="SYNSIG:"+World.Sha256Hex("sigmed"+eFormMedNum).Substring(0,40);
					}
					WriteEFormField(eFormMedNum,person,field,value,isSpanish,hasEnglishCopies);
				}
			}
			if(status==OdValues.EFormStatusFilled && _rng.Chance(0.2)) {
				AddSecurityLog(OdValues.PermEFormImport,_rng.Pick(_world.ListFrontDeskUsers),dateShown.AddHours(1),person.PatNum,eFormNum,"eForm imported: New Patient Registration");
			}
		}

		private static readonly Dictionary<string,string> _dictSpanish=new Dictionary<string,string> {
			["Welcome. Please tell us about yourself."]="Bienvenido. Cuéntenos sobre usted.",
			["First Name"]="Nombre",["Last Name"]="Apellido",["Birthdate"]="Fecha de nacimiento",["Street Address"]="Dirección",["City"]="Ciudad",
			["Home Phone"]="Teléfono de casa",["E-mail"]="Correo electrónico",["Gender"]="Género",["Do you have dental insurance?"]="¿Tiene seguro dental?",
			["Primary Insurance"]="Seguro principal",["Subscriber"]="Titular del seguro",["Insurance Company"]="Compañía de seguros",
			["Group Number"]="Número de grupo",["Subscriber ID"]="Número de identificación del titular",["Signature"]="Firma",
			["Medical History"]="Historial médico",["Current medications"]="Medicamentos actuales",["Allergic to penicillin?"]="¿Alergia a la penicilina?",
			["High blood pressure?"]="¿Presión arterial alta?",["Anything else we should know?"]="¿Algo más que debamos saber?",
		};

		///<summary>Spanish pick-list items. Items without a translation stay in English.</summary>
		private static readonly Dictionary<string,string> _dictSpanishPickItems=new Dictionary<string,string> {
			["Male"]="Masculino",["Female"]="Femenino",["Unknown"]="Desconocido",["Yes"]="Sí",["No"]="No",
			["None"]="Ninguno",["Existing"]="Existente",["New"]="Nuevo",
		};

		private void WriteEFormField(long eFormNum,Person person,(long fieldDefNum,int fieldType,string dbLink,string label,string pickList,int itemOrder) field,
			string value,bool isSpanish,bool hasEnglishCopies)
		{
			string label=isSpanish && _dictSpanish.TryGetValue(field.label,out string spanish) ? spanish : field.label;
			string pickListVis=field.pickList;
			if(isSpanish && field.pickList!="") {
				//Translated item by item, so the visible list keeps one item per PickListDb item.
				pickListVis=EFormJson.JoinPickList(EFormJson.SplitPickList(field.pickList)
					.Select(x => _dictSpanishPickItems.TryGetValue(x,out string item) ? item : x));
			}
			bool hasCondition=_world.DictEFormConditions.TryGetValue(field.fieldDefNum,out (string parent,string value) condition);
			_sink.Insert("eformfield",new Row {
				["EFormFieldNum"]=_sink.NextId("eformfield"),["EFormNum"]=eFormNum,["PatNum"]=person.PatNum,["FieldType"]=field.fieldType,
				["DbLink"]=field.dbLink,["ValueLabel"]=EFormJson.ValueLabel(field.fieldType,label),["ValueString"]=value,["ItemOrder"]=field.itemOrder,
				["PickListVis"]=pickListVis,["PickListDb"]=PracticeGenerator.PickListDb(field.fieldType,field.pickList),
				["IsHorizStacking"]=field.fieldType==OdValues.EfRadioButtons,["IsTextWrap"]=field.fieldType==OdValues.EfLabel,
				["Width"]=field.fieldType==OdValues.EfTextField ? 300 : 0,["FontScale"]=100,["IsRequired"]=field.dbLink=="FName" || field.dbLink=="LName",
				["ConditionalParent"]=hasCondition ? condition.parent : "",["ConditionalValue"]=hasCondition ? condition.value : "",["ReportableName"]="",
				["AutoImport"]=field.dbLink!="" && field.dbLink!="HmPhone",["PrefillFromGuar"]=field.dbLink=="Address" || field.dbLink=="City" || field.dbLink=="HmPhone",
				["ValueLabelEnglish"]=hasEnglishCopies || isSpanish ? EFormJson.ValueLabel(field.fieldType,field.label) : "",
				["PickListVisEnglish"]=(hasEnglishCopies || isSpanish) ? field.pickList : "",
			});
		}

		///<summary>Signed consent sheets for major treatment. The practice signature box records the signing provider user (25.2 UserSigned).</summary>
		private void WriteConsentSheets(Person person) {
			foreach(ProcRecord proc in _listProcs.Where(x => x.Patient==person && x.Status==OdValues.ProcComplete
				&& (x.Code.IsProsth || x.Code.Code.StartsWith("Z33") || x.Code.Code.StartsWith("Z72") || x.Code.Code=="Z6010")))
			{
				if(!Chance(0.4)) {
					continue;
				}
				ProviderInfo provider=_world.AllProviders.First(x => x.ProvNum==proc.ProvNum);
				DateTime dateSigned=proc.ProcDate.AddHours(_rng.Between(8,11)).AddMinutes(_rng.Next(60));
				long sheetNum=_sink.NextId("sheet");
				_sink.Insert("sheet",new Row {
					["SheetNum"]=sheetNum,["SheetType"]=OdValues.SheetConsent,["PatNum"]=person.PatNum,["DateTimeSheet"]=dateSigned,["FontSize"]=10f,
					["FontName"]="Arial",["Width"]=850,["Height"]=1100,["IsLandscape"]=false,["InternalNote"]="",["Description"]="Treatment Consent",
					["ShowInTerminal"]=0,["IsWebForm"]=false,["IsMultiPage"]=false,["IsDeleted"]=false,["SheetDefNum"]=_world.SheetDefConsent,
					["DocNum"]=0L,["ClinicNum"]=_world.ClinicNum,["DateTSheetEdited"]=dateSigned,["HasMobileLayout"]=false,["RevID"]=1,["WebFormSheetID"]=0L,
				});
				WriteSheetField(sheetNum,OdValues.SfStaticText,"","I agree to the treatment described below.",_minDate,0,50,60);
				WriteSheetField(sheetNum,OdValues.SfOutputText,"patient.nameFL",person.NameFL,_minDate,0,50,120);
				WriteSheetField(sheetNum,OdValues.SfCheckBox,"risksExplained","X",_minDate,0,50,160);
				WriteSheetField(sheetNum,OdValues.SfSigBox,"","SYNSIG:"+World.Sha256Hex("consent"+sheetNum).Substring(0,40),dateSigned,0,50,400);
				bool hasPracticeSignature=dateSigned>=new DateTime(2025,7,1) || _rng.Chance(0.5);
				WriteSheetField(sheetNum,OdValues.SfSigBoxPractice,"",hasPracticeSignature ? "SYNSIG:"+World.Sha256Hex("practice"+sheetNum).Substring(0,40) : "",
					hasPracticeSignature ? dateSigned.AddMinutes(2) : _minDate,hasPracticeSignature && dateSigned>=new DateTime(2025,7,1) ? provider.UserNum : 0,450,400);
			}
		}

		private void WriteSheetField(long sheetNum,int fieldType,string fieldName,string fieldValue,DateTime dateTimeSig,long userSigned,int x,int y) {
			_sink.Insert("sheetfield",new Row {
				["SheetFieldNum"]=_sink.NextId("sheetfield"),["SheetNum"]=sheetNum,["FieldType"]=fieldType,["FieldName"]=fieldName,["FieldValue"]=fieldValue,
				["FontSize"]=10f,["FontName"]="Arial",["FontIsBold"]=false,["XPos"]=x,["YPos"]=y,["Width"]=fieldType>=OdValues.SfSigBox ? 300 : 400,
				["Height"]=fieldType>=OdValues.SfSigBox ? 80 : 20,["GrowthBehavior"]=0,["RadioButtonValue"]="",["RadioButtonGroup"]="",["IsRequired"]=false,
				["TabOrder"]=0,["ReportableName"]="",["TextAlign"]=0,["DateTimeSig"]=dateTimeSig,["IsLocked"]=false,["TabOrderMobile"]=0,
				["UiLabelMobile"]="",["UiLabelMobileRadioButton"]="",["SheetFieldDefNum"]=0L,["CanElectronicallySign"]=fieldType==OdValues.SfSigBox || fieldType==OdValues.SfSigBoxPractice,
				["IsSigProvRestricted"]=fieldType==OdValues.SfSigBoxPractice,["UserSigned"]=userSigned,
			});
		}

		///<summary>Chart letters (24.4): Word documents listed in the Chart module, versioned by a hash of the file. A changed letter gets a new row.</summary>
		private void WriteChartLetters(Person person) {
			ProcRecord proc=_listProcs.FirstOrDefault(x => x.Patient==person && x.Status==OdValues.ProcComplete && x.ProcDate>=_dateChartLettersStarted);
			if(proc==null || !Chance(0.05)) {
				return;
			}
			ProviderInfo provider=_world.AllProviders.First(x => x.ProvNum==proc.ProvNum);
			string description=_rng.Pick(new[] { "Referral letter","Post-op instructions","Treatment summary" });
			int versions=_rng.Chance(0.35) ? 2 : 1;
			for(int version=1;version<=versions;version++) {
				long docNum=_sink.NextId("document");
				DateTime dateCreated=proc.ProcDate.AddDays(version-1).AddHours(14+version);
				_sink.Insert("document",new Row {
					["DocNum"]=docNum,["Description"]=description,["DateCreated"]=dateCreated,["DocCategory"]=0L,["PatNum"]=person.PatNum,
					["FileName"]="ChartLetter_"+person.PatNum+"_"+docNum+".docx",["ImgType"]=OdValues.ImgFile,["IsFlipped"]=false,["DegreesRotated"]=0f,
					["ToothNumbers"]="",["Note"]="",["SigIsTopaz"]=0,["Signature"]="",["MountItemNum"]=0L,["DateTStamp"]=dateCreated,["RawBase64"]="",
					["Thumbnail"]="",["ExternalGUID"]="",["ExternalSource"]="",["ProvNum"]=provider.ProvNum,["IsCropOld"]=0,["OcrResponseData"]="",
					["ChartLetterStatus"]=1,["UserNum"]=provider.UserNum,["ChartLetterHash"]=World.Sha256Hex("chartletter:"+person.PatNum+":"+description+":"+version),
				});
			}
		}

		///<summary>Radiograph documents for x-ray procedures, some signed (24.4 imaging signatures) and some with drawings, including
		///BetterDiagnostics layers (24.4/25.1; the integration is cut but the rows must survive).</summary>
		private void AddImagesForVisit(Person person,List<ProcRecord> listProcs,DateTime date) {
			foreach(ProcRecord proc in listProcs.Where(x => x.Code.Code=="Z0210" || x.Code.Code=="Z0272" || x.Code.Code=="Z0274" || x.Code.Code=="Z0330")) {
				long docNum=_sink.NextId("document");
				bool isSigned=_rng.Chance(0.1);
				DateTime dateCreated=date.AddHours(_rng.Between(8,16)).AddMinutes(_rng.Next(60));
				ProviderInfo provider=_world.AllProviders.First(x => x.ProvNum==proc.ProvNum);
				_sink.Insert("document",new Row {
					["DocNum"]=docNum,["Description"]=proc.Code.Descript,["DateCreated"]=dateCreated,["DocCategory"]=_world.Def(OdValues.DefImageCats,"X-Rays"),
					["PatNum"]=person.PatNum,["FileName"]=person.LName+person.FName+docNum+".jpg",["ImgType"]=OdValues.ImgRadiograph,["DegreesRotated"]=0f,
					["ToothNumbers"]="",["Note"]="",["SigIsTopaz"]=0,["Signature"]=isSigned ? "SYNSIG:"+World.Sha256Hex("doc"+docNum).Substring(0,40) : "",
					["CropW"]=0,["CropH"]=0,["WindowingMin"]=0,["WindowingMax"]=255,["DateTStamp"]=dateCreated,["RawBase64"]="",["Thumbnail"]="",
					["ExternalGUID"]="",["ExternalSource"]="",["ProvNum"]=isSigned ? provider.ProvNum : 0,["OcrResponseData"]="",["ImageCaptureType"]=0,
					["PrintHeading"]=false,["ChartLetterStatus"]=0,["UserNum"]=0L,["ChartLetterHash"]="",
				});
				if(isSigned) {
					AddSecurityLog(OdValues.PermImageSignatureCreate,provider.UserNum,dateCreated.AddMinutes(5),person.PatNum,docNum,"Signed image "+docNum);
				}
				if(Chance(0.2)) {
					int vendor=date>=_dateBetterDiag && Chance(0.3) ? OdValues.AnnotBetterDiagnostics : _rng.Chance(0.3) ? OdValues.AnnotPearl : OdValues.AnnotOpenDental;
					int drawings=_rng.Between(1,3);
					for(int i=0;i<drawings;i++) {
						_sink.Insert("imagedraw",new Row {
							["ImageDrawNum"]=_sink.NextId("imagedraw"),["DocNum"]=docNum,["MountNum"]=0L,["ColorDraw"]=unchecked((int)0xFFFF0000),
							["ColorBack"]=0,["DrawingSegment"]=_rng.Between(10,200)+","+_rng.Between(10,200)+";"+_rng.Between(10,200)+","+_rng.Between(10,200),
							["DrawText"]=vendor==OdValues.AnnotOpenDental ? "Watch #"+_rng.Pick(new[] { "3","14","19","30" }) : "",["FontSize"]=8f,
							["DrawType"]=vendor==OdValues.AnnotOpenDental ? 0 : 2,["ImageAnnotVendor"]=vendor,["Details"]="",
							["PearlLayer"]=vendor==OdValues.AnnotPearl ? _rng.Between(1,6) : 0,
							["BetterDiagLayer"]=vendor==OdValues.AnnotBetterDiagnostics ? _rng.Pick(new[] { 1,2,3,5,8,13,14,15,18 }) : 0,
						});
					}
				}
			}
		}

		///<summary>Patient tasks with task categories (25.2), history rows and notes. A few rows carry a triage position (HQ-only column in
		///25.3) so the column is proven to survive.</summary>
		private void WriteTasks(Person person) {
			if(person.Status==OdValues.PatDeleted || !_rng.Chance(0.05) && !(_isShowcase && person.IsGuarantor)) {
				return;
			}
			long taskNum=_sink.NextId("task");
			DateTime dateEntry=AsOf.AddDays(-_rng.Between(1,(AsOf-_world.Settings.HistoryStart).Days)).Date.AddHours(_rng.Between(8,17));
			if(_isShowcase) {
				dateEntry=AsOf.AddDays(-20).Date.AddHours(10);
			}
			bool isDone=_rng.Chance(0.6);
			long userNum=_rng.Pick(_world.ListFrontDeskUsers);
			string categoryName=_rng.Pick(new[] { "Billing","Insurance Follow-up","Clinical","Front Desk" });
			long category=dateEntry>=_dateTaskCategories ? _world.Def(OdValues.DefTaskCategories,categoryName) : 0;
			string descript=categoryName=="Billing" ? "Call about the balance on the account"
				: categoryName=="Insurance Follow-up" ? "Check the status of the outstanding claim"
				: categoryName=="Clinical" ? "Doctor to review x-rays and call the patient" : "Update the patient's contact details";
			int triagePosition=taskNum%25==0 || _isShowcase ? _rng.Between(1,9) : 0;
			Row row=new Row {
				["TaskNum"]=taskNum,["TaskListNum"]=_rng.Pick(_world.ListTaskListNums),["DateTask"]=_minDate,["KeyNum"]=person.PatNum,["Descript"]=descript,
				["TaskStatus"]=isDone ? OdValues.TaskDone : OdValues.TaskNew,["IsRepeating"]=false,["DateType"]=0,["FromNum"]=0L,["ObjectType"]=OdValues.TaskObjPatient,
				["DateTimeEntry"]=dateEntry,["UserNum"]=userNum,["DateTimeFinished"]=isDone ? dateEntry.AddDays(_rng.Between(0,10)).AddHours(2) : _minDate,
				["PriorityDefNum"]=_world.Def(OdValues.DefTaskPriorities,_rng.Chance(0.8) ? "Normal" : "High"),["ReminderGroupId"]="",["ReminderType"]=0,
				["ReminderFrequency"]=0,["DateTimeOriginal"]=dateEntry,["SecDateTEdit"]=dateEntry.AddHours(isDone ? 50 : 1),["DescriptOverride"]="",
				["IsReadOnly"]=false,["Category"]=category,["TriagePosition"]=triagePosition,
			};
			_sink.Insert("task",row);
			Row hist=new Row();
			foreach(KeyValuePair<string,object> pair in row) {
				hist[pair.Key]=pair.Value;
			}
			hist["TaskHistNum"]=_sink.NextId("taskhist");
			hist["UserNumHist"]=userNum;
			hist["DateTStamp"]=dateEntry.AddMinutes(30);
			hist["IsNoteChange"]=false;
			hist["TaskStatus"]=OdValues.TaskNew;
			hist["DateTimeFinished"]=_minDate;
			_sink.Insert("taskhist",hist);
			if(_rng.Chance(0.5)) {
				_sink.Insert("tasknote",new Row { ["TaskNoteNum"]=_sink.NextId("tasknote"),["TaskNum"]=taskNum,["UserNum"]=userNum,
					["DateTimeNote"]=dateEntry.AddHours(3),["Note"]=isDone ? "Done. Patient informed." : "Left a voicemail." });
			}
			if(!isDone) {
				_sink.Insert("taskunread",new Row { ["TaskUnreadNum"]=_sink.NextId("taskunread"),["TaskNum"]=taskNum,["UserNum"]=_rng.Pick(_world.ListFrontDeskUsers) });
			}
			if(_rng.Chance(0.2)) {
				_sink.Insert("tasksubscription",new Row { ["TaskSubscriptionNum"]=_sink.NextId("tasksubscription"),["UserNum"]=userNum,["TaskListNum"]=0L,["TaskNum"]=taskNum });
			}
			if(_rng.Chance(0.2)) {
				AddSecurityLog(OdValues.PermPatientEdit,userNum,dateEntry,person.PatNum,taskNum,"Task created: "+descript);
			}
		}

		private void WriteNotesAndPopups(Person person) {
			if(person.Status==OdValues.PatDeleted) {
				return;
			}
			if(_rng.Chance(0.1)) {
				_sink.Insert("patientnote",new Row {
					["PatNum"]=person.PatNum,["FamFinancial"]=person.IsGuarantor && _payerKind!=PayerKind.Prompt ? "Agreed to pay $50 a month." : "",
					["ApptPhone"]="",["Medical"]=_rng.Chance(0.5) ? "Prefers numbing gel before injections." : "",["Service"]="",["MedicalComp"]="",
					["Treatment"]="",["ICEName"]=NameBank.GivenName(_rng)+" "+person.LName,["ICEPhone"]=NameBank.Phone(_rng),
					["SecDateTEntry"]=AsOf.AddDays(-_rng.Between(1,400)).AddHours(10),["SecDateTEdit"]=AsOf.AddHours(9),["Consent"]=0,["UserNumOrthoLocked"]=0L,["Pronoun"]=0,
				});
			}
			if(_rng.Chance(0.01)) {
				_sink.Insert("popup",new Row {
					["PopupNum"]=_sink.NextId("popup"),["PatNum"]=person.PatNum,["Description"]="Prefers afternoon appointments.",["IsDisabled"]=false,
					["PopupLevel"]=0,["UserNum"]=_rng.Pick(_world.ListFrontDeskUsers),["DateTimeEntry"]=AsOf.AddDays(-_rng.Between(1,300)).AddHours(11),
					["IsArchived"]=false,["PopupNumArchive"]=0L,["DateTimeDisabled"]=_minDate,
				});
			}
			if(person.ListCoverages.Count>0 && _rng.Chance(0.05)) {
				AddCommlog(person,AsOf.AddDays(-_rng.Between(1,300)).Date.AddHours(13),"Insurance",OdValues.CommModePhone,OdValues.CommSent,
					"Called the carrier to check remaining benefits.");
			}
		}

		private void WriteReferrals(Person person) {
			foreach(ProcRecord proc in _listProcs.Where(x => x.Patient==person && (x.Code.Code=="Z7210" || x.Code.Code=="Z6010") && x.Status!=OdValues.ProcDeleted)) {
				if(!_rng.Chance(0.3)) {
					continue;
				}
				DateTime refDate=proc.DateTP.Year>1900 ? proc.DateTP : proc.ProcDate;
				_sink.Insert("refattach",new Row {
					["RefAttachNum"]=_sink.NextId("refattach"),["ReferralNum"]=_world.ListReferralNums[0],["PatNum"]=person.PatNum,["ItemOrder"]=0,
					["RefDate"]=refDate,["RefType"]=0,["RefToStatus"]=proc.Status==OdValues.ProcComplete ? 3 : 0,["Note"]="",["IsTransitionOfCare"]=false,
					["ProcNum"]=proc.ProcNum,["DateProcComplete"]=proc.Status==OdValues.ProcComplete ? proc.ProcDate : _minDate,["ProvNum"]=person.Dentist.ProvNum,
					["DateTStamp"]=refDate.AddHours(12),
				});
			}
		}

		///<summary>Guardians of children, using the 25.1 numbering (Mother 1, Father 6), so a second renumbering would be detected.</summary>
		private void WriteGuardians() {
			List<Person> listAdults=_listMembers.Where(x => x.Position!=OdValues.PosChild).ToList();
			foreach(Person child in _listMembers.Where(x => x.Position==OdValues.PosChild && x.AgeOn(AsOf)<18)) {
				foreach(Person adult in listAdults) {
					int relationship=adult.Gender==OdValues.GenderFemale ? OdValues.GuardianMother : adult.Gender==OdValues.GenderMale ? OdValues.GuardianFather : 13;
					_sink.Insert("guardian",new Row { ["GuardianNum"]=_sink.NextId("guardian"),["PatNumChild"]=child.PatNum,["PatNumGuardian"]=adult.PatNum,
						["Relationship"]=relationship,["IsGuardian"]=true });
				}
				if(_rng.Chance(0.05)) {
					//A sitter on file: value 23 since 25.1 (it was 6 before).
					_sink.Insert("guardian",new Row { ["GuardianNum"]=_sink.NextId("guardian"),["PatNumChild"]=child.PatNum,["PatNumGuardian"]=_guarantor.PatNum,
						["Relationship"]=OdValues.GuardianSitter,["IsGuardian"]=false });
				}
			}
		}

		///<summary>Card on file with the 25.1 card-holder name. Masked numbers only; no tokens for any processor.</summary>
		private void AddCreditCard(Person person,long payPlanNum,decimal chargeAmt) {
			DateTime dateStart=AsOf.AddDays(-_rng.Between(10,400));
			_sink.Insert("creditcard",new Row {
				["CreditCardNum"]=_sink.NextId("creditcard"),["PatNum"]=person.PatNum,["Address"]=_familyAddress,["Zip"]=_familyZip,["XChargeToken"]="",
				["CCNumberMasked"]="XXXXXXXXXXXX"+_rng.Between(1000,9999),["CCExpiration"]=new DateTime(AsOf.Year+_rng.Between(1,4),_rng.Between(1,12),1),
				["ItemOrder"]=0,["ChargeAmt"]=chargeAmt,["DateStart"]=payPlanNum!=0 ? dateStart : _minDate,["DateStop"]=_minDate,["Note"]="",
				["PayPlanNum"]=payPlanNum,["PayConnectToken"]="",["PayConnectTokenExp"]=_minDate,["Procedures"]="",["CCSource"]=0,["ClinicNum"]=_world.ClinicNum,
				["ExcludeProcSync"]=false,["PaySimpleToken"]="",["ChargeFrequency"]="",["CanChargeWhenNoBal"]=false,
				["PaymentType"]=_world.Def(OdValues.DefPaymentTypes,"Credit Card"),["IsRecurringActive"]=payPlanNum!=0,["Nickname"]="Card on file",
				["CardHolderName"]=person.NameFL,
			});
		}

		private void AddCommlog(Person person,DateTime dateTime,string typeName,int mode,int sentOrReceived,string note) {
			_sink.Insert("commlog",new Row {
				["CommlogNum"]=_sink.NextId("commlog"),["PatNum"]=person.PatNum,["CommDateTime"]=dateTime,["CommType"]=_world.Def(OdValues.DefCommLogTypes,typeName),
				["Note"]=note,["Mode_"]=mode,["SentOrReceived"]=sentOrReceived,["UserNum"]=_rng.Pick(_world.ListFrontDeskUsers),["Signature"]="",
				["SigIsTopaz"]=false,["DateTStamp"]=dateTime,["DateTimeEnd"]=_minDate,["CommSource"]=0,["ProgramNum"]=0L,["DateTEntry"]=dateTime,
				["ReferralNum"]=0L,["CommReferralBehavior"]=0,
			});
		}

		private void AddSecurityLog(int permType,long userNum,DateTime dateTime,long patNum,long fKey,string text) {
			_sink.Insert("securitylog",new Row {
				["SecurityLogNum"]=_sink.NextId("securitylog"),["PermType"]=permType,["UserNum"]=userNum,["LogDateTime"]=dateTime,["LogText"]=text,
				["PatNum"]=patNum,["CompName"]="SYN-WS0"+(1+(int)(fKey%4)),["FKey"]=fKey,["LogSource"]=0,["DefNum"]=0L,["DefNumError"]=0L,
				["DateTPrevious"]=_minDate,
			});
		}
	}
}
