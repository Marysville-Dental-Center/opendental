using System;
using System.ComponentModel;

namespace OpenPmsDentalBusiness{

	///<summary>Each row is a bridge to an outside program, frequently an imaging program.  Most of the bridges are hard coded, and simply need to be enabled.  But user can also add their own custom bridge.</summary>
	[Serializable]
	public class Program:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long ProgramNum;
		///<summary>Unique name for built-in program bridges. Not user-editable. enum ProgramName</summary>
		public string ProgName;
		///<summary>Description that shows.</summary>
		public string ProgDesc;
		///<summary>True if enabled.</summary>
		public bool Enabled;
		///<summary>The path of the executable to run or file to open. Text since 255 is the largest VARCHAR supported and some softwares have long paths that can easily exceed 255 characters.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string Path;
		///<summary>Some programs will accept command line arguments. Text since 255 is the largest VARCHAR supported and some softwares have long command line arguments that can easily exceed 255 characters.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string CommandLine;
		///<summary>Notes about this program link. Peculiarities, etc.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string Note;
		///<summary>If this is a Plugin, then this is the filename of the dll, including the extension.  The dll must be located in the application directory.</summary>
		public string PluginDllName;
		///<summary>If no image, then will be an empty string.  In this case, the bitmap will be null when loaded from the database.
		///Must be a 22 x 22 image, and thus needs (width) x (height) x (depth) = 22 x 22 x 4 = 1936 bytes.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string ButtonImage;
		/// <summary>For custom program links only.  Stores the template of a file to be generated when launching the program link.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string FileTemplate;
		/// <summary>For custom program links only.  Stores the path of a file to be generated when launching the program link.</summary>
		public string FilePath;
		///<summary>No longer used. Open Dental HQ set this to disable a program link for all customers (WebServiceHq.EnableAdditionalFeatures()). That kill switch was removed; the column is kept for schema compatibility and ignored.</summary>
		public bool IsDisabledByHq;
		///<summary>No longer used. Held Open Dental HQ's message for a program link it disabled (HqProgram.Download()). Kept for schema compatibility and ignored.</summary>
		public string CustErr;

