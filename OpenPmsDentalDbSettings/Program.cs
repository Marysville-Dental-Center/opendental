// The connection settings tool: OpenPmsDentalDbSettings.exe, in the program folder, run as an administrator.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using DataConnectionBase;

namespace OpenPmsDentalDbSettings {
	///<summary>Sets up and changes this computer's database connection settings only: server, database, user, the office CA, the client
	///certificate or the password (owner decision Q7 of 2026-11-02, docs/specs/db-connection-security.md section 4). The installer runs it with
	///switches; an administrator runs it to change the settings later. Its manifest asks for administrator rights.</summary>
	internal static class Program {
		[STAThread]
		private static int Main(string[] arrayArgs) {
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			bool isSilentRaw=arrayArgs.Any(x => x.Trim().Equals("/silent",StringComparison.OrdinalIgnoreCase)
				|| x.Trim().Equals("-silent",StringComparison.OrdinalIgnoreCase) || x.Trim().Equals("/check",StringComparison.OrdinalIgnoreCase));
			DbSettingsArgs dbSettingsArgs;
			try {
				dbSettingsArgs=DbSettingsArgs.Parse(arrayArgs);
			}
			catch(DbSettingsException ex) {
				DbSettingsResult dbSettingsResult=new DbSettingsResult(ex.ExitCode,ex.Message);
				if(!isSilentRaw) {
					MessageBox.Show(ex.Message+"\r\n\r\n"+DbSettingsArgs.Usage(),FormDbSettings.TITLE,MessageBoxButtons.OK,MessageBoxIcon.Error);
				}
				return dbSettingsResult.ExitCode;
			}
			if(dbSettingsArgs.IsHelp) {
				MessageBox.Show(DbSettingsArgs.Usage(),FormDbSettings.TITLE);
				return DbSettingsRunner.EXIT_OK;
			}
			string configPath=DbConnectionConfig.GetConfigPath(Application.StartupPath);
			if(dbSettingsArgs.IsCheck) {
				DbSettingsResult dbSettingsResult=DbSettingsRunner.Check(configPath);
				DbSettingsRunner.AppendLog(dbSettingsArgs.LogFile,dbSettingsResult);
				return dbSettingsResult.ExitCode;
			}
			if(dbSettingsArgs.IsSilent) {
				DbSettingsResult dbSettingsResult=RunSilent(dbSettingsArgs,configPath);
				DbSettingsRunner.AppendLog(dbSettingsArgs.LogFile,dbSettingsResult);
				return dbSettingsResult.ExitCode;
			}
			DbSettingsSecrets dbSettingsSecrets=null;
			if(dbSettingsArgs.SecretsFile!=null) {
				try {
					dbSettingsSecrets=DbSettingsSecrets.ReadFile(dbSettingsArgs.SecretsFile);
				}
				catch(DbSettingsException ex) {
					MessageBox.Show(ex.Message,FormDbSettings.TITLE,MessageBoxButtons.OK,MessageBoxIcon.Error);
					return ex.ExitCode;
				}
			}
			using(FormDbSettings formDbSettings=new FormDbSettings(configPath,DbConnectionConfig.GetOfficeCaCopyPath(Application.StartupPath),
				DbSettingsRunner.RequestFromArgs(dbSettingsArgs,dbSettingsSecrets)))
			{
				Application.Run(formDbSettings);
				DbSettingsResult dbSettingsResult=formDbSettings.Result??new DbSettingsResult(DbSettingsRunner.EXIT_CANCELLED,"Cancelled: nothing was saved.");
				DbSettingsRunner.AppendLog(dbSettingsArgs.LogFile,dbSettingsResult);
				return dbSettingsResult.ExitCode;
			}
		}

		///<summary>/silent: the switches, the secrets file or the prompt, apply, and /test. No window except the prompt.</summary>
		private static DbSettingsResult RunSilent(DbSettingsArgs dbSettingsArgs,string configPath) {
			DbSettingsSecrets dbSettingsSecrets=new DbSettingsSecrets();
			try {
				if(dbSettingsArgs.SecretsFile!=null) {
					dbSettingsSecrets=DbSettingsSecrets.ReadFile(dbSettingsArgs.SecretsFile);
				}
				DbSettingsRequest dbSettingsRequest=DbSettingsRunner.RequestFromArgs(dbSettingsArgs,dbSettingsSecrets);
				if(dbSettingsArgs.IsPrompt) {
					DbConnectionConfig dbConnectionConfigCurrent=DbSettingsRunner.ReadCurrent(configPath);
					DbSettingsAuth dbSettingsAuth=DbSettingsRunner.ResolveAuth(dbSettingsRequest,dbConnectionConfigCurrent);
					if(dbSettingsAuth==DbSettingsAuth.Certificate && dbSettingsRequest.PfxFile!=null && dbSettingsRequest.PfxPassword==null) {
						dbSettingsRequest.PfxPassword=FormSecretPrompt.Ask("The password of the client certificate file "+Path.GetFileName(dbSettingsRequest.PfxFile)+":");
					}
					if(dbSettingsAuth==DbSettingsAuth.Password && dbSettingsRequest.DbPassword==null) {
						dbSettingsRequest.DbPassword=FormSecretPrompt.Ask("The database password of the user "
							+(dbSettingsRequest.User??dbConnectionConfigCurrent.User)+":");
					}
				}
				DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(dbSettingsRequest,configPath,DbConnectionConfig.GetOfficeCaCopyPath(Application.StartupPath));
				if(dbSettingsResult.ExitCode!=DbSettingsRunner.EXIT_OK || !dbSettingsArgs.IsTest) {
					return dbSettingsResult;
				}
				DbSettingsResult dbSettingsResultTest=DbSettingsRunner.TestSaved(configPath);
				if(dbSettingsResultTest.ExitCode!=DbSettingsRunner.EXIT_OK) {
					return new DbSettingsResult(DbSettingsRunner.EXIT_TEST_FAILED,dbSettingsResult.Message+" "+dbSettingsResultTest.Message);
				}
				return new DbSettingsResult(DbSettingsRunner.EXIT_OK,dbSettingsResult.Message+" "+dbSettingsResultTest.Message);
			}
			catch(DbSettingsException ex) {
				return new DbSettingsResult(ex.ExitCode,ex.Message);
			}
		}
	}
}
