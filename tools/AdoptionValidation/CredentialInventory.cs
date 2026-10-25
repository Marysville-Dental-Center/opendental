// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using SyntheticData.Infrastructure;

namespace AdoptionValidation {
	///<summary>Lists the stored passwords and keys of a database that the fork cannot read, so they must be re-entered after adoption (decoupling plan
	///ground rule 3; CodeBase/Crypto/README.md, "Migrating a database from Open Dental"). Open Dental encrypted them with its proprietary key; the
	///fork's Secrets only reads its own "osd1$" format. Read-only, and it reports where a secret is, never its value. It runs on any local database,
	///including a restored office copy.</summary>
	public static class CredentialInventory {
		///<summary>The fork's ciphertext prefix (CodeBase.Secrets.Prefix).</summary>
		public static string ForkPrefix {
			get { return CodeBase.Secrets.Prefix; }
		}

		///<summary>Preferences that hold passwords the fork decrypts with Secrets (archive, reporting-server and read-only-server MySQL passwords).
		///</summary>
		public static readonly string[] ArrayPasswordPrefs={ "ArchivePassHash","ReportingServerMySqlPassHash","ReadOnlyServerMySqlPassHash" };

		public class Item {
			///<summary>Where to re-enter it, in plain words.</summary>
			public string Location;
			///<summary>The table and row, for example "programproperty 1234 (XVWeb, Password, clinic 0)".</summary>
			public string Row;

			public override string ToString() {
				return Location+" ["+Row+"]";
			}
		}

		public static List<Item> Run(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			db.Use(database);
			SchemaInfo schema=SchemaInfo.Load(db,database);
			List<Item> listItems=new List<Item>();
			string notFork="<>'' AND {0} NOT LIKE "+SqlLiteral.Quote(ForkPrefix+"%");
			if(schema.GetTable("programproperty")!=null && schema.GetTable("program")!=null) {
				foreach(object[] row in db.Rows("SELECT pp.ProgramPropertyNum,IFNULL(p.ProgName,''),pp.PropertyDesc,pp.ClinicNum,IFNULL(p.Enabled,0) "
					+"FROM programproperty pp LEFT JOIN program p ON p.ProgramNum=pp.ProgramNum "
					+"WHERE (pp.PropertyDesc LIKE '%Password%' OR pp.IsMasked=1) AND IFNULL(pp.PropertyValue,'')"+string.Format(notFork,"pp.PropertyValue")
					+" ORDER BY p.ProgName,pp.PropertyDesc,pp.ClinicNum"))
				{
					listItems.Add(new Item {
						Location="Program link "+row[1]+(Convert.ToInt64(row[4])==0 ? " (disabled)" : "")+": "+row[2]+(Convert.ToInt64(row[3])==0 ? "" : ", clinic "+row[3]),
						Row="programproperty "+row[0],
					});
				}
			}
			if(schema.GetTable("clearinghouse")!=null) {
				foreach(object[] row in db.Rows("SELECT ClearinghouseNum,Description,ClinicNum FROM clearinghouse WHERE Password"+string.Format(notFork,"Password")
					+" ORDER BY Description,ClinicNum"))
				{
					listItems.Add(new Item { Location="Clearinghouse "+row[1]+(Convert.ToInt64(row[2])==0 ? "" : ", clinic "+row[2])+": password",
						Row="clearinghouse "+row[0] });
				}
			}
			if(schema.GetTable("preference")!=null) {
				foreach(object[] row in db.Rows("SELECT PrefNum,PrefName FROM preference WHERE PrefName IN ("
					+string.Join(",",ArrayPasswordPrefs.Select(x => SqlLiteral.Quote(x)))+") AND ValueString"+string.Format(notFork,"ValueString")+" ORDER BY PrefName"))
				{
					listItems.Add(new Item { Location="Preference "+row[1],Row="preference "+row[0] });
				}
			}
			if(schema.GetTable("hl7def")!=null && schema.GetTable("hl7def").HasColumn("SftpPassword")) {
				foreach(object[] row in db.Rows("SELECT HL7DefNum,Description FROM hl7def WHERE SftpPassword"+string.Format(notFork,"SftpPassword")
					+" ORDER BY Description"))
				{
					listItems.Add(new Item { Location="HL7 definition "+row[1]+": SFTP password",Row="hl7def "+row[0] });
				}
			}
			if(schema.GetTable("ebill")!=null && schema.GetTable("ebill").HasColumn("ElectPassword")) {
				foreach(object[] row in db.Rows("SELECT EbillNum,ClinicNum FROM ebill WHERE ElectPassword"+string.Format(notFork,"ElectPassword")+" ORDER BY ClinicNum")) {
					listItems.Add(new Item { Location="Electronic billing (eBill)"+(Convert.ToInt64(row[1])==0 ? "" : ", clinic "+row[1])+": password",
						Row="ebill "+row[0] });
				}
			}
			if(schema.GetTable("userodpref")!=null && schema.GetTable("program")!=null) {
				//UserOdFkeyType.ProgramPassword (per-user bridge passwords, for example Oryx).
				long fkeyTypeProgramPassword=(long)OpenDentBusiness.UserOdFkeyType.ProgramPassword;
				foreach(object[] row in db.Rows("SELECT u.UserOdPrefNum,IFNULL(p.ProgName,''),u.UserNum FROM userodpref u LEFT JOIN program p ON p.ProgramNum=u.Fkey "
					+"WHERE u.FkeyType="+fkeyTypeProgramPassword+" AND u.ValueString"+string.Format(notFork,"u.ValueString")+" ORDER BY p.ProgName,u.UserNum"))
				{
					listItems.Add(new Item { Location="Program link "+row[1]+": password of user "+row[2],Row="userodpref "+row[0] });
				}
			}
			return listItems;
		}
	}
}