		public Program Copy(){
			return (Program)this.MemberwiseClone();
		}

	}

	///<summary>This enum is stored in the database as strings rather than as numbers, so we can do the order alphabetically and we can change it whenever we want.</summary>
	public enum ProgramName {
		None,
		ActeonImagingSuite,
		Adstra,
		AiDental,
		Apixia,
		Apteryx,
		AudaxCeph,
		///<summary>Avalara Inc.</summary>
		AvaTax,
		BencoPracticeManagement,
		///<summary>PDMP program. Formerly Appriss.</summary>
		Bamboo,
		BioPAK,
		BolaAI,
		///<summary>Newer version of MediaDent. Uses OLE COM interface.</summary>
		CADI,
		CallFire,
		Camsight,
		CaptureLink,
		CareCredit,
		Carestream,
		CentralDataStorage,
		Cerec,
		CleaRay,
		CliniView,
		ClioSoft,
		DBSWin,
		DemandForce,
		DentalEye,
		DentalIntel,
		DentalStudio,
		DentalTekSmartOfficePhone,
		DentForms,
		///<summary>Removed on 2026-11-07 (ProgramLinksDiscontinued.ListProgramNamesRemoved). Kept: program rows name it.</summary>
		DentX,
		Dexis,
		///<summary>Removed on 2026-11-07 (ProgramLinksDiscontinued.ListProgramNamesRemoved). Kept: program rows name it.</summary>
		DexisIntegrator,
		Digora,
		Dimaxis,
		Divvy,
		Dolphin,
		DrCeph,
		Dropbox,
		DXCPatientCreditScore,
		Dxis,
		EasyNotesPro,
		eClinicalWorks,
		EdgeExpress,
		///<summary>electronic Rx.</summary>
		eRx,
		EvaSoft,
		EzDenti,
		Ez3Di,
		FHIR,
		FloridaProbe,
		Guru,
		HandyDentist,
		HdxWill,
		HouseCalls,
		IAP,
		iCat,
		iDixel,
		///<summary>FxImage was renamed to PatientGallery.</summary>
		ImageFX,
		iRYS,
		JazzClassicCapture,
		JazzClassicExamView,
		JazzClassicPatientUpdate,
		Lightyear,
		MediaDent,
		MeditLink,
		/// <summary>Obsolete. All instances may be removed as you run across them. Midway was acquired by Henry Schein https://investor.henryschein.com/news-releases/news-release-details/henry-schein-acquires-midway-dental-supply.</summary>
		Midway,
		MiPACS,
		Mountainside,
		NewTomNNT,
		Office,
		One2,
		///<summary>Obsolete. All instances may be removed as you run across them.  Also see Programs.UsingOrion.</summary>
		Orion,
		OrthoCAD,
		OrthoInsight3d,
		OrthoPlex,
		Oryx,
		Owandy,
		PandaPerio,
		PandaPerioAdvanced,
		PayConnect,
		PaySimple,
		///<summary>FxImage was renamed to PatientGallery.</summary>
		PatientGallery,
		Patterson,
		Pearl,
		PerioPal,
		PDMP,
		Pixel,
		Podium,
		PORTRAY,
		PracticeBooster,
		PracticeByNumbers,
		PracticeWebReports,
		PreXionAcquire,
		PreXionViewer,
		Progeny,
		///<summary>Paperless Technology.</summary>
		PT,
		///<summary>Paperless Technology.</summary>
		PTupdate,
		QuickBooksOnline,
		RapidCall,
		///<summary>New version of SMARTDent.</summary>
		RayBridge,
		RayMage,
		Romexis,
		Scanora,
		Schick,
		SFTP,
		Shining3D,
		Sirona,
		SMARTDent,
		Sopro,
		SOTACloud,
		SteriSimple,
		ThreeShape,
		TigerView,
		Transworld,
		Triana,
		Trojan,
		TrojanExpressCollect,
		Trophy,
		TrophyEnhanced,
		Tscan,
		UAppoint,
		///<summary>Removed on 2026-11-07 (ProgramLinksDiscontinued.ListProgramNamesRemoved). Kept: program rows name it.</summary>
		Vipersoft,
		visOra,
		VisionX,
		VistaDent,
		VixWin,
		VixWinBase36,
		VixWinBase41,
		VixWinNumbered,
		VixWinOld,
		Xcharge,
		XDR,
		XVWeb,
		ZImage,
		//Vendor-direct program links, appended (not alphabetical) so they're easy to find. Their rows are inserted at runtime with Enabled=0
		//(Programs.InsertIfMissing); there is no ConvertDatabases step.
		///<summary>Microsoft 365 email through the office's own Entra app registration.</summary>
		Microsoft365Email,
		///<summary>Gmail through the office's own Google OAuth client.</summary>
		GmailApi,
		///<summary>USPS Addresses API v3 with the office's own consumer key.</summary>
		USPSAddresses,
		///<summary>PostcardMania (PCM Integrations) DirectMail API v3 with the office's own API key.</summary>
		PostcardMania,
		//Open Dental 25.1 and 25.2 program links (V-BRIDGE1), appended so existing values keep their numbers. Rows are inserted at runtime with
		//Enabled=0 (OpenPmsDentalBusiness/VendorDirect/ProgramLinks). The names are our proposal; confirm them against program.ProgName in the office data.
		///<summary>Overjet: opens the vendor's web app in the browser (Open Dental 25.1).</summary>
		Overjet,
		///<summary>DTX Studio: opens the patient in DTX Studio Clinic through DTXsync.exe (Open Dental 25.2).</summary>
		DTXStudio,
		///<summary>DTX Studio Acquire: opens the patient in DTX Studio Clinic with acquisition ready (Open Dental 25.2).</summary>
		DTXStudioAcquire,
		//Open Dental 25.4 program links (V-BRIDGE2), appended so existing values keep their numbers. Rows are inserted at runtime with Enabled=0.
		//The names are the fork's: Open Dental's program.ProgName values for them are not public.
		///<summary>Clerri membership plans (25.4): a Family module button that opens the office's Clerri dashboard. Launch only, no data sync.</summary>
		Clerri,
		///<summary>Clever One by VATECH (25.4), the newer VATECH imaging program. The EzDenti link stays.</summary>
		CleverOne,
		//Global Payments V2 (V-GP), appended after the V-BRIDGE2 links when both were merged (port/int-13). program.ProgName and
		//payterminal.CCIntegration store the name, not the number, so the order is free.
		///<summary>Global Payments V2 (Open Dental 25.4): card terminals through Global Payments' cloud terminal service, called directly with the
		///office's own Global Payments API app. "MeetInTheCloud" is the internal name the published 25.4 database documents give this integration
		///(CreditCardSource.MeetInTheCloudTerminal, computerpref.CreditCardTerminalId); the program row's name itself is not published (INFERRED).
		///The UI says "Global Payments V2".</summary>
		MeetInTheCloud,
		//Open Dental 26.2 program links (P262-LINKS), appended so existing values keep their numbers. Rows are inserted at runtime with Enabled=0
		//(OpenPmsDentalBusiness/VendorDirect/ProgramLinks). The names are the fork's: Open Dental's program.ProgName values are not public (INFERRED).
		///<summary>Allied Star Connect (26.2): writes the selected patient to a .json info file, then starts Alliedstar's Connect program.</summary>
		AlliedstarConnect,
		///<summary>OrthoComposite (26.2): an imaging program from the Microsoft Store, started with the patient's name and birthdate.</summary>
		OrthoComposite
	}

	


}










