// Vendor follow-up from public documentation (next/vendorpub-1): data step for the OrthoComposite program link (P262-LINKS, Open Dental 26.2
// item 45). Level 2026110801.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	///<summary>OrthoComposite's public Microsoft Store listing gives its command arguments (--lastname, --firstname, --dob), so a new OrthoComposite
	///program row gets OrthoCompositeLink.DefaultPatientArguments as its "Patient arguments" value. Rows inserted before that (by Setup > Program
	///Links in a build of the 26.2 integration, p262/links-1) hold a blank value, which the button refuses for a patient. This step gives those
	///rows the default once: a ClinicNum 0 "Patient arguments" row of an OrthoComposite program row that is blank (empty or white space only, what
	///the button treats as not set) gets the default; a value the office typed is never changed. The row's Note is replaced by
	///OrthoCompositeLink.Note only where it is still exactly the text p262/links-1 wrote (OrthoCompositePatientArguments.NoteBefore). A row without
	///the property gets it with the default from OrthoCompositeLink.EnsureProgram the next time Program Links opens. Without an OrthoComposite row
	///(every adopted Open Dental 25.3.48 database: this program inserts the row at runtime) nothing changes. Once the step ran, a blank value is the
	///office's own choice. A second run finds nothing to change.</summary>
	[ForkMigration(2026110801,"Fill the blank OrthoComposite patient arguments with its public arguments")]
	public class OrthoCompositePatientArgumentsStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			if(!context.TableExists("program") || !context.TableExists("programproperty")) {
				return;
			}
			foreach(string change in OrthoCompositePatientArguments.Run()) {
				context.Log(change);
			}
		}
	}

	///<summary>The rule of OrthoCompositePatientArgumentsStep: reads the OrthoComposite program rows (ProgName compared as MariaDB compares it: case and
	///trailing spaces ignored) and their ClinicNum 0 "Patient arguments" rows, and writes only the blank ones and a Note that is still the old
	///built-in text.</summary>
	public static class OrthoCompositePatientArguments {
		public const string PROG_NAME="OrthoComposite";
		///<summary>The Note p262/links-1 wrote into a new OrthoComposite row, when the office had to enter the arguments itself.</summary>
		public const string NoteBefore="Starts OrthoComposite for the selected patient. OrthoComposite finds the patient by first name, last name and birthdate. "
			+"Enter where OrthoComposite is in Path of file to open. Before the first use with a patient, enter the arguments from OrthoComposite's "
			+"instructions in the Patient arguments property, using the tokens [FName], [LName] and [Birthdate] (MM/dd/yyyy). Put quotes around a token "
			+"whose value can contain spaces, for example \"[LName]\". No other patient data is sent. The Command line field is added after the patient "
			+"arguments. Without a patient, OrthoComposite starts with the Command line only.";

		///<summary>True for the ProgName of the link, compared as the database compares it (utf8 general collation: case and trailing spaces ignored).
		///</summary>
		public static bool IsOrthoCompositeProgName(string progName) {
			return string.Equals((progName??"").TrimEnd(' '),PROG_NAME,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Fills the blank Patient arguments and replaces the old built-in Note, with plain UPDATE statements on those rows only. Returns the
		///lines for the fork migration log, in ProgramNum and then ProgramPropertyNum order; empty when nothing changed.</summary>
		public static List<string> Run() {
			List<string> listChanges=new List<string>();
			DataTable tablePrograms=Db.GetTable("SELECT ProgramNum,ProgName,Note FROM program ORDER BY ProgramNum");
			for(int i=0;i<tablePrograms.Rows.Count;i++) {
				if(!IsOrthoCompositeProgName(tablePrograms.Rows[i]["ProgName"].ToString())) {
					continue;
				}
				long programNum=PIn.Long(tablePrograms.Rows[i]["ProgramNum"].ToString());
				DataTable tableProperties=Db.GetTable("SELECT ProgramPropertyNum,PropertyValue FROM programproperty "
					+"WHERE ProgramNum="+POut.Long(programNum)+" AND ClinicNum=0 "
					+"AND PropertyDesc='"+POut.String(OrthoCompositeLink.PropertyDescs.PatientArguments)+"' "
					+"ORDER BY ProgramPropertyNum");
				for(int j=0;j<tableProperties.Rows.Count;j++) {
					if(!string.IsNullOrWhiteSpace(tableProperties.Rows[j]["PropertyValue"].ToString())) {
						continue;//The office's own value.
					}
					long programPropertyNum=PIn.Long(tableProperties.Rows[j]["ProgramPropertyNum"].ToString());
					Db.NonQ("UPDATE programproperty SET PropertyValue='"+POut.String(OrthoCompositeLink.DefaultPatientArguments)+"' "
						+"WHERE ProgramPropertyNum="+POut.Long(programPropertyNum));
					listChanges.Add("Set the blank Patient arguments of the OrthoComposite program link (ProgramNum "+programNum+", ProgramPropertyNum "
						+programPropertyNum+") to OrthoComposite's public arguments");
				}
				if(tablePrograms.Rows[i]["Note"].ToString()==NoteBefore) {
					Db.NonQ("UPDATE program SET Note='"+POut.String(OrthoCompositeLink.Note)+"' WHERE ProgramNum="+POut.Long(programNum));
					listChanges.Add("Replaced the built-in note of the OrthoComposite program link (ProgramNum "+programNum+")");
				}
			}
			return listChanges;
		}
	}
}
