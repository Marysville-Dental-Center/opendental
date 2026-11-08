#if !DISABLE_WINDOWS_BRIDGES
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental.Bridges {

	///<summary>The Dexis Integrator is used when an office uses Terminal Services or Citrix and want to take X-rays on a client workstation with OD
	///running on a server viewed through an RDP session.  The Integrator will be installed on the Terminal Server and on the client workstations.
	///When a user pushes the Dexis Integrator button to launch Dexis the Remote Desktop window is minimized and Dexis is opened on the client
	///workstation with the selected patient.  The patient data can be sent via DDE commands or text files to communicate between server
	///and client.  The Dexis Integrator can also be used by multi-office installations using multiple image db's.
	///S0-R (2026-11-08): restored, on the in-house DDE client (CodeBase.ODDdeClient) in place of NDde.dll.</summary>
	public static class DexisIntegrator{
		///<summary>The DDE conversation with the Dexis Integrator (service Dexis, topic Patient). It stays open after the button is pressed, so each
		///patient selected afterwards is sent on it (PatientChangedEvent_Fired), until the link is disabled, the user changes or the conversation fails.
		///</summary>
		private static IDdeConversation _ddeConversation=null;

		public static void SendData(Program progCur,Patient pat) {
			if(pat==null) {
				MsgBox.Show("DexisIntegrator","Please select a patient first.");
				return;
			}
			if(ProgramProperties.GetPropVal(progCur.ProgramNum,"Enter 0 to use DDE, or 1 to use communication file")=="0") {
				SendDataDDE(progCur,pat);
			}
			else {
				SendDataCommFile(progCur,pat);
			}
		}


		///<summary>Sends data for pat to the Dexis Integrator program using a communication file.
		///Writes patient info with following format:
		///PN=X
		///LN="LName"
		///FN="FName"
		///MI="MiddleI"
		///BD=19760205
		///SX="X".
		///The first parameter of the file is the patient ID defined by the program property as either PatNum or ChartNumber.  The BD portion will only be added
		///if the pat has a valid bday. The sex portion will only be added if patient has listed Male or Female as their sex. Dexis has no support for unknown.</summary>
		public static void SendDataCommFile(Program progCur,Patient pat) {
			string processPath=Programs.GetProgramPath(progCur); //Holds the path to the dexis integrator application, Integra.exe
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetForProgram(progCur.ProgramNum);
			string communicationFile=ProgramProperties.GetPropValFromList(listProgramProperties,"Communication files folder path");//This path is the folder where all comm files will be stored.
			string fileName="Patient_"+ODEnvironment.MachineName+".txt";
			if(communicationFile.Trim()=="") {
				communicationFile=CodeBase.ODFileUtils.CombinePaths(PrefC.GetTempFolderPath(),fileName);
			}
			else {
				communicationFile=CodeBase.ODFileUtils.CombinePaths(communicationFile,fileName);
			}
			try {
				using MemoryStream memoryStream=new MemoryStream();
				//patientID can be any string format, max 12 char.
				//There is no validation to ensure that length is 12 char or less.
				string id=pat.PatNum.ToString();
				if(ProgramProperties.GetPropValFromList(listProgramProperties,"Enter 0 to use PatientNum, or 1 to use ChartNum")=="1") {
					id=pat.ChartNumber;
				}
				//Encoding 1252 was specifically requested by the XDR development team to help with accented characters (ex Canadian customers).
				//On 05/19/2015, a reseller noticed UTF8 encoding in the Dexis bridge caused a similar issue.
				//We decided it was safe to switch Dexis from using UTF8 to code page 1252 because the bridge depends entirely on the bridge ID,
				//not the patient names.  Thus there is no chance of breaking the Dexis bridge by using code page 1252 instead.
				//06/01/2015 A customer tested and confirmed that using the XDR bridge and thus coding page 1252, solved the special characters issue.
				Encoding encoding=Encoding.GetEncoding(1252);
				using (StreamWriter streamWriter=new StreamWriter(memoryStream,encoding)) {
					streamWriter.WriteLine("PN="+id);
					streamWriter.WriteLine("LN="+pat.LName);
					streamWriter.WriteLine("FN="+pat.FName);
					streamWriter.WriteLine("MI="+pat.MiddleI);
					if(pat.Birthdate.Year>1880) {//add optional bday only if valid
						streamWriter.WriteLine("BD="+pat.Birthdate.ToString("yyyyMMdd"));
					}
					if(pat.Gender==PatientGender.Female) {
						streamWriter.WriteLine("SX=F");
					}
					else if(pat.Gender==PatientGender.Male) {
						streamWriter.WriteLine("SX=M");
					}
				}

				//Dexis integrator was starting before the patient information was done being written to the communication file.
				//This resulted in the RDP session minimizing and the client installation opening up with the patient that was
				//previously in the communication file. Or if the communication file did not exist, the server dexis integrator
				//would prompt that no patient was selected. We added an optional Thread.Sleep to WriteAllBytesThenStart to ensure that
				//the file is done being written to and added a progress bar to let users know OD did not stutter or freeze.
				ProgressWin progressOD=new ProgressWin();
				Action actionWriteStartDexis=() => ODFileUtils.WriteAllBytesThenStart(communicationFile,memoryStream.ToArray(),processPath,"\"@"+communicationFile+"\"",millisecondsToSleep:2000);
				progressOD.ActionMain=actionWriteStartDexis;
				progressOD.StartingMessage="Writing patient data to communication file.";
				progressOD.ShowDialog();
				if(progressOD.IsCancelled) {
					return;
				}
			}
			catch {
				MessageBox.Show(processPath+" is not available.");
			}
		}

		///<summary>Sends data for pat to the Dexis Integrator program using a DDE Execute command (GetDdeCommand), on the open conversation or a new
		///one (ExecuteDDE). Shows the error if that fails. Once a command went through, the patient and user change events are followed.</summary>
		public static void SendDataDDE(Program progCur,Patient pat) {
			try {
				string patId=pat.PatNum.ToString();
				if(ProgramProperties.GetPropVal(progCur.ProgramNum,"Enter 0 to use PatientNum, or 1 to use ChartNum")=="1") {
					patId=pat.ChartNumber;
				}
				ExecuteDDE(GetDdeCommand(pat,patId));
				//if execute was successfully sent, subscribe the PatientChangedEvent_Fired handler to the PatientChangedEvent.Fired event
				ODEvent.Fired-=PatientChangedEvent_Fired;
				ODEvent.Fired+=PatientChangedEvent_Fired;
				ODEvent.Fired-=UserodChangedEvent_Fired;
				ODEvent.Fired+=UserodChangedEvent_Fired;
			}
			catch(Exception ex) {
				FriendlyException.Show(Lans.g("DexisIntegrator","There was an error trying to launch Dexis with the selected patient."),ex);
			}
		}

		///<summary>The SET command with the following format: SET 17 LN="LName" FN="FName" MI="MiddleI" BD=19760205. The first parameter of the
		///command is the patient ID defined by the program property as either PatNum or ChartNumber (patId).  The BD portion will only be added
		///if the pat has a valid bday and adding it to the commmand won't increase the length of the command to >255 characters.</summary>
		public static string GetDdeCommand(Patient pat,string patId) {
			string ddeCommand="SET "+patId+" LN=\""+pat.LName+"\" FN=\""+pat.FName+"\" MI=\""+pat.MiddleI+"\"";
			if(pat.Birthdate.Year>1880 && ddeCommand.Length<244) {//add optional bday only if valid and it won't increase the length to >255 characters
				ddeCommand+=" BD="+pat.Birthdate.ToString("yyyyMMdd");
			}
			return ddeCommand;
		}

		///<summary>Sends the command on the conversation with the Dexis Integrator, opening one first when there is none or it ended, and waits up to
		///30 seconds. When the conversation turns out to be disposed (ObjectDisposedException), it is closed and the command is sent once more on a new
		///one. Throws when that fails too, and for every other error.</summary>
		public static void ExecuteDDE(string ddeCommand) {
			try {
				ExecuteOnConversation(ddeCommand);
			}
			catch(ObjectDisposedException) {
				CloseConversation();//will cause a new conversation to be made
				ExecuteOnConversation(ddeCommand);
			}
		}

		///<summary>Ends the conversation with the Dexis Integrator, if there is one. The next command opens a new one; the user must press the
		///button again to send the next patient.</summary>
		public static void CloseConversation() {
			if(_ddeConversation==null) {
				return;
			}
			IDdeConversation ddeConversation=_ddeConversation;
			_ddeConversation=null;
			if(ddeConversation.IsConnected) {
				ODException.SwallowAnyException(new Action(() => ddeConversation.Disconnect()));//disconnect if necessary
			}
			ODException.SwallowAnyException(new Action(() => ddeConversation.Dispose()));
		}

		private static void ExecuteOnConversation(string ddeCommand) {
			if(_ddeConversation==null || _ddeConversation.IsDisposed) {
				//The conversation runs on the main window's thread, as NDde's DdeContext(form) did, so a patient change raised on another thread
				//still reaches it.
				_ddeConversation=ODDdeClient.Create("Dexis","Patient",GetMainForm());
			}
			if(!_ddeConversation.IsConnected) {
				_ddeConversation.Connect();
			}
			_ddeConversation.Execute(ddeCommand,30000);//timeout 30 seconds
		}

		///<summary>The main window, or the first open window, or null when none is open (the DDE calls then run on the calling thread).</summary>
		private static Form GetMainForm() {
			//FormOpenDental is in the exe, which this assembly cannot reference. It shares FrmODBase's namespace, taken from the type so the
			//lookup follows a rename.
			string fullNameMain=typeof(FrmODBase).Namespace+".FormOpenDental";
			Form formOD=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.GetType().FullName==fullNameMain);
			if(formOD is null && Application.OpenForms.Count>0){
				formOD=Application.OpenForms[0];
			}
			return formOD;
		}

		private static void UserodChangedEvent_Fired(ODEventArgs e) {
			if(e.EventType!=ODEventType.Userod || e.Tag.GetType()!=typeof(long)) {
				return;
			}
			CloseConversation();//user must press the button to launch
		}

		///<summary>This event will be added to the PatientChangedEvent.Fired handler when the DexisIntegrator button is pressed.  When this fires we
		///will send the command to Dexis Integrator to change pats if the connection is still active and the program link is still enabled.  If the
		///program link is no longer enabled we will unsubscribe this event from the PatientChangedEvent.Fired handler and close the conversation.  If the
		///PatNum tagged to ODEventArgs e is invalid we will also close the conversation so the user will have to press the button again to launch.</summary>
		private static void PatientChangedEvent_Fired(ODEventArgs e) {
			if(e.EventType!=ODEventType.Patient || e.Tag.GetType()!=typeof(long)) {
				return;
			}
			Program prog=Programs.GetCur(ProgramName.DexisIntegrator);
			Patient pat=null;
			if(prog.Enabled && _ddeConversation!=null && _ddeConversation.IsConnected) {//prog enabled and connection made (i.e. user previously pressed the button)
				pat=Patients.GetPat((long)e.Tag);//the tag for this event is the newly selected PatNum
			}
			if(pat!=null) {//pat is not null, so prog is enabled and connection has been previously made (i.e. user has pressed the button)
				SendDataDDE(prog,pat);
				return;
			}
			//pat==null, i.e. invalid PatNum (e.g. log off and back on as different user so PatNum==0), the prog isn't enabled or no connection
			//we will disconnect and close the conversation.  User will have to press the button again to reconnect to the service
			if(!prog.Enabled) {//prog was enabled (otherwise this event wouldn't be triggered) but now is not enabled
				ODEvent.Fired-=PatientChangedEvent_Fired;//remove this event from PatientChangedEvent so changing pats won't cause this to fire
				ODEvent.Fired-=UserodChangedEvent_Fired;
			}
			CloseConversation();//a new conversation is made the next time they press the button
		}

	}
}
#endif
