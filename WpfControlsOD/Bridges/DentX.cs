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
	/// <summary>ProImage (DentX). Sends the patient over DDE. S0-R (2026-11-08): restored, on the in-house DDE client (CodeBase.ODDdeClient) in
	/// place of NDde.dll.</summary>
	public class DentX{

		/// <summary></summary>
		public DentX(){

		}

		[DllImport("kernel32")]//this is the windows function for reading from ini files.
		private static extern int GetPrivateProfileString(string section,string key,string def
			,StringBuilder retVal,int size,string filePath);

		private static string ReadValue(string fileName,string section,string key){
			StringBuilder strBuild=new StringBuilder(255);
			int i=GetPrivateProfileString(section,key,"",strBuild,255,fileName);
			return strBuild.ToString();
		}

		///<summary>Launches the program using the patient.Cur data.</summary>
		public static void SendData(Program ProgramCur, Patient pat){
			List<ProgramProperty> ForProgram =ProgramProperties.GetForProgram(ProgramCur.ProgramNum);;
			if(pat==null){
				MessageBox.Show("Please select a patient first");
				return;
			}
			//Get the program path from the ini file
			string windir=Environment.GetEnvironmentVariable("windir");// C:\WINDOWS
			string iniFile=windir+"\\dentx.ini";
			if(!File.Exists(iniFile)){
				MessageBox.Show("Could not find "+iniFile);
				return;
			}
			//Make sure the program is running
			string proimagePath=ReadValue(iniFile,"imagemgt","MainFile");
			Process[] proImageInstances=Process.GetProcessesByName("ProImage");
			if(proImageInstances.Length==0){
				ODFileUtils.ProcessStart(proimagePath);
				Thread.Sleep(TimeSpan.FromSeconds(10));
			}
			ProgramProperty PPCur=ProgramProperties.GetCur(ForProgram, "Enter 0 to use PatientNum, or 1 to use ChartNum");;
			bool isChartNum=(PPCur.PropertyValue!="0");
			if(isChartNum && pat.ChartNumber==""){
				MessageBox.Show("ChartNumber for this patient is blank.");
				return;
			}
			string command=GetCommand(pat,isChartNum);
			//MessageBox.Show(command);
			SendCommand(command);
		}

		///<summary>The DDE command that opens ProImage's Xray Chart screen for the patient:
		///command="Xray,PatientNo,FirstName,LastName,Birth Date,Sex,Address,City,State,Code"(zip). Commas inside the values are dropped.
		///PatientNo is the PatNum, or the ChartNumber when isChartNum (any string up to 9 characters). Sex is F for female, otherwise M.</summary>
		public static string GetCommand(Patient pat,bool isChartNum){
			string command="Xray,";
			//PatientNo can be any string format up to 9 char
			if(!isChartNum){
				command+=pat.PatNum.ToString()+",";
			}
			else{
				command+=pat.ChartNumber.Replace(",","")+",";
			}
			command+=pat.FName.Replace(",","")+","
				+pat.LName.Replace(",","")+","
				+pat.Birthdate.ToShortDateString()+",";
			if(pat.Gender==PatientGender.Female)
				command+="F,";
			else
				command+="M,";
			command+=pat.Address.Replace(",","")+","
				+pat.City.Replace(",","")+","
				+pat.State.Replace(",","")+","
				+pat.Zip.Replace(",","");
			return command;
		}

		///<summary>Sends the command to ProImage over DDE (service ProImage, topic Image) and waits up to 2 seconds for ProImage to take it. Errors
		///are ignored, as they always were.</summary>
		public static void SendCommand(string command){
			try {
				//The conversation runs on this thread (the main window's, from the button click) and ends with the using block.
				using(IDdeConversation ddeConversation=ODDdeClient.Create("ProImage","Image")){
					//Establish the conversation.
					ddeConversation.Connect();
					//Start ProImage and open to the Xray Chart screen
					ddeConversation.Execute(command,2000);//timeout 2 secs
				}
			}
			catch{
				//MessageBox.Show(e.Message);
			}
		}

	}
}
#endif
