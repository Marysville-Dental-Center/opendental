using System;

namespace OpenDentBusiness {
	///<summary>Stand-in for Open Dental's hosted Web Forms service (SheetsSynch.asmx). Web Forms were an Open Dental hosted eService, so every
	///method throws HqNotAvailableException without making a network call.</summary>
	public class SheetsSynchNoHq:ISheetsSynch {
		public string SetPreferences(string officeData) { throw new HqNotAvailableException(); }
		public string GetPreferences(string officeData) { throw new HqNotAvailableException(); }
		public string GetWebFormSheetIDs(string officeData) { throw new HqNotAvailableException(); }
		public string GetWebFormSheets(string officeData) { throw new HqNotAvailableException(); }
		public string DeleteSheetData(string officeData) { throw new HqNotAvailableException(); }
		public string GetDentalOfficeID(string officeData) { throw new HqNotAvailableException(); }
		public string GetSheetDefAddress(string officeData) { throw new HqNotAvailableException(); }
		public string DownloadSheetDefs(string officeData) { throw new HqNotAvailableException(); }
		public string DeleteSheetDef(string officeData) { throw new HqNotAvailableException(); }
		public string UpLoadSheetDef(string officeData) { throw new HqNotAvailableException(); }
		public string UpdateSheetDef(string officeData) { throw new HqNotAvailableException(); }
		public string UploadSheetDefChunk(string officeData) { throw new HqNotAvailableException(); }
		public string UploadSheetDefFromFile(string officeData) { throw new HqNotAvailableException(); }
		public string UpdateSheetDefFromFile(string officeData) { throw new HqNotAvailableException(); }
		public string CheckWebFormsDb() { throw new HqNotAvailableException(); }
		public string GetRegistrationKeyID(string officeData) { throw new HqNotAvailableException(); }
		public string GetSheetsCount(string officeData) { throw new HqNotAvailableException(); }
	}
}
