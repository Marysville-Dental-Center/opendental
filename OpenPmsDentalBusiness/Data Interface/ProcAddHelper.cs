// Adding treatment planned procedures to the patient's Active treatment plan straight from the Treatment Plan module (Open Dental 25.3, "easier
// adding of procedures to the active treatment plan", backlog package B-TP). The procedure is created exactly as the Chart module creates one
// (ChartModules.AddProcHelper: provider, clinic, fee, default note, estimates, treatment plan attach), so ControlChart.cs is not touched.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>One row of the Add Procedures window: a procedure code and what the user typed for its teeth and its surfaces or area, in the
	///office's tooth numbering. One row can make several procedures, for example a tooth code typed for teeth 3,5.</summary>
	[Serializable]
	public class ProcAddEntry {
		///<summary>FK to procedurecode.CodeNum.</summary>
		public long CodeNum;
		///<summary>Teeth as typed: one tooth, a list (3,5) or a range (3-5), in the office's nomenclature. Blank when none.</summary>
		public string ToothEntry="";
		///<summary>Surfaces (MOD), quadrant (UR, UL, LR, LL), sextant (1-6) or arch (U, L) as typed. Blank when none.</summary>
		public string SurfEntry="";

		public ProcAddEntry Copy() {
			return (ProcAddEntry)MemberwiseClone();
		}
	}

	///<summary>Where one new procedure goes, in database values (Universal tooth numbers, database surfaces).</summary>
	public class ProcAddPlacement {
		public string ToothNum="";
		public string Surf="";
		public string ToothRange="";
	}

	///<summary>What ProcAddHelper.AddToActivePlan did.</summary>
	public class ProcAddResult {
		///<summary>The Active plan the procedures were attached to. Created when the patient had none.</summary>
		public TreatPlan TreatPlanActive;
		///<summary>True when the Active plan was created by this call.</summary>
		public bool IsTreatPlanCreated;
		///<summary>The new procedures, in the order of the entries.</summary>
		public List<Procedure> ListProcedures=new List<Procedure>();
		///<summary>The patient data the procedures were created with: patient, family, insurance, benefits, appointments, procedures.</summary>
		public PatientData PatientData;
		///<summary>The estimates of each new procedure, by ProcNum, as computed when it was created.</summary>
		public Dictionary<long,List<ClaimProc>> DictClaimProcs=new Dictionary<long,List<ClaimProc>>();

		///<summary>The estimates of one new procedure. Never null.</summary>
		public List<ClaimProc> GetClaimProcs(long procNum) {
			if(DictClaimProcs.TryGetValue(procNum,out List<ClaimProc> listClaimProcs)) {
				return listClaimProcs;
			}
			return new List<ClaimProc>();
		}
	}

	///<summary>The shared procedure-entry rules for adding treatment planned procedures outside the Chart module. Validation mirrors Chart entry:
	///the code must exist, must not be in a hidden category and must not be the group note code, and every code gets the tooth, surfaces or area
	///its treatment area needs. Only the patient's Active treatment plan can be the target.</summary>
	public class ProcAddHelper {
		private const string LANS="ProcAddHelper";
		private static readonly string[] _arrayQuadrants=new[] { "UR","UL","LR","LL" };

		#region Validation
		///<summary>Blank when the code can be treatment planned from the Treatment Plan module; otherwise why not. Uses the caches.</summary>
		public static string GetCodeError(ProcedureCode procedureCode) {
			Meth.NoCheckMiddleTierRole();
			if(procedureCode==null || procedureCode.CodeNum==0) {
				return Lans.g(LANS,"Procedure code not found.");
			}
			if(procedureCode.ProcCode==ProcedureCodes.GroupProcCode) {
				return Lans.g(LANS,"Group notes cannot be treatment planned.");
			}
			if(ProcedureCodes.AreAnyProcCodesHidden(procedureCode.CodeNum)) {
				return Lans.g(LANS,"The code is in a hidden category.");
			}
			return "";
		}

		///<summary>Blank when the row is ready to add; otherwise the first problem. Uses the caches for the code and the office's tooth numbering.</summary>
		public static string GetEntryError(ProcAddEntry procAddEntry) {
			Meth.NoCheckMiddleTierRole();
			return GetEntryError(procAddEntry,(ToothNumberingNomenclature)PrefC.GetInt(PrefName.UseInternationalToothNumbers));
		}

		///<summary>Blank when the row is ready to add; otherwise the first problem. Uses the caches for the code.</summary>
		public static string GetEntryError(ProcAddEntry procAddEntry,ToothNumberingNomenclature toothNumberingNomenclature) {
			Meth.NoCheckMiddleTierRole();
			if(procAddEntry==null) {
				return Lans.g(LANS,"Procedure code not found.");
			}
			ProcedureCode procedureCode=ProcedureCodes.GetProcCode(procAddEntry.CodeNum);
			string error=GetCodeError(procedureCode);
			if(error!="") {
				return error;
			}
			GetPlacements(procedureCode,procAddEntry.ToothEntry,procAddEntry.SurfEntry,toothNumberingNomenclature,out error);
			return error;
		}

		///<summary>Turns what the user typed into the procedures to create, following the code's treatment area the way Chart entry does: a tooth or
		///surface code makes one procedure per tooth; a quadrant or arch code typed with teeth instead of an area makes one per quadrant or arch (with
		///the teeth as its tooth range when the code also takes a tooth range); a tooth range code makes one procedure for all the teeth. Returns an
		///empty list and sets error when something is missing or invalid; entries a code cannot use are refused rather than dropped, so nothing the
		///user typed is silently lost. Uses no cache or database, so it can be tested on its own.</summary>
		public static List<ProcAddPlacement> GetPlacements(ProcedureCode procedureCode,string toothEntry,string surfEntry,
			ToothNumberingNomenclature toothNumberingNomenclature,out string error)
		{
			Meth.NoCheckMiddleTierRole();
			error="";
			List<ProcAddPlacement> listProcAddPlacements=new List<ProcAddPlacement>();
			if(procedureCode==null) {
				error=Lans.g(LANS,"Procedure code not found.");
				return listProcAddPlacements;
			}
			toothEntry=(toothEntry??"").Trim();
			surfEntry=Regex.Replace(surfEntry??"",@"\s+","").ToUpper();
			List<string> listTeeth;
			try {
				listTeeth=Tooth.GetToothListTeeth(Tooth.ParseToothList(toothEntry,toothNumberingNomenclature));
			}
			catch(ApplicationException ex) {
				error=ex.Message;
				return listProcAddPlacements;
			}
			switch(procedureCode.TreatArea) {
				case TreatmentArea.Surf:
					return GetPlacementsSurf(listTeeth,surfEntry,toothNumberingNomenclature,out error);
				case TreatmentArea.Tooth:
					if(listTeeth.Count==0) {
						error=Lans.g(LANS,"Enter the tooth.");
						return listProcAddPlacements;
					}
					if(surfEntry!="") {
						error=Lans.g(LANS,"This code takes a tooth only. Clear the surface or area.");
						return listProcAddPlacements;
					}
					return listTeeth.Select(x => new ProcAddPlacement { ToothNum=x }).ToList();
				case TreatmentArea.ToothRange:
					if(listTeeth.Count==0) {
						error=Lans.g(LANS,"Enter the teeth of the range.");
						return listProcAddPlacements;
					}
					if(surfEntry!="") {
						error=Lans.g(LANS,"This code takes a tooth range only. Clear the surface or area.");
						return listProcAddPlacements;
					}
					listProcAddPlacements.Add(new ProcAddPlacement { ToothRange=string.Join(",",listTeeth) });
					return listProcAddPlacements;
				case TreatmentArea.Quad:
					return GetPlacementsQuadOrArch(procedureCode,listTeeth,surfEntry,isQuad:true,toothNumberingNomenclature,out error);
				case TreatmentArea.Arch:
					return GetPlacementsQuadOrArch(procedureCode,listTeeth,surfEntry,isQuad:false,toothNumberingNomenclature,out error);
				case TreatmentArea.Sextant:
					if(listTeeth.Count>0) {
						error=Lans.g(LANS,"This code takes a sextant, not teeth. Clear the tooth.");
						return listProcAddPlacements;
					}
					string sextant=ParseSextant(surfEntry,toothNumberingNomenclature);
					if(sextant=="") {
						error=Lans.g(LANS,"Enter the sextant (1 to 6).");
						return listProcAddPlacements;
					}
					listProcAddPlacements.Add(new ProcAddPlacement { Surf=sextant });
					return listProcAddPlacements;
				default://Mouth and None
					if(listTeeth.Count>0 || surfEntry!="") {
						error=Lans.g(LANS,"This code is for the whole mouth. Clear the tooth and area.");
						return listProcAddPlacements;
					}
					listProcAddPlacements.Add(new ProcAddPlacement());
					return listProcAddPlacements;
			}
		}

		private static List<ProcAddPlacement> GetPlacementsSurf(List<string> listTeeth,string surfEntry,ToothNumberingNomenclature toothNumberingNomenclature,
			out string error)
		{
			error="";
			List<ProcAddPlacement> listProcAddPlacements=new List<ProcAddPlacement>();
			if(listTeeth.Count==0) {
				error=Lans.g(LANS,"Enter the tooth.");
				return listProcAddPlacements;
			}
			if(surfEntry=="") {
				error=Lans.g(LANS,"Enter the surfaces.");
				return listProcAddPlacements;
			}
			for(int i=0;i<listTeeth.Count;i++) {
				string surfDb=Tooth.SurfTidyFromDisplayToDb(surfEntry,listTeeth[i]);
				//Chart entry quietly drops letters that do not fit the tooth (O on an anterior tooth). Here they are refused so nothing is lost.
				string surfKept=Tooth.SurfTidyFromDbToDisplay(surfDb,listTeeth[i]);
				string lettersRefused=new string(surfEntry.Distinct().Where(x => surfKept.IndexOf(x)<0).ToArray());
				if(lettersRefused!="" || surfDb=="") {
					error=Lans.g(LANS,"Surface")+" "+(lettersRefused=="" ? surfEntry : lettersRefused)+" "+Lans.g(LANS,"is not valid for tooth")+" "
						+Tooth.Display(listTeeth[i],toothNumberingNomenclature)+".";
					return new List<ProcAddPlacement>();
				}
				listProcAddPlacements.Add(new ProcAddPlacement { ToothNum=listTeeth[i],Surf=surfDb });
			}
			return listProcAddPlacements;
		}

		private static List<ProcAddPlacement> GetPlacementsQuadOrArch(ProcedureCode procedureCode,List<string> listTeeth,string surfEntry,bool isQuad,
			ToothNumberingNomenclature toothNumberingNomenclature,out string error)
		{
			error="";
			List<ProcAddPlacement> listProcAddPlacements=new List<ProcAddPlacement>();
			string area=surfEntry;
			if(area!="" && !IsValidArea(area,isQuad)) {
				error=isQuad ? Lans.g(LANS,"Enter the quadrant as UR, UL, LR or LL.") : Lans.g(LANS,"Enter the arch as U or L.");
				return listProcAddPlacements;
			}
			if(area=="" && listTeeth.Count==0) {
				error=isQuad ? Lans.g(LANS,"Enter the quadrant (UR, UL, LR or LL) or the teeth.") : Lans.g(LANS,"Enter the arch (U or L) or the teeth.");
				return listProcAddPlacements;
			}
			if(area!="" && listTeeth.Count>0 && !procedureCode.AreaAlsoToothRange) {
				error=isQuad ? Lans.g(LANS,"Enter the quadrant or the teeth, not both.") : Lans.g(LANS,"Enter the arch or the teeth, not both.");
				return listProcAddPlacements;
			}
			if(area!="") {
				ProcAddPlacement procAddPlacement=new ProcAddPlacement { Surf=area };
				if(listTeeth.Count>0) {//The code also takes a tooth range: the teeth must be in the typed quadrant or arch.
					string toothOutside=listTeeth.FirstOrDefault(x => GetArea(x,isQuad)!=area);
					if(toothOutside!=null) {
						error=Lans.g(LANS,"Tooth")+" "+Tooth.Display(toothOutside,toothNumberingNomenclature)+" "+Lans.g(LANS,"is not in")+" "+area+".";
						return listProcAddPlacements;
					}
					procAddPlacement.ToothRange=string.Join(",",listTeeth);
				}
				listProcAddPlacements.Add(procAddPlacement);
				return listProcAddPlacements;
			}
			//Teeth without an area: one procedure per quadrant or arch the teeth are in, as Chart entry does with teeth selected.
			string toothNoArea=listTeeth.FirstOrDefault(x => GetArea(x,isQuad)=="");
			if(toothNoArea!=null) {
				error=Lans.g(LANS,"Tooth")+" "+Tooth.Display(toothNoArea,toothNumberingNomenclature)+" "
					+(isQuad ? Lans.g(LANS,"is not in a quadrant.") : Lans.g(LANS,"is not in an arch."));
				return listProcAddPlacements;
			}
			List<string> listAreas=listTeeth.Select(x => GetArea(x,isQuad)).Distinct().ToList();
			if(isQuad) {
				listAreas=_arrayQuadrants.Where(x => listAreas.Contains(x)).ToList();//UR, UL, LR, LL order
			}
			else {
				listAreas=new[] { "U","L" }.Where(x => listAreas.Contains(x)).ToList();
			}
			for(int i=0;i<listAreas.Count;i++) {
				ProcAddPlacement procAddPlacement=new ProcAddPlacement { Surf=listAreas[i] };
				if(procedureCode.AreaAlsoToothRange) {
					procAddPlacement.ToothRange=string.Join(",",listTeeth.Where(x => GetArea(x,isQuad)==listAreas[i]));
				}
				listProcAddPlacements.Add(procAddPlacement);
			}
			return listProcAddPlacements;
		}

		private static bool IsValidArea(string area,bool isQuad) {
			if(isQuad) {
				return _arrayQuadrants.Contains(area);
			}
			return area=="U" || area=="L";
		}

		///<summary>The quadrant (UR, UL, LR, LL) or arch (U, L) of a tooth in database numbering. Blank when it has none.</summary>
		private static string GetArea(string toothNum,bool isQuad) {
			if(!Tooth.IsValidDB(toothNum)) {
				return "";
			}
			if(isQuad) {
				return Tooth.GetQuadrant(toothNum);
			}
			int intTooth=Tooth.ToInt(toothNum);
			if(intTooth>=1 && intTooth<=16) {
				return "U";
			}
			if(intTooth>=17 && intTooth<=32) {
				return "L";
			}
			return "";
		}

		///<summary>The sextant in database values (1 to 6) for a typed sextant: 1 to 6, or 03 to 08 when the office uses FDI numbering (the way FDI
		///sextants are shown). Blank when the entry is not a sextant.</summary>
		public static string ParseSextant(string sextantEntry,ToothNumberingNomenclature toothNumberingNomenclature) {
			Meth.NoCheckMiddleTierRole();
			sextantEntry=(sextantEntry??"").Trim();
			for(int i=1;i<=6;i++) {
				string sextant=i.ToString();
				if(sextantEntry==sextant) {
					return sextant;
				}
				if(toothNumberingNomenclature==ToothNumberingNomenclature.FDI && sextantEntry==Tooth.GetSextant(sextant,ToothNumberingNomenclature.FDI)) {
					return sextant;
				}
			}
			return "";
		}

		///<summary>Blank when procedures can be added to this plan for this patient; otherwise why not. Only the patient's Active plan qualifies.</summary>
		public static string GetTargetError(TreatPlan treatPlan,long patNum) {
			Meth.NoCheckMiddleTierRole();
			if(treatPlan==null) {
				return Lans.g(LANS,"The treatment plan no longer exists.");
			}
			if(treatPlan.PatNum!=patNum) {
				return Lans.g(LANS,"The treatment plan belongs to another patient.");
			}
			if(!TreatPlans.CanAddProcsTo(treatPlan)) {
				return Lans.g(LANS,"Procedures can only be added to the Active treatment plan.");
			}
			return "";
		}
		#endregion Validation

		#region Display
		///<summary>The treatment area as shown in the Add Procedures window.</summary>
		public static string GetTreatAreaText(TreatmentArea treatmentArea) {
			Meth.NoCheckMiddleTierRole();
			switch(treatmentArea) {
				case TreatmentArea.Surf:
					return Lans.g(LANS,"Surface");
				case TreatmentArea.Tooth:
					return Lans.g(LANS,"Tooth");
				case TreatmentArea.Mouth:
					return Lans.g(LANS,"Mouth");
				case TreatmentArea.Quad:
					return Lans.g(LANS,"Quadrant");
				case TreatmentArea.Sextant:
					return Lans.g(LANS,"Sextant");
				case TreatmentArea.Arch:
					return Lans.g(LANS,"Arch");
				case TreatmentArea.ToothRange:
					return Lans.g(LANS,"Tooth Range");
				default:
					return Lans.g(LANS,"None");
			}
		}

		///<summary>True when the code's treatment area uses the Surf/Area entry: surfaces, quadrant, sextant or arch.</summary>
		public static bool UsesSurfEntry(ProcedureCode procedureCode) {
			Meth.NoCheckMiddleTierRole();
			return procedureCode!=null && procedureCode.TreatArea.In(TreatmentArea.Surf,TreatmentArea.Quad,TreatmentArea.Sextant,TreatmentArea.Arch);
		}
		#endregion Display

		#region Picking codes
		///<summary>The CodeNum for a code typed into the code box, or 0 when it is not a valid code. Like Chart entry, in the United States four digits
		///that are not a visible code are tried with a D in front.</summary>
		public static long FindTypedCode(string text) {
			Meth.NoCheckMiddleTierRole();
			return FindTypedCode(text,CultureInfo.CurrentCulture.Name=="en-US");
		}

		///<summary>The CodeNum for a code typed into the code box, or 0 when it is not a valid code.</summary>
		public static long FindTypedCode(string text,bool isUnitedStates) {
			Meth.NoCheckMiddleTierRole();
			text=(text??"").Trim();
			if(text=="") {
				return 0;
			}
			if(isUnitedStates && Regex.IsMatch(text,@"^\d{4}$")) {
				if(!ProcedureCodes.GetContainsKey(text) || ProcedureCodes.AreAnyProcCodesHidden(ProcedureCodes.GetCodeNum(text))) {
					text="D"+text;
				}
			}
			if(!ProcedureCodes.IsValidCode(text)) {
				return 0;
			}
			return ProcedureCodes.GetCodeNum(text);
		}

		///<summary>The Chart module's quick buttons that can add a procedure here, in their Chart order (row, then position): labels and buttons whose
		///code does not exist or is in a hidden category are left out.</summary>
		public static List<ProcButtonQuick> GetQuickButtons(List<ProcButtonQuick> listProcButtonQuicks) {
			Meth.NoCheckMiddleTierRole();
			List<ProcButtonQuick> listProcButtonQuicksUsable=(listProcButtonQuicks??new List<ProcButtonQuick>())
				.Where(x => !x.IsLabel)
				.Where(x => GetCodeError(ProcedureCodes.GetProcCode(ProcedureCodes.GetCodeNum(x.CodeValue)))=="")
				.ToList();
			listProcButtonQuicksUsable.Sort(ProcButtonQuicks.sortYX);
			return listProcButtonQuicksUsable;
		}

		///<summary>A new row for a quick button: its code, and its surfaces when the code's treatment area uses them (Chart entry ignores a quick
		///button's surfaces for other codes).</summary>
		public static ProcAddEntry GetEntryForQuickButton(ProcButtonQuick procButtonQuick) {
			Meth.NoCheckMiddleTierRole();
			ProcedureCode procedureCode=ProcedureCodes.GetProcCode(ProcedureCodes.GetCodeNum(procButtonQuick.CodeValue));
			ProcAddEntry procAddEntry=new ProcAddEntry { CodeNum=procedureCode.CodeNum };
			if(UsesSurfEntry(procedureCode)) {
				procAddEntry.SurfEntry=(procButtonQuick.Surf??"").Trim().ToUpper();
			}
			return procAddEntry;
		}
		#endregion Picking codes

		#region Adding
		///<summary>Creates the procedures with status TP and attaches them to the patient's Active treatment plan in one step, with the given priority
		///(a TxPriorities DefNum, or 0). Each procedure is created as Chart entry creates one: provider, clinic, fee from the patient's fee schedules,
		///default note, estimates and the treatment plan attach. Pass treatPlanNum 0 when the patient has no treatment plan yet: an Active plan is
		///created. A plan number that is not the patient's current Active plan (Inactive, Saved, another patient's, or a plan another workstation
		///just made inactive) is refused. Every entry is checked before anything is written; throws an ODException listing the problems.</summary>
		public static ProcAddResult AddToActivePlan(long patNum,long treatPlanNum,List<ProcAddEntry> listProcAddEntries,long priorityNum,DateTime dateTP) {
			Meth.NoCheckMiddleTierRole();
			if(listProcAddEntries==null || listProcAddEntries.Count==0) {
				throw new ODException(Lans.g(LANS,"No procedures to add."));
			}
			ToothNumberingNomenclature toothNumberingNomenclature=(ToothNumberingNomenclature)PrefC.GetInt(PrefName.UseInternationalToothNumbers);
			List<string> listErrors=new List<string>();
			for(int i=0;i<listProcAddEntries.Count;i++) {
				string error=GetEntryError(listProcAddEntries[i],toothNumberingNomenclature);
				if(error!="") {
					listErrors.Add(ProcedureCodes.GetStringProcCode(listProcAddEntries[i].CodeNum)+": "+error);
				}
			}
			if(listErrors.Count>0) {
				throw new ODException(string.Join("\r\n",listErrors));
			}
			TreatPlan treatPlanActive=TreatPlans.GetActiveForPat(patNum);
			if(treatPlanNum!=0) {
				string targetError=GetTargetError(TreatPlans.GetOne(treatPlanNum),patNum);
				if(targetError=="" && (treatPlanActive==null || treatPlanActive.TreatPlanNum!=treatPlanNum)) {
					targetError=Lans.g(LANS,"Procedures can only be added to the Active treatment plan.");
				}
				if(targetError!="") {
					throw new ODException(targetError);
				}
			}
			PatientData patientData=new PatientData();
			patientData.SetPatNum(patNum);
			patientData.FillIfNeeded(EnumPdTable.Patient,EnumPdTable.InsSub,EnumPdTable.InsPlan,EnumPdTable.PatPlan,EnumPdTable.Benefit,
				EnumPdTable.ClaimProcHist,EnumPdTable.Appointment,EnumPdTable.Procedure);
			if(patientData.Patient==null) {
				throw new ODException(Lans.g(LANS,"Patient not found."));
			}
			ProcAddResult procAddResult=new ProcAddResult();
			procAddResult.PatientData=patientData;
			if(treatPlanActive==null) {//Same plan Chart entry creates when a patient with no Active plan gets a TP procedure.
				treatPlanActive=new TreatPlan() {
					Heading=Lans.g("TreatPlans","Active Treatment Plan"),
					Note=PrefC.GetString(PrefName.TreatmentPlanNote),
					TPStatus=TreatPlanStatus.Active,
					PatNum=patNum,
					TPType=DiscountPlanSubs.HasDiscountPlan(patNum) ? TreatPlanType.Discount : TreatPlanType.Insurance
				};
				TreatPlans.Insert(treatPlanActive);
				procAddResult.IsTreatPlanCreated=true;
			}
			procAddResult.TreatPlanActive=treatPlanActive;
			List<ProcedureCode> listProcedureCodes=listProcAddEntries.Select(x => x.CodeNum).Distinct().Select(x => ProcedureCodes.GetProcCode(x)).ToList();
			List<SubstitutionLink> listSubstitutionLinks=SubstitutionLinks.GetAllForPlans(patientData.ListInsPlans);
			long discountPlanNum=DiscountPlanSubs.GetDiscountPlanNumForPat(patNum);
			List<Fee> listFees=Fees.GetListFromObjects(listProcedureCodes,null,//no proc, so medical code won't have changed
				null,//listProvNumsTreat: providers will instead be set from other places
				patientData.Patient.PriProv,patientData.Patient.SecProv,patientData.Patient.FeeSched,patientData.ListInsPlans,
				new List<long>(){ patientData.Patient.ClinicNum },patientData.ListAppointments,listSubstitutionLinks,discountPlanNum);
			if(listProcedureCodes.Any(x => !IsBrokenApptCode(x.ProcCode))) {//As Chart entry: broken appointment codes don't count as a first visit.
				Procedures.SetDateFirstVisit(DateTime.Today,1,patientData.Patient);
			}
			List<TreatPlan> listTreatPlans=TreatPlans.GetAllCurrentForPat(patNum);
			for(int i=0;i<listProcAddEntries.Count;i++) {
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(listProcAddEntries[i].CodeNum);
				List<ProcAddPlacement> listProcAddPlacements=GetPlacements(procedureCode,listProcAddEntries[i].ToothEntry,listProcAddEntries[i].SurfEntry,
					toothNumberingNomenclature,out string _);
				for(int p=0;p<listProcAddPlacements.Count;p++) {
					Procedure procedure=new Procedure();
					procedure.CodeNum=procedureCode.CodeNum;
					procedure.ToothNum=listProcAddPlacements[p].ToothNum;
					procedure.Surf=listProcAddPlacements[p].Surf;
					procedure.ToothRange=listProcAddPlacements[p].ToothRange;
					procedure.DateTP=dateTP;
					List<ClaimProc> listClaimProcs=new List<ClaimProc>();
					List<ClaimProcHist> listClaimProcHistsLoop=new List<ClaimProcHist>();
					//A new list each time: AttachProcToTPs adds to it.
					List<long> listTreatPlanNums=new List<long>() { treatPlanActive.TreatPlanNum };
					if(!ChartModules.AddProcHelper(procedure,listFees,patientData,ProcStat.TP,null,listTreatPlans,listSubstitutionLinks,priorityNum,
						listTreatPlanNums,ref listClaimProcs,ref listClaimProcHistsLoop,diagnosisNum:0,prognosisNum:0))
					{
						continue;//Only refuses completed procedures, which are never made here.
					}
					procAddResult.ListProcedures.Add(procedure);
					procAddResult.DictClaimProcs[procedure.ProcNum]=listClaimProcs;
					SecurityLogs.MakeLogEntry(EnumPermType.ProcEdit,patNum,GetLogText(procedure,procedureCode,toothNumberingNomenclature));
				}
			}
			return procAddResult;
		}

		///<summary>The two broken appointment codes, which Chart entry does not count as a first visit.</summary>
		public static bool IsBrokenApptCode(string procCode) {
			Meth.NoCheckMiddleTierRole();
			return procCode=="D9986" || procCode=="D9987";
		}

		///<summary>The ProcEdit audit entry for a procedure added from the Treatment Plan module, like the other "treatment planned via" entries.</summary>
		public static string GetLogText(Procedure procedure,ProcedureCode procedureCode,ToothNumberingNomenclature toothNumberingNomenclature) {
			Meth.NoCheckMiddleTierRole();
			string logText=procedureCode.ProcCode;
			if(!string.IsNullOrEmpty(procedure.ToothNum)) {
				logText+=", "+Lans.g(LANS,"tooth")+" "+Tooth.Display(procedure.ToothNum,toothNumberingNomenclature);
			}
			if(!string.IsNullOrEmpty(procedure.ToothRange)) {
				logText+=", "+Lans.g(LANS,"teeth")+" "+Tooth.DisplayToothList(procedure.ToothRange,toothNumberingNomenclature);
			}
			if(!string.IsNullOrEmpty(procedure.Surf)) {
				logText+=", "+Tooth.SurfTidyFromDbToDisplay(procedure.Surf,procedure.ToothNum);
			}
			logText+=", "+procedure.ProcFee.ToString("c")+", "+Lans.g(LANS,"treatment planned from the Treatment Plan module.");
			return logText;
		}

		///<summary>The code the auto codes recommend for a new procedure, as Chart entry checks right after creating one. Equal to the procedure's own
		///code when there is nothing to suggest.</summary>
		public static long GetRecommendedCodeNum(Procedure procedure,Patient patient,List<ClaimProc> listClaimProcsForProc) {
			Meth.NoCheckMiddleTierRole();
			ProcedureCode procedureCode=ProcedureCodes.GetProcCode(procedure.CodeNum);
			bool isMandibular=false;
			if(!string.IsNullOrEmpty(procedure.ToothRange)) {
				isMandibular=procedure.ToothRange.Split(',').Any(x => !Tooth.IsMaxillary(x));
			}
			return AutoCodeItems.GetRecommendedCodeNum(procedure,procedureCode,patient,isMandibular,listClaimProcsForProc??new List<ClaimProc>());
		}
		#endregion Adding
	}
}
