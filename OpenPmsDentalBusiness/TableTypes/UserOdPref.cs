using System;
using System.Collections;
using System.Drawing;

namespace OpenPmsDentalBusiness {
	///<summary></summary>
	[Serializable]
	[CrudTable(IsSynchable=true,HasBatchWriteMethods=true)]
	public class UserOdPref:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey = true)]
		public long UserOdPrefNum;
		///<summary>FK to userod.UserNum.</summary>
		public long UserNum;
		///<summary>Foreign key to a table associated with FkeyType.  Can be 0 if the user preference does not need a foreign key.</summary>
		public long Fkey;
		///<summary>Enum:UserOdFkeyType Specifies which flag is overridden for the specified definition, since an individual definition can have multiple flags.</summary>
		public UserOdFkeyType FkeyType;
		///<summary>Used to hold the override, which might be a simple primitive value, a comma separated list, or a complex document in xml.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string ValueString;
		///<summary>FK to Clinic.ClinicNum.</summary>
		public long ClinicNum;

		///<summary></summary>
		public UserOdPref Clone() {
			return (UserOdPref)this.MemberwiseClone();
		}
	}

	///<summary>These FKey Types are to be used as an identifier for what table the Fkey column is associated to, or what type of override it is.
	///Not always associated to a database table.  Example:  TaskCollapse,SuppressLogOffMessage,AcctProcBreakdown.
	///Do not reorder items, since they are numbers in the db.</summary>
	public enum UserOdFkeyType {
		///<summary>0 - Imaging module expanded categories.  Presence of a row means expanded.  Absence means collapsed.  No valuestring needed.</summary>
		ImageCategoryExpanded,
		///<summary>1</summary>
		ClinicLast,
		///<summary>2 - Wiki home pages use ValueString to store the name of the wiki page instead of Fkey due to how FormWiki loads pages.</summary>
		WikiHomePage,
		///<summary>3 - ValueString will be a comma delimited list of DefNums for the last expanded categories for the user.  When FormAutoNotes loads,
		///these categories will be expanded again.</summary>
		AutoNoteExpandedCats,
		///<summary>4 - Controls whether tasks will be collapsed or not by default</summary>
		TaskCollapse,
		///<summary>5 - When FormCommItem is in Persistent mode, clear the note text box after the user creates a commlog.</summary>
		CommlogPersistClearNote,
		///<summary>6 - When FormCommItem is in Persistent mode, clear the End text box after the user creates a commlog.</summary>
		CommlogPersistClearEndDate,
		///<summary>7 - When FormCommItem is in Persistent mode, update the Date / Time text box with NOW() whenver the patient changes.</summary>
		CommlogPersistUpdateDateTimeWithNewPatient,
		///<summary>8 - Whether or not to display just the currently selected exam in the Perio Chart. When true, this shows values, both probing and
		///non-probing, from the current exam only (Open Dental 26.2's description). The Perio Chart's "Show previous exams" check box is its opposite
		///(PerioMeasures.GetShowPreviousExams).</summary>
		PerioCurrentExamOnly,
		///<summary>9 - Text message grouping preference. 0 - None; 1 - By Patient;</summary>
		SmsGroupBy,
		///<summary>10 - Stores a TaskListNum that the corresponding user wants to block all pop ups from.</summary>
		TaskListBlock,
		///<summary>11 - Stores user specific values for programs.  Currently only used in DoseSpot for the DoseSpot User ID.</summary>
		Program,
		///<summary>12</summary>
		SuppressLogOffMessage,
		///<summary>13 - Sets the default state of the Account Module "Show Proc Breakdowns" checkbox.</summary>
		AcctProcBreakdown,
		///<summary>14 - Stores user specific username for programs.</summary>
		ProgramUserName,
		///<summary>15 - Stores user specific password for programs.</summary>
		ProgramPassword,
		///<summary>16 - Stores user specific dashboard to open on load. FKey points to the SheetDef.SheetDefNum that the user last had open.</summary>
		Dashboard,
		///<summary>17 - Deprecated. </summary>
		UserTheme,
		///<summary>18 - Stores the Dynamic Chart Layout SheetDef.SheetDefNum selected by a user.</summary>
		DynamicChartLayout,
		///<summary>19 - Whether or not to set the perio auto advance to custom.</summary>
		PerioAutoAdvanceFacialsFirst,
		///<summary>20 - Stores the value (in minutes) of when the user should be auto logged off.</summary>
		LogOffTimerOverride,
		///<summary>21 - Whether or not we check the "Show received" checkbox when loading the supply order history.</summary>
		ReceivedSupplyOrders,
		///<summary>22 - Color defs can be used for different proc statuses when the user's provider doesn't match the procedure's provider.</summary>
		ToothChartUsesDiffColorByProv,
		///<summary>23 - Whether to show automated commlogs in the account module, chart module, and appointment edit window.</summary>
		ShowAutomatedCommlog,
		///<summary>24 - Preference that indicates whether stack traces are to be logged by the query monitor or not. If exists 0 or 1 is stored in ValueString</summary>
		QueryMonitorHasStackTraces,
		///<summary>25 - Preference only applies to the wiki search. Set and unset only in the wiki search window. Box defaults to checked if this preference is not set.</summary>
		WikiSearchIncludeContent,
		///<summary>26 - Sets whether tasks popups that are blocked still play the sound notification. If true, sound will play. If false, sound will be blocked as well.</summary>
		TaskBlockedMakeSound,
		///<summary>27 - Stores the width of the left Image Selector (tree) of the Imaging module in the ValueString. Does not store expanded/collapsed, so this width would be the expanded width. There is no limit. Does not use FKey.</summary>
		ImageSelectorWidth,
		///<summary>28 - Reserved (Open Dental 25.3). Open Dental HQ only: the default office map for a user. This program never writes it.</summary>
		[CodeBase.IsODHQ]
		DefaultMapSetting,
		///<summary>29 - Reserved (Open Dental 25.4). Open Dental HQ only: a user's default testing team in the Job Manager. This program never writes it.</summary>
		[CodeBase.IsODHQ]
		DefaultTestingTeamJM,
		//Open Dental's published documents have no 30 (26.2 goes from 29 to 31).
		///<summary>31 - Open Dental 26.2 (userodpref.FkeyType, OpenDentalDiffDocumentation26-2.xml): whether the Perio Chart shows the probing values
		///of the selected exam only. When true, only the current exam's probing values show. The Perio Chart's "Show previous probing values" check
		///box is its opposite (PerioMeasures.GetShowPreviousProbing).</summary>
		PerioShowCurrentProbingOnly=31,
		///<summary>114 - Fork only, not an Open Dental value. Open Dental's numbers end at 29 (25.4), so fork values sit at 100 and up, inside the
		///tinyint column (at most 127). 114 follows the "14" of backlog package B-EF's reserved migration range (2027001401-2027001499), to keep it
		///apart from other packages' fork values. The Patient Forms window's Show filter and sort order for this user, in ValueString like
		///"Show=0;Sort=0" (see PatientFormsList.SerializeView). Does not use FKey.</summary>
		PatientFormsView=114,
	}
}