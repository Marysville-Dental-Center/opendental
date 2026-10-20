// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.

namespace SyntheticData.Generation {
	///<summary>Stored enum numbers the generator writes, pinned to Open Dental's published database documentation (25.3). Values that
	///already existed in 24.3 are cross-checked against OpenDentBusiness by the tests; values that are new in 24.4 to 25.3 are cited.
	///This tool does not reference OpenDentBusiness at run time, because the schema it fills is newer than the business layer.</summary>
	public static class OdValues {
		//ApptStatus
		public const int AptScheduled=1, AptComplete=2, AptUnschedList=3, AptBroken=5, AptPlanned=6;
		//ProcStat
		public const int ProcTP=1, ProcComplete=2, ProcExistingCurrent=3, ProcExistingOther=4, ProcReferred=5, ProcDeleted=6;
		//ClaimProcStatus
		public const int CpNotReceived=0, CpReceived=1, CpPreauth=2, CpSupplemental=4, CpEstimate=6, CpInsHist=9;
		//PatientStatus
		public const int PatPatient=0, PatNonPatient=1, PatInactive=2, PatArchived=3, PatDeleted=4, PatDeceased=5, PatProspective=6;
		//PatientGender
		public const int GenderMale=0, GenderFemale=1, GenderUnknown=2;
		//PatientPosition
		public const int PosSingle=0, PosMarried=1, PosChild=2, PosWidowed=3, PosDivorced=4;
		//Relat
		public const int RelatSelf=0, RelatSpouse=1, RelatChild=2;
		//InsBenefitType
		public const int BenCoInsurance=1, BenDeductible=2, BenExclusions=4, BenLimitations=5, BenWaitingPeriod=6;
		//BenefitTimePeriod
		public const int TimeNone=0, TimeServiceYear=1, TimeCalendarYear=2, TimeLifetime=3, TimeYears=4;
		//BenefitQuantity
		public const int QtyNone=0, QtyNumberOfServices=1, QtyAgeLimit=2, QtyYears=4, QtyMonths=5;
		//BenefitCoverageLevel
		public const int CovNone=0, CovIndividual=1, CovFamily=2;
		//EbenefitCategory
		public const int EbGeneral=1, EbDiagnostic=2, EbPeriodontics=3, EbRestorative=4, EbEndodontics=5, EbCrowns=7, EbOrthodontics=9,
			EbProsthodontics=10, EbOralSurgery=11, EbRoutinePreventive=12, EbDiagnosticXRay=13, EbAdjunctive=14;
		//TreatmentArea
		public const int AreaSurf=1, AreaTooth=2, AreaMouth=3, AreaQuad=4, AreaArch=6;
		//ToothPaintingType
		public const int PaintNone=0, PaintExtraction=1, PaintImplant=2, PaintRct=3, PaintPostBU=4, PaintFillingDark=5, PaintFillingLight=6,
			PaintCrownLight=8, PaintDentureLight=12, PaintSealant=13;
		//FeeScheduleType
		public const int FeeSchedNormal=0, FeeSchedCoPay=1, FeeSchedOutNetwork=2;
		//ScheduleType
		public const int SchedPractice=0, SchedProvider=1, SchedBlockout=2;
		//PayPlanChargeType
		public const int PpcDebit=0, PpcCredit=1;
		//PayPlanFrequency
		public const int PayFreqMonthly=3;
		//PayPlanLinkType
		public const int PplProcedure=2;
		//PayPlanDebitTypes
		public const int PpdPrincipal=1, PpdInterest=2;
		//AccountType
		public const int AcctAsset=0, AcctLiability=1, AcctEquity=2, AcctIncome=3, AcctExpense=4;
		//TaskStatusEnum, TaskObjectType, TaskDateType
		public const int TaskNew=0, TaskViewed=1, TaskDone=2, TaskObjNone=0, TaskObjPatient=1, TaskObjAppointment=2;
		//ImageType
		public const int ImgDocument=0, ImgRadiograph=1, ImgPhoto=2, ImgFile=3;
		//EnumEFormType
		public const int EFormPatientForm=0, EFormMedicalHistory=1, EFormConsent=2;
		//EnumEFormStatus. 25.2 removed Imported (3) and UnmatchedToPatient (4) from the list; old rows keep them and load as Filled.
		public const int EFormStatusShowInEClipboard=1, EFormStatusFilled=2, EFormStatusLegacyImported=3;
		//EnumEFormFieldType: 0-7 from 24.3; PatientList 8 (25.1 database documentation), InsPick 9 (25.2).
		public const int EfTextField=0, EfLabel=1, EfDateField=2, EfCheckBox=3, EfRadioButtons=4, EfSigBox=5, EfPageBreak=6,
			EfMedicationList=7, EfPatientList=8, EfInsPick=9;
		//SheetTypeEnum: PaymentPlan 19 (24.3), Check 28 (25.3 database documentation), Consent 6.
		public const int SheetConsent=6, SheetPaymentPlan=19, SheetCheck=28;
		//SheetFieldType
		public const int SfOutputText=0, SfInputField=1, SfStaticText=2, SfCheckBox=8, SfSigBox=9, SfSigBoxPractice=16;
		//GuardianRelationship, 25.1 numbering: Father moved from 0 to 6, Sitter from 6 to 23, None is 0 (eForms only).
		public const int GuardianMother=1, GuardianFather=6, GuardianGrandmother=5, GuardianSitter=23;
		//StatementMode: API 4 is new in 25.3.
		public const int StmtMail=0, StmtInPerson=1, StmtEmail=2, StmtElectronic=3, StmtApi=4;
		//EnumImageAnnotVendor: BetterDiagnostics 2 is new in 24.4.
		public const int AnnotOpenDental=0, AnnotPearl=1, AnnotBetterDiagnostics=2;
		//EnumCodeGroupFixed
		public const int CgNone=0, CgBW=1, CgPanoFMX=2, CgExam=3, CgPerio=4, CgProphy=5, CgSRP=6, CgFMDebride=7, CgFluoride=8, CgSealant=9;
		//HistAppointmentAction
		public const int HistCreated=0, HistChanged=1, HistMissed=2;
		//CommItemMode, CommSentOrReceived
		public const int CommModeEmail=1, CommModeMail=2, CommModePhone=3, CommModeText=5, CommSent=1, CommReceived=2;
		//TreatPlanStatus
		public const int TPSaved=0, TPActive=1;
		//AsapCommFKeyType, AsapRSVPStatus
		public const int AsapScheduledAppt=1, AsapUnscheduledAppt=2, AsapPendingRsvp=2, AsapDeclined=7;
		//VerifyTypes
		public const int VerifyInsuranceBenefit=1, VerifyPatientEnrollment=2;
		//DeletedObjectType
		public const int DelAppointment=0, DelRecallPatNum=2, DelMedicationPat=8;
		//EnumPermType: 24.3 values, plus 264 to 271 from the 24.4-25.2 database documentation.
		public const int PermPaymentCreate=15, PermAdjustmentCreate=17, PermProcComplCreate=23, PermAppointmentCreate=25, PermAppointmentEdit=27,
			PermAccountingCreate=32, PermTreatPlanEdit=38, PermPatientEdit=108, PermPayPlanEdit=149, PermImageSignatureCreate=264,
			PermSignedImageEdit=265, PermBlockoutsFlagged=266, PermPayPlanUnlock=267, PermProcTPEditFee=270, PermEFormImport=271;
		//DefCat
		public const int DefAdjTypes=1, DefApptConfirmed=2, DefBillingTypes=4, DefPaymentTypes=10, DefProcCodeCats=11, DefRecallUnschedStatus=13,
			DefImageCats=18, DefBlockoutTypes=25, DefCommLogTypes=27, DefPaySplitUnearnedType=29, DefInsurancePaymentType=32,
			DefTaskPriorities=33, DefProviderSpecialties=35, DefInsuranceVerificationStatus=38, DefPayPlanCategories=47, DefTaskCategories=54;
		//ProviderStatus
		public const int ProvActive=0;
		//EnumUserStatus (chatuserod). The 25.3 document names Available, Away and DoNotDisturb without numbers; 0-2 is an assumption.
		public const int ChatUserAvailable=0, ChatUserAway=1, ChatUserDoNotDisturb=2;
	}
}
