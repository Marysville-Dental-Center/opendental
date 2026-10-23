using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using OpenDentBusiness;

namespace xCrudGenerator {
	///<summary>Headless CRUD generation, so table classes can be regenerated from a script without the window. It writes the same CRUD files as the
	///Run button with "Run" checked and every other option off: no database connection, no convert script, no data interface stubs, no svn.
	///Usage: CrudGenerator.exe crud=Branding,Chat   or   CrudGenerator.exe crud=*
	///A summary is appended to CrudGeneratorLog.txt next to the exe, and the exit code is 0 on success, 1 on failure.</summary>
	public partial class Form1 {
		///<summary>Generates the CRUD file of each named table type (class names, comma-separated; * for every table type). Paths are resolved from the
		///folder of the exe, so the working directory does not matter. Returns the process exit code.</summary>
		public static int GenerateCrudFromCommandLine(string typeNames) {
			string logFileName=Path.Combine(Application.StartupPath,"CrudGeneratorLog.txt");
			StringBuilder log=new StringBuilder();
			log.AppendLine("-------------------Generating CRUD ("+typeNames+") on "+DateTime.Now.ToString()+"-------------------");
			int exitCode=0;
			try {
				List<Type> listTypesAll=GetTableTypes();
				List<Type> listTypes;
				if(typeNames.Trim()=="*") {
					listTypes=listTypesAll;
				}
				else {
					listTypes=new List<Type>();
					foreach(string name in typeNames.Split(',').Select(x => x.Trim()).Where(x => x!="")) {
						Type type=listTypesAll.FirstOrDefault(x => x.Name==name);
						if(type==null) {
							throw new ApplicationException("No table type named "+name+".");
						}
						listTypes.Add(type);
					}
				}
				Form1 form=new Form1();//Not shown. WriteAll is an instance method but uses no controls.
				foreach(Type type in listTypes) {
					string className=type.Name+"Crud";
					StringBuilder stringBuilder=new StringBuilder();
					form.WriteAll(stringBuilder,className,type,false);
					string crudDirectory=CrudGenHelper.GetCrudLocationOverride(type);
					if(string.IsNullOrEmpty(crudDirectory)) {
						crudDirectory=crudDir;
					}
					string crudFileName=Path.GetFullPath(Path.Combine(Application.StartupPath,crudDirectory,className+".cs"));
					bool isNewFile=!File.Exists(crudFileName);
					File.WriteAllText(crudFileName,stringBuilder.ToString());
					log.AppendLine((isNewFile ? "Created " : "Wrote ")+crudFileName);
				}
			}
			catch(Exception ex) {
				log.AppendLine("Error: "+ex.Message+"\r\n"+ex.StackTrace);
				exitCode=1;
			}
			File.AppendAllText(logFileName,log.ToString());
			return exitCode;
		}

		///<summary>Concrete table types in the order the window lists them.</summary>
		private static List<Type> GetTableTypes() {
			Type typeTableBase=typeof(TableBase);
			return Assembly.GetAssembly(typeTableBase).GetTypes()
				.Where(x => !x.Name.StartsWith("WebForms_") && x.IsSubclassOf(typeTableBase) && !x.IsAbstract)
				.OrderBy(x => x.Name,StringComparer.Ordinal)
				.ToList();
		}
	}
}
