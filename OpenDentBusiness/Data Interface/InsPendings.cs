using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Pending insurance (table inspending, Open Dental 25.2): insurance details a patient entered, held until staff turn them into real insplan,
	///inssub and patplan rows. Rows are made by eForm import (the Ins Pick field). Rows that Open Dental's API or eClipboard made in an adopted
	///database are handled the same way, so nothing here assumes an eForm made the row.</summary>
	public class InsPendings {
		#region Get Methods
		public static InsPending GetOne(long insPendingNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<InsPending>(MethodBase.GetCurrentMethod(),insPendingNum);
			}
			return Crud.InsPendingCrud.SelectOne(insPendingNum);
		}

		///<summary>The patient's pending insurance, in ordinal order.</summary>
		public static List<InsPending> GetForPat(long patNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<InsPending>>(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="SELECT * FROM inspending WHERE PatNum="+POut.Long(patNum)+" ORDER BY Ordinal,InsPendingNum";
			return Crud.InsPendingCrud.SelectMany(command);
		}

		///<summary>Pending insurance of any of these patients, in patient and ordinal order.</summary>
		public static List<InsPending> GetForPats(List<long> listPatNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<InsPending>>(MethodBase.GetCurrentMethod(),listPatNums);
			}
			if(listPatNums.IsNullOrEmpty()) {
				return new List<InsPending>();
			}
			string command="SELECT * FROM inspending WHERE PatNum IN("+string.Join(",",listPatNums.Select(x => POut.Long(x)))+") "
				+"ORDER BY PatNum,Ordinal,InsPendingNum";
			return Crud.InsPendingCrud.SelectMany(command);
		}

		///<summary>Rows whose patient or subscriber no longer exists, or is marked deleted. Used by Database Maintenance.</summary>
		public static List<InsPending> GetOrphans() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<InsPending>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT inspending.* FROM inspending "
				+"LEFT JOIN patient pat ON pat.PatNum=inspending.PatNum "
				+"LEFT JOIN patient sub ON sub.PatNum=inspending.PatNumSubscriber "
				+"WHERE pat.PatNum IS NULL OR sub.PatNum IS NULL "
				+"OR pat.PatStatus="+POut.Int((int)PatientStatus.Deleted)+" OR sub.PatStatus="+POut.Int((int)PatientStatus.Deleted);
			return Crud.InsPendingCrud.SelectMany(command);
		}
		#endregion Get Methods

		#region Modification Methods
		///<summary>Stores the subscriber ID without dashes or spaces.</summary>
		public static long Insert(InsPending insPending) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				insPending.InsPendingNum=Meth.GetLong(MethodBase.GetCurrentMethod(),insPending);
				return insPending.InsPendingNum;
			}
			Normalize(insPending);
			return Crud.InsPendingCrud.Insert(insPending);
		}

		public static void Update(InsPending insPending) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),insPending);
				return;
			}
			Normalize(insPending);
			Crud.InsPendingCrud.Update(insPending);
		}

		public static void Delete(long insPendingNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),insPendingNum);
				return;
			}
			Crud.InsPendingCrud.Delete(insPendingNum);
		}

		///<summary>Deletes the pending insurance of a patient (not rows where the patient is only the subscriber). Returns the number deleted.</summary>
		public static long DeleteForPatient(long patNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="DELETE FROM inspending WHERE PatNum="+POut.Long(patNum);
			return Db.NonQ(command);
		}

		///<summary>Patient merge moves inspending.PatNum and inspending.PatNumSubscriber from patFrom to patTo with the other patient columns
		///(Patients.StringArrayPatNumForeignKeys). Afterwards a row whose patient and subscriber became the same person must be Self; this sets that.
		///Returns the number of rows changed.</summary>
		public static long FixRelationshipAfterMerge(long patTo) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),patTo);
			}
			string command="UPDATE inspending SET Relationship="+POut.Int((int)Relat.Self)+" WHERE PatNum="+POut.Long(patTo)
				+" AND PatNumSubscriber="+POut.Long(patTo)+" AND Relationship!="+POut.Int((int)Relat.Self);
			return Db.NonQ(command);
		}
		#endregion Modification Methods

		#region Misc Methods
		///<summary>Trims the text fields, removes dashes and spaces from the subscriber ID, and replaces nulls with empty strings.</summary>
		public static void Normalize(InsPending insPending) {
			Meth.NoCheckMiddleTierRole();
			insPending.SubscriberID=EFormInsPick.NormalizeSubscriberID(insPending.SubscriberID);
			insPending.CarrierName=(insPending.CarrierName??"").Trim();
			insPending.Phone=(insPending.Phone??"").Trim();
			insPending.GroupName=(insPending.GroupName??"").Trim();
			insPending.GroupNum=(insPending.GroupNum??"").Trim();
			insPending.Employer=(insPending.Employer??"").Trim();
		}

		///<summary>Returns an error message, or empty string if the row may be saved. The patient and subscriber are required, the subscriber ID may not be
		///blank, and the relationship is Self exactly when the patient is the subscriber.</summary>
		public static string Validate(InsPending insPending) {
			Meth.NoCheckMiddleTierRole();
			if(insPending.PatNum==0) {
				return Lans.g("InsPendings","A patient is required.");
			}
			if(insPending.PatNumSubscriber==0) {
				return Lans.g("InsPendings","A subscriber is required.");
			}
			if(EFormInsPick.NormalizeSubscriberID(insPending.SubscriberID)=="") {
				return Lans.g("InsPendings","The subscriber ID is blank.");
			}
			if(insPending.PatNum==insPending.PatNumSubscriber && insPending.Relationship!=Relat.Self) {
				return Lans.g("InsPendings","The patient is the subscriber, so the relationship must be Self.");
			}
			if(insPending.PatNum!=insPending.PatNumSubscriber && insPending.Relationship==Relat.Self) {
				return Lans.g("InsPendings","The relationship is Self, so the subscriber must be the patient.");
			}
			return "";
		}

		///<summary>True if the two rows describe the same insurance for the same patient: same subscriber, subscriber ID and carrier name (ignoring case,
		///dashes and spaces) and the same ordinal.</summary>
		public static bool IsSameForPatient(InsPending insPending1,InsPending insPending2) {
			Meth.NoCheckMiddleTierRole();
			return insPending1.PatNum==insPending2.PatNum
				&& insPending1.Ordinal==insPending2.Ordinal
				&& IsSameInsurance(insPending1,insPending2);
		}

		///<summary>True if the rows name the same subscriber, subscriber ID and carrier name (ignoring case, dashes and spaces), whoever the patient is.</summary>
		public static bool IsSameInsurance(InsPending insPending1,InsPending insPending2) {
			Meth.NoCheckMiddleTierRole();
			return insPending1.PatNumSubscriber==insPending2.PatNumSubscriber
				&& string.Equals(EFormInsPick.NormalizeSubscriberID(insPending1.SubscriberID),EFormInsPick.NormalizeSubscriberID(insPending2.SubscriberID),
					StringComparison.OrdinalIgnoreCase)
				&& string.Equals((insPending1.CarrierName??"").Trim(),(insPending2.CarrierName??"").Trim(),StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Other family members' pending rows for the same insurance (same subscriber, subscriber ID and carrier name). Conversion offers to convert
		///these too, reusing the same subscription.</summary>
		public static List<InsPending> GetFamilyDuplicates(InsPending insPending,List<InsPending> listInsPendingsFamily) {
			Meth.NoCheckMiddleTierRole();
			return listInsPendingsFamily.FindAll(x => x.InsPendingNum!=insPending.InsPendingNum && x.PatNum!=insPending.PatNum
				&& IsSameInsurance(x,insPending));
		}

		///<summary>A pending row filled from an existing subscription, so that conversion is one click. Not inserted.</summary>
		public static InsPending CreateFromInsSub(long patNum,InsSub insSub,Relat relationship,byte ordinal) {
			Meth.NoCheckMiddleTierRole();
			InsPending insPending=new InsPending();
			insPending.PatNum=patNum;
			insPending.PatNumSubscriber=insSub.Subscriber;
			insPending.Relationship=relationship;
			insPending.Ordinal=ordinal;
			insPending.SubscriberID=insSub.SubscriberID??"";
			InsPlan insPlan=InsPlans.GetPlan(insSub.PlanNum,null);
			if(insPlan!=null) {
				insPending.GroupName=insPlan.GroupName??"";
				insPending.GroupNum=insPlan.GroupNum??"";
				insPending.Employer=Employers.GetName(insPlan.EmployerNum)??"";
				Carrier carrier=Carriers.GetCarrier(insPlan.CarrierNum);
				if(carrier!=null) {
					insPending.CarrierName=carrier.CarrierName??"";
					insPending.Phone=carrier.Phone??"";
				}
			}
			Normalize(insPending);
			return insPending;
		}

		///<summary>A copy of another patient's pending row for this patient. Not inserted.</summary>
		public static InsPending CopyForPatient(InsPending insPendingSource,long patNum,Relat relationship,byte ordinal) {
			Meth.NoCheckMiddleTierRole();
			InsPending insPending=insPendingSource.Copy();
			insPending.InsPendingNum=0;
			insPending.PatNum=patNum;
			insPending.Relationship=relationship;
			insPending.Ordinal=ordinal;
			Normalize(insPending);
			return insPending;
		}

		///<summary>Our guess at a patient's relationship to a subscriber when the patient did not say (an Existing answer): Self for the same person, Child
		///when the patient is marked as a child, Spouse when both are married, otherwise Dependent. Staff see and can change it before converting.</summary>
		public static Relat GuessRelationship(Patient patient,Patient patientSubscriber) {
			Meth.NoCheckMiddleTierRole();
			if(patientSubscriber==null || patient.PatNum==patientSubscriber.PatNum) {
				return Relat.Self;
			}
			if(patient.Position==PatientPosition.Child) {
				return Relat.Child;
			}
			if(patient.Position==PatientPosition.Married && patientSubscriber.Position==PatientPosition.Married) {
				return Relat.Spouse;
			}
			return Relat.Dependent;
		}

		///<summary>A short one-line description for lists, like "Delta (sub: John Doe, ID 12345)".</summary>
		public static string GetDescription(InsPending insPending,Family family) {
			Meth.NoCheckMiddleTierRole();
			string subscriber=family?.GetNameInFamFL(insPending.PatNumSubscriber)??"";
			if(subscriber=="") {
				subscriber=Patients.GetNameFL(insPending.PatNumSubscriber);
			}
			return (insPending.CarrierName==""?Lans.g("InsPendings","(no carrier)"):insPending.CarrierName)
				+" ("+Lans.g("InsPendings","sub:")+" "+subscriber+", "+Lans.g("InsPendings","ID")+" "+insPending.SubscriberID+")";
		}
		#endregion Misc Methods

		#region Conversion
		///<summary>Carriers whose name matches the pending carrier name: exact matches (ignoring case) first, then near matches (one name contains the
		///other, or they share the first word). Hidden carriers are left out.</summary>
		public static List<Carrier> GetCarrierMatches(string carrierName) {
			Meth.NoCheckMiddleTierRole();
			string name=(carrierName??"").Trim().ToLower();
			if(name=="") {
				return new List<Carrier>();
			}
			string firstWord=name.Split(new char[] { ' ' },StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
			List<Carrier> listCarriers=Carriers.GetWhere(x => !x.IsHidden);
			List<Carrier> listExact=listCarriers.FindAll(x => (x.CarrierName??"").Trim().ToLower()==name);
			List<Carrier> listNear=listCarriers.FindAll(x => {
				string nameCarrier=(x.CarrierName??"").Trim().ToLower();
				if(nameCarrier=="" || nameCarrier==name) {
					return false;
				}
				if(nameCarrier.Contains(name) || name.Contains(nameCarrier)) {
					return true;
				}
				return firstWord.Length>=3 && nameCarrier.Split(new char[] { ' ' },StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()==firstWord;
			});
			return listExact.OrderBy(x => x.CarrierName).Concat(listNear.OrderBy(x => x.CarrierName)).ToList();
		}

		///<summary>The carrier's plans that are not hidden. When onlyMatching is true, only plans with the pending group number (when one was entered) and
		///employer (when one was entered).</summary>
		public static List<InsPlan> GetPlanMatches(long carrierNum,string groupNum,string employer,bool onlyMatching) {
			Meth.NoCheckMiddleTierRole();
			if(carrierNum==0) {
				return new List<InsPlan>();
			}
			List<InsPlan> listInsPlans=GetPlansForCarrier(carrierNum);
			if(!onlyMatching) {
				return listInsPlans;
			}
			string groupNumNorm=EFormInsPick.NormalizeSubscriberID(groupNum).ToLower();
			string employerNorm=(employer??"").Trim().ToLower();
			return listInsPlans.FindAll(x =>
				(groupNumNorm=="" || EFormInsPick.NormalizeSubscriberID(x.GroupNum).ToLower()==groupNumNorm)
				&& (employerNorm=="" || (Employers.GetName(x.EmployerNum)??"").Trim().ToLower()==employerNorm));
		}

		///<summary>The plans of a carrier that are not hidden.</summary>
		public static List<InsPlan> GetPlansForCarrier(long carrierNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<InsPlan>>(MethodBase.GetCurrentMethod(),carrierNum);
			}
			string command="SELECT * FROM insplan WHERE CarrierNum="+POut.Long(carrierNum)+" AND IsHidden=0 ORDER BY GroupName,GroupNum";
			return Crud.InsPlanCrud.SelectMany(command);
		}

		///<summary>The permissions a conversion needs: CarrierCreate when a carrier is created (as in the plan and carrier windows), InsPlanCreate when a
		///carrier or plan is created, InsPlanEdit when attaching to an existing plan, and PatPlanCreate for the new patplan rows.</summary>
		public static List<EnumPermType> GetPermissionsForConvert(long carrierNum,long planNum) {
			Meth.NoCheckMiddleTierRole();
			List<EnumPermType> listPerms=new List<EnumPermType>();
			if(carrierNum==0) {
				listPerms.Add(EnumPermType.CarrierCreate);
			}
			if(carrierNum==0 || planNum==0) {
				listPerms.Add(EnumPermType.InsPlanCreate);
			}
			if(planNum!=0) {
				listPerms.Add(EnumPermType.InsPlanEdit);
			}
			listPerms.Add(EnumPermType.PatPlanCreate);
			return listPerms;
		}

		///<summary>Turns a pending row into real insurance. carrierNum 0 creates a carrier from the pending name and phone; planNum 0 creates a plan for the
		///carrier with the pending group name, number and employer (and the default benefits). The subscriber's existing subscription to the plan is reused,
		///otherwise one is created with the pending subscriber ID. A patplan is added at the ordinal (pushing others down) with the pending relationship,
		///and the pending row is deleted. Each row in listInsPendingsAlso (other family members' rows for the same insurance) gets its own patplan on the
		///same subscription and is deleted too. Permissions are checked by the caller. Throws ApplicationException when the input is not usable.</summary>
		public static InsPendingConvertResult Convert(InsPending insPending,long carrierNum,long planNum,int ordinal,List<InsPending> listInsPendingsAlso=null) {
			Meth.NoCheckMiddleTierRole();
			listInsPendingsAlso=listInsPendingsAlso??new List<InsPending>();
			string error=Validate(insPending);
			if(error!="") {
				throw new ApplicationException(error);
			}
			if(GetOne(insPending.InsPendingNum)==null) {
				throw new ApplicationException(Lans.g("InsPendings","This pending insurance was already converted or deleted by another user."));
			}
			InsPendingConvertResult result=new InsPendingConvertResult();
			string strSource=Lans.g("InsPendings","from pending insurance");
			//Carrier--------------------------------------------------------------------------------------------------------------------------------
			Carrier carrier;
			if(carrierNum==0) {
				if(insPending.CarrierName.Trim()=="") {
					throw new ApplicationException(Lans.g("InsPendings","A carrier name is required to create a carrier."));
				}
				carrier=new Carrier();
				SetNullStringsEmpty(carrier);
				carrier.CarrierName=insPending.CarrierName.Trim();
				carrier.Phone=insPending.Phone.Trim();
				Carriers.Insert(carrier);
				Signalods.SetInvalid(InvalidType.Carriers);
				Carriers.RefreshCache();
				result.IsCarrierCreated=true;
				SecurityLogs.MakeLogEntry(EnumPermType.CarrierCreate,insPending.PatNum,"Carrier '"+carrier.CarrierName+"' created "+strSource+".");
			}
			else {
				carrier=Carriers.GetCarrier(carrierNum);
				if(carrier==null || carrier.CarrierNum==0) {
					throw new ApplicationException(Lans.g("InsPendings","The selected carrier no longer exists."));
				}
			}
			result.CarrierNum=carrier.CarrierNum;
			//Plan-----------------------------------------------------------------------------------------------------------------------------------
			InsPlan insPlan;
			if(planNum==0) {
				insPlan=new InsPlan();
				SetNullStringsEmpty(insPlan);
				insPlan.CarrierNum=carrier.CarrierNum;
				insPlan.GroupName=insPending.GroupName.Trim();
				insPlan.GroupNum=insPending.GroupNum.Trim();
				insPlan.EmployerNum=Employers.GetEmployerNum(insPending.Employer.Trim());
				insPlan.PlanType=PrefC.GetBool(PrefName.InsDefaultPPOpercent) ? "p" : "";
				insPlan.CobRule=(EnumCobRule)PrefC.GetInt(PrefName.InsDefaultCobRule);
				InsPlans.Insert(insPlan);
				AddDefaultBenefits(insPlan.PlanNum);
				result.IsPlanCreated=true;
				SecurityLogs.MakeLogEntry(EnumPermType.InsPlanCreate,insPending.PatNum,"Insurance plan for carrier '"+carrier.CarrierName+"'"
					+(insPlan.GroupNum==""?"":", group "+insPlan.GroupNum)+" created "+strSource+".",insPlan.PlanNum,DateTime.MinValue);
			}
			else {
				insPlan=InsPlans.GetPlan(planNum,null);
				if(insPlan==null) {
					throw new ApplicationException(Lans.g("InsPendings","The selected plan no longer exists."));
				}
				if(insPlan.CarrierNum!=carrier.CarrierNum) {
					throw new ApplicationException(Lans.g("InsPendings","The selected plan belongs to a different carrier."));
				}
			}
			result.PlanNum=insPlan.PlanNum;
			//Subscription---------------------------------------------------------------------------------------------------------------------------
			InsSub insSub=InsSubs.GetListForSubscriber(insPending.PatNumSubscriber).Find(x => x.PlanNum==insPlan.PlanNum);
			if(insSub==null) {
				insSub=new InsSub();
				SetNullStringsEmpty(insSub);
				insSub.PlanNum=insPlan.PlanNum;
				insSub.Subscriber=insPending.PatNumSubscriber;
				insSub.SubscriberID=EFormInsPick.NormalizeSubscriberID(insPending.SubscriberID);
				insSub.ReleaseInfo=true;
				insSub.AssignBen=PrefC.GetBool(PrefName.InsDefaultAssignBen);
				InsSubs.Insert(insSub);
				result.IsInsSubCreated=true;
				SecurityLogs.MakeLogEntry(EnumPermType.InsPlanCreate,insPending.PatNumSubscriber,"Subscription to '"+carrier.CarrierName+"' with subscriber ID "
					+insSub.SubscriberID+" created "+strSource+".",insPlan.PlanNum,DateTime.MinValue);
			}
			result.InsSubNum=insSub.InsSubNum;
			//Patplans-------------------------------------------------------------------------------------------------------------------------------
			AddPatPlanAndDelete(insPending,insSub,ordinal,carrier.CarrierName,result);
			for(int i=0;i<listInsPendingsAlso.Count;i++) {
				if(listInsPendingsAlso[i].InsPendingNum==insPending.InsPendingNum || GetOne(listInsPendingsAlso[i].InsPendingNum)==null) {
					continue;
				}
				AddPatPlanAndDelete(listInsPendingsAlso[i],insSub,listInsPendingsAlso[i].Ordinal,carrier.CarrierName,result);
			}
			//Estimates and HasIns for everyone who got coverage.
			for(int i=0;i<result.ListPatNumsChanged.Count;i++) {
				RecomputeForPatient(result.ListPatNumsChanged[i]);
			}
			return result;
		}

		///<summary>Adds the patplan for one pending row on the subscription (unless the patient already has one on it) and deletes the pending row.</summary>
		private static void AddPatPlanAndDelete(InsPending insPending,InsSub insSub,int ordinal,string carrierName,InsPendingConvertResult result) {
			List<PatPlan> listPatPlans=PatPlans.Refresh(insPending.PatNum);
			if(listPatPlans.Exists(x => x.InsSubNum==insSub.InsSubNum)) {
				result.ListNotes.Add(Patients.GetNameFL(insPending.PatNum)+": "+Lans.g("InsPendings","already had this insurance; pending row removed."));
			}
			else {
				PatPlan patPlan=new PatPlan();
				SetNullStringsEmpty(patPlan);
				patPlan.PatNum=insPending.PatNum;
				patPlan.InsSubNum=insSub.InsSubNum;
				patPlan.Relationship=insPending.Relationship;
				patPlan.Ordinal=(byte)(listPatPlans.Count+1);
				PatPlans.Insert(patPlan);
				if(ordinal<1) {
					ordinal=listPatPlans.Count+1;
				}
				if(ordinal<=listPatPlans.Count) {
					PatPlans.SetOrdinal(patPlan.PatPlanNum,ordinal);
				}
				result.ListPatPlanNums.Add(patPlan.PatPlanNum);
				SecurityLogs.MakeLogEntry(EnumPermType.PatPlanCreate,insPending.PatNum,"Insurance '"+carrierName+"' added "
					+Lans.g("InsPendings","from pending insurance")+".",insSub.PlanNum,DateTime.MinValue);
			}
			Delete(insPending.InsPendingNum);
			SecurityLogs.MakeLogEntry(EnumPermType.InsPlanEdit,insPending.PatNum,"Pending insurance '"+insPending.CarrierName+"' converted to insurance.");
			if(!result.ListPatNumsChanged.Contains(insPending.PatNum)) {
				result.ListPatNumsChanged.Add(insPending.PatNum);
			}
		}

		///<summary>After coverage changes: recompute the patient's estimates and set patient.HasIns, as the sheet import does after adding a plan.</summary>
		private static void RecomputeForPatient(long patNum) {
			Patient patient=Patients.GetPat(patNum);
			if(patient==null) {
				return;
			}
			Family family=Patients.GetFamily(patNum);
			List<ClaimProc> listClaimProcs=ClaimProcs.Refresh(patNum);
			List<Procedure> listProcedures=Procedures.Refresh(patNum);
			List<PatPlan> listPatPlans=PatPlans.Refresh(patNum);
			List<InsSub> listInsSubs=InsSubs.RefreshForFam(family);
			List<InsPlan> listInsPlans=InsPlans.RefreshForSubList(listInsSubs);
			List<Benefit> listBenefits=Benefits.Refresh(listPatPlans,listInsSubs);
			Procedures.ComputeEstimatesForAll(patNum,listClaimProcs,listProcedures,listInsPlans,listPatPlans,listBenefits,patient.Age,listInsSubs);
			Patients.SetHasIns(patNum);
		}

		///<summary>The coinsurance percentages of the coverage categories and zero deductibles for diagnostic and preventive, the same defaults the sheet
		///import gives a plan it creates.</summary>
		private static void AddDefaultBenefits(long planNum) {
			List<CovCat> listCovCats=CovCats.GetDeepCopy(true);
			for(int i=0;i<listCovCats.Count;i++) {
				if(listCovCats[i].DefaultPercent==-1) {
					continue;
				}
				Benefit benefit=new Benefit();
				benefit.BenefitType=InsBenefitType.CoInsurance;
				benefit.CovCatNum=listCovCats[i].CovCatNum;
				benefit.PlanNum=planNum;
				benefit.Percent=listCovCats[i].DefaultPercent;
				benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
				benefit.CodeNum=0;
				Benefits.Insert(benefit);
			}
			List<EbenefitCategory> listEbenCats=new List<EbenefitCategory>() { EbenefitCategory.Diagnostic,EbenefitCategory.RoutinePreventive };
			for(int i=0;i<listEbenCats.Count;i++) {
				CovCat covCat=CovCats.GetForEbenCat(listEbenCats[i]);
				if(covCat==null) {
					continue;
				}
				Benefit benefit=new Benefit();
				benefit.CodeNum=0;
				benefit.BenefitType=InsBenefitType.Deductible;
				benefit.CovCatNum=covCat.CovCatNum;
				benefit.PlanNum=planNum;
				benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
				benefit.MonetaryAmt=0;
				benefit.Percent=-1;
				benefit.CoverageLevel=BenefitCoverageLevel.Individual;
				Benefits.Insert(benefit);
			}
		}

		///<summary>New table objects leave their string fields null; the generated CRUD and other code expect empty strings.</summary>
		private static void SetNullStringsEmpty(object obj) {
			FieldInfo[] arrayFieldInfos=obj.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance);
			for(int i=0;i<arrayFieldInfos.Length;i++) {
				if(arrayFieldInfos[i].FieldType==typeof(string) && arrayFieldInfos[i].GetValue(obj)==null) {
					arrayFieldInfos[i].SetValue(obj,"");
				}
			}
		}
		#endregion Conversion
	}

	///<summary>What a conversion of pending insurance created.</summary>
	public class InsPendingConvertResult {
		public long CarrierNum;
		public long PlanNum;
		public long InsSubNum;
		public bool IsCarrierCreated;
		public bool IsPlanCreated;
		public bool IsInsSubCreated;
		public List<long> ListPatPlanNums=new List<long>();
		public List<long> ListPatNumsChanged=new List<long>();
		public List<string> ListNotes=new List<string>();
	}
}
