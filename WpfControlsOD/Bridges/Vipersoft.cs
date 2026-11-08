#if !DISABLE_WINDOWS_BRIDGES
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using System.Collections.Generic;
using CodeBase;

namespace OpenPmsDental.Bridges{
	/// <summary>Vipersoft (Clarity). Sends the patient over DDE. S0-R (2026-11-08): restored, on the in-house DDE client (CodeBase.ODDdeClient) in
	/// place of NDde.dll.</summary>
	public class Vipersoft{

		/// <summary></summary>
		public Vipersoft(){

		}

		///<summary>Launches the program if necessary.  Then passes patient.Cur data using DDE.</summary>
		public static void SendData(Program ProgramCur, Patient pat){
			string path=Programs.GetProgramPath(ProgramCur);
			List<ProgramProperty> ForProgram =ProgramProperties.GetForProgram(ProgramCur.ProgramNum);;
			if(pat==null){
				MessageBox.Show("Please select a patient first");
				return;
			}
			if(!File.Exists(path)){
				MessageBox.Show("Could not find "+path);
				return;
			}
			//Make sure the program is running
			//Documentation says to include the -nostartup command line switch (to avoid optional program preference startup command).
			if(Process.GetProcessesByName("Vipersoft").Length==0){
				ODFileUtils.ProcessStart(path,"-nostartup");
				Thread.Sleep(TimeSpan.FromSeconds(4));
			}
			IntPtr hwnd=Application.OpenForms[0].Handle;
			ProgramProperty PPCur=ProgramProperties.GetCur(ForProgram, "Enter 0 to use PatientNum, or 1 to use ChartNum");;
			string patID;
			if(PPCur.PropertyValue=="0"){
				patID=pat.PatNum.ToString();
			}
			else{
				if(pat.ChartNumber==""){
					MessageBox.Show("ChartNumber for this patient is blank.");
					return;
				}
				patID=pat.ChartNumber;
			}
			Provider prov=Providers.GetProv(Patients.GetProvNum(pat));
			string command=GetCommand(pat,patID,prov,hwnd);
			//MessageBox.Show(command);
			SendCommand(command);
		}

		///<summary>The XTYP_EXECUTE command for the patient.
		///Data is sent to the Vipersoft DDE Server by use of the XTYP_EXECUTE DDE message only.
		///The format ot the XTYP_EXECUTE DDE message is"
		///command="\004hwnd|name|ID|Lastname|Firstname|MI|Comments|Provider|Provider Phone|Addrs1|Addrs2|City|State|Zip|Patient Phone|Practice Name|Patient SSN|restore server|"
		///\004 is one byte code for version 4. 0x04 or Char(4)
		///hwnd is calling software's windows handle.
		///name is for name of calling software (Open Dental)
		///ID is patient ID.  Required and must be unique.
		///Provider field is for provider name.
		///hwnd, ID, Lastname, Firstname, and Provider fields are required.  All other fields are optional.
		///All vertical bars (|) are required, including the ending bar.
		///The restore server flag is for a future release's support of the specialized capture/view commands (default is '1')
		///Visual Basic pseudo code:
		///Chan = DDEInitiate("Vipersoft", "Advanced IntraOral")
		///DDE_String$ = "" //etc
		///DDEExecute Chan, DDE_String$ //send XTYP_EXECUTE DDE command:
		///DDETerminate Chan</summary>
		public static string GetCommand(Patient pat,string patID,Provider prov,IntPtr hwnd){
			Char char4=Convert.ToChar(4);
			string command=char4.ToString();//tested to make sure this is just one non-printable byte.
			command+=hwnd.ToString()+"|"//hwnd
				+"OpenDental|";//name
			command+=patID+"|";//ID
			command+=pat.LName+"|";//Lastname
			command+=pat.FName+"|";//Firstname
			command+=pat.MiddleI+"|";//
			command+="|";//Comments: blank
			command+=prov.LName+", "+prov.FName+"|";//Provider
			command+="|";//Provider phone
			command+="|";//Addr
			command+="|";//Addr2
			command+="|";//City
			command+="|";//State
			command+="|";//Zip
			command+="|";//Phone
			command+="|";//Practice
			command+=pat.SSN+"|";//SSN
			command+="1|";//Restore Server
			return command;
		}

		///<summary>Sends the command to Vipersoft over DDE (service Vipersoft, topic Advanced IntraOral), waits up to 2 seconds for it to take it, and
		///ends the conversation. Errors are ignored, as they always were.</summary>
		public static void SendCommand(string command){
			try {
				//The conversation runs on this thread (the main window's, from the button click) and ends with the using block.
				using(IDdeConversation ddeConversation=ODDdeClient.Create("Vipersoft","Advanced IntraOral")){
					//Establish the conversation.
					ddeConversation.Connect();
					//Select patient
					ddeConversation.Execute(command,2000);//timeout 2 secs
					ddeConversation.Disconnect();
				}
			}
			catch{
				//MessageBox.Show(e.Message);
			}
		}

	}
}
#endif
