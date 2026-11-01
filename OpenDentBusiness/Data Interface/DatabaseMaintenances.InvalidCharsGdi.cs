// Business side of the Database Maintenance tool "Invalid Characters (GDI+)" (Open Dental 25.2, backlog package B-DBM). Open Dental publishes only
// the tool's name; the target columns and character set are our design (docs/specs/features-25.2.md, docs/port-notes/B-DBM.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using DataConnectionBase;

namespace OpenDentBusiness {
	public partial class DatabaseMaintenances {
		///<summary>How many rows of one column the tool reads and fixes at a time.</summary>
		private const int INVALID_CHARS_BATCH_SIZE=500;

		///<summary>The columns the Invalid Characters (GDI+) tool scans, in display order. A column whose rows can be signed carries the SQL condition
		///that marks a signed row: the signature covers the value, so removing a character would break it, and those rows are only reported.
		///</summary>
		public static List<DbmInvalidCharTarget> GetInvalidCharTargets() {
			Meth.NoCheckMiddleTierRole();
			List<DbmInvalidCharTarget> listTargets=new List<DbmInvalidCharTarget>();
			string[] arrayPatientColumns=new string[] { "LName","FName","MiddleI","Preferred","Address","Address2","City","State","Zip",
				"AddrNote","FamFinUrgNote","MedUrgNote","ApptModNote" };
			for(int i=0;i<arrayPatientColumns.Length;i++) {
				listTargets.Add(new DbmInvalidCharTarget("patient",arrayPatientColumns[i],"PatNum",""));
			}
			listTargets.Add(new DbmInvalidCharTarget("appointment","Note","AptNum",""));
			listTargets.Add(new DbmInvalidCharTarget("appointment","ProcDescript","AptNum",""));
			//Procedure notes are kept in procnote, one row per edit; a signed row's signature covers its note. Since 26.1 a group note's second
			//signature covers it too (M2-NOTES); Signature2 is NULL on rows written before the column existed.
			listTargets.Add(new DbmInvalidCharTarget("procnote","Note","ProcNoteNum","procnote.Signature!='' OR IFNULL(procnote.Signature2,'')!=''"));
			listTargets.Add(new DbmInvalidCharTarget("commlog","Note","CommlogNum","commlog.Signature!=''"));
			listTargets.Add(new DbmInvalidCharTarget("definition","ItemName","DefNum",""));
			//A sheet signature covers every field value of the sheet (SigBox.GetSignatureKeySheets), and a signature box holds the signature itself.
			string sigTypes=POut.Int((int)SheetFieldType.SigBox)+","+POut.Int((int)SheetFieldType.SigBoxPractice);
			listTargets.Add(new DbmInvalidCharTarget("sheetfield","FieldValue","SheetFieldNum","sheetfield.FieldType IN("+sigTypes+") "
				+"OR sheetfield.SheetNum IN(SELECT sheetfieldsig.SheetNum FROM sheetfield sheetfieldsig "
				+"WHERE sheetfieldsig.FieldType IN("+sigTypes+") AND sheetfieldsig.FieldValue!='')"));
			listTargets.Add(new DbmInvalidCharTarget("wikipage","PageContent","WikiPageNum",""));
			return listTargets;
		}

		///<summary>Counts, for each named column (DbmInvalidCharTarget.Label), the rows and characters the GDI+ rule finds, how many of those rows are
		///signed, and returns up to maxPreviews affected values in total, with the bad characters shown as [U+XXXX]. Changes nothing. Unknown labels
		///are ignored.</summary>
		public static List<DbmInvalidCharResult> InvalidCharsGdiScan(List<string> listLabels,int maxPreviews=50) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DbmInvalidCharResult>>(MethodBase.GetCurrentMethod(),listLabels,maxPreviews);
			}
			return RunInvalidCharsGdi(listLabels,isFix:false,maxPreviews:maxPreviews);
		}

		///<summary>Removes the characters of the GDI+ rule from every unsigned row of the named columns, one row at a time by primary key, and makes one
		///Setup security log entry with the counts. Signed rows are never changed; they are counted in CountRowsSigned. Returns the counts per column.
		///</summary>
		public static List<DbmInvalidCharResult> InvalidCharsGdiFix(List<string> listLabels) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DbmInvalidCharResult>>(MethodBase.GetCurrentMethod(),listLabels);
			}
			List<DbmInvalidCharResult> listResults=RunInvalidCharsGdi(listLabels,isFix:true,maxPreviews:0);
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,GetInvalidCharsGdiLogText(listResults));
			return listResults;
		}

		///<summary>The security log text for one Fix run: the totals, then one line per column that had anything.</summary>
		public static string GetInvalidCharsGdiLogText(List<DbmInvalidCharResult> listResults) {
			Meth.NoCheckMiddleTierRole();
			string logText=Lans.g("FormDatabaseMaintenance","Database Maintenance tool Invalid Characters (GDI+) removed")+" "
				+listResults.Sum(x => x.CountChars)+" "+Lans.g("FormDatabaseMaintenance","characters from")+" "+listResults.Sum(x => x.CountRowsFixed)+" "
				+Lans.g("FormDatabaseMaintenance","values. Signed values left unchanged:")+" "+listResults.Sum(x => x.CountRowsSigned)+".";
			List<DbmInvalidCharResult> listResultsWithRows=listResults.FindAll(x => x.CountRowsFixed>0 || x.CountRowsSigned>0);
			for(int i=0;i<listResultsWithRows.Count;i++) {
				logText+="\r\n"+listResultsWithRows[i].Label+": "+Lans.g("FormDatabaseMaintenance","fixed")+" "+listResultsWithRows[i].CountRowsFixed
					+", "+Lans.g("FormDatabaseMaintenance","signed")+" "+listResultsWithRows[i].CountRowsSigned;
			}
			return logText;
		}

		private static List<DbmInvalidCharResult> RunInvalidCharsGdi(List<string> listLabels,bool isFix,int maxPreviews) {
			List<DbmInvalidCharTarget> listTargets=GetInvalidCharTargets().FindAll(x => listLabels.Contains(x.Label));
			List<DbmInvalidCharResult> listResults=new List<DbmInvalidCharResult>();
			int countPreviews=0;
			for(int i=0;i<listTargets.Count;i++) {
				DbmInvalidCharTarget target=listTargets[i];
				DbmInvalidCharResult result=new DbmInvalidCharResult();
				result.Label=target.Label;
				string signedExpression="0";
				if(!string.IsNullOrEmpty(target.SignedCondition)) {
					signedExpression="("+target.SignedCondition+")";
				}
				long priKeyLast=0;//Primary keys start at 1.
				while(true) {
					string command="SELECT "+target.TableName+"."+target.KeyName+" PriKey,"+target.TableName+"."+target.ColumnName+" Value,"
						+signedExpression+" IsSigned "
						+"FROM "+target.TableName+" "
						+"WHERE "+target.TableName+"."+target.KeyName+">"+POut.Long(priKeyLast)+" "
						+"AND "+DbmTextCleaner.GetSqlHasGdiInvalidChars(target.TableName+"."+target.ColumnName)+" "
						+"ORDER BY "+target.TableName+"."+target.KeyName+" LIMIT "+INVALID_CHARS_BATCH_SIZE;
					DataTable table=Db.GetTable(command);
					for(int j=0;j<table.Rows.Count;j++) {
						long priKey=PIn.Long(table.Rows[j]["PriKey"].ToString());
						priKeyLast=priKey;
						string value=PIn.String(table.Rows[j]["Value"].ToString());
						int countChars=DbmTextCleaner.CountGdiInvalidChars(value);
						if(countChars==0) {
							continue;
						}
						bool isSigned=PIn.Bool(table.Rows[j]["IsSigned"].ToString());
						if(isSigned) {
							result.CountRowsSigned++;
						}
						else {
							result.CountRows++;
							result.CountChars+=countChars;
							if(isFix) {
								UpdateTextColumn(target.TableName,target.ColumnName,target.KeyName,priKey,DbmTextCleaner.RemoveGdiInvalidChars(value));
								result.CountRowsFixed++;
							}
						}
						if(countPreviews<maxPreviews) {
							DbmInvalidCharPreview preview=new DbmInvalidCharPreview();
							preview.Label=target.Label;
							preview.PriKey=priKey;
							preview.IsSigned=isSigned;
							preview.CountChars=countChars;
							preview.ValueShown=DbmTextCleaner.ShowGdiInvalidChars(value);
							result.ListPreviews.Add(preview);
							countPreviews++;
						}
					}
					if(table.Rows.Count<INVALID_CHARS_BATCH_SIZE) {
						break;
					}
				}
				listResults.Add(result);
			}
			return listResults;
		}
	}

	///<summary>A column scanned by the Invalid Characters (GDI+) tool. Names come from code, never from the user.</summary>
	[Serializable]
	public class DbmInvalidCharTarget {
		public string TableName;
		public string ColumnName;
		///<summary>The table's primary key column.</summary>
		public string KeyName;
		///<summary>SQL condition on TableName that is true for a signed row, whose value must never change. Empty when the column is never signed.
		///</summary>
		public string SignedCondition;

		public DbmInvalidCharTarget() {
		}

		public DbmInvalidCharTarget(string tableName,string columnName,string keyName,string signedCondition) {
			TableName=tableName;
			ColumnName=columnName;
			KeyName=keyName;
			SignedCondition=signedCondition;
		}

		///<summary>table.column, the name the window shows and the scan and fix methods take.</summary>
		public string Label {
			get {
				return TableName+"."+ColumnName;
			}
		}
	}

	///<summary>What the Invalid Characters (GDI+) tool found in, or fixed in, one column.</summary>
	[Serializable]
	public class DbmInvalidCharResult {
		///<summary>table.column.</summary>
		public string Label;
		///<summary>Unsigned rows with at least one invalid character: the rows Fix changes.</summary>
		public long CountRows;
		///<summary>Invalid characters in those unsigned rows (removed, after a fix).</summary>
		public long CountChars;
		///<summary>Signed rows with invalid characters. They are reported and never changed.</summary>
		public long CountRowsSigned;
		///<summary>Rows changed by a fix. Always 0 after a scan.</summary>
		public long CountRowsFixed;
		public List<DbmInvalidCharPreview> ListPreviews=new List<DbmInvalidCharPreview>();
	}

	///<summary>One affected value, for the preview grid.</summary>
	[Serializable]
	public class DbmInvalidCharPreview {
		///<summary>table.column.</summary>
		public string Label;
		public long PriKey;
		public bool IsSigned;
		public int CountChars;
		///<summary>A piece of the value with each invalid character shown as [U+XXXX].</summary>
		public string ValueShown;
	}
}
