using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalStorage;

namespace OpenPmsDentalBusiness.Eclaims {
	/// <summary>
	/// aka Denti-Cal.
	/// </summary>
	public class DentiCal {

		public static string ErrorMessage="";

		///<summary></summary>
		public DentiCal() {
		}

		///<summary>Returns true if the communications were successful, and false if they failed. Both sends and retrieves.</summary>
		public static bool Launch(Clearinghouse clearinghouseClin,int batchNum,IODProgressExtended progress=null) { //called from FormClaimReports and Eclaims.cs. clinic-level clearinghouse passed in.
			//Before this function is called, the X12 file for the current batch has already been generated in
			//the clearinghouse export folder. The export folder will also contain batch files which have failed
			//to upload from previous attempts and we must attempt to upload these older batch files again if
			//there are any.
			//Step 1: Retrieve reports regarding the existing pending claim statuses.
			//Step 2: Send new claims in a new batch.
			progress=progress??new ODProgressExtendedNull();
			bool success=true;
			//Connect to the Denti-Cal SFTP server.
			SftpSession sftpSession=null;
			progress.UpdateProgress(Lans.g(progress.LanThis,"Contacting web server"),"reports","17%",17);
			if(progress.IsPauseOrCancel()) {
				progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
				return false;
			}
			try {
				GetRemoteHostAndPort(clearinghouseClin.ClientProgram,out string remoteHost,out int remotePort);
				sftpSession=new SftpSession(remoteHost,remotePort,clearinghouseClin.LoginID,clearinghouseClin.Password);
				sftpSession.Connect();
			}
			catch(Exception ex) {
				ErrorMessage=Lans.g("DentiCal","Connection Failed")+": "+ex.Message;
				sftpSession?.Dispose();
				return false;
			}
			progress.UpdateProgress(Lans.g(progress.LanThis,"Web server contact successful."));
			try {
				string homeDir="/";//new production home root dir
				//At this point we are connected to the Denti-Cal SFTP server.
				if(batchNum==0) { //Retrieve reports.
					progress.UpdateProgress(Lans.g(progress.LanThis,"Downloading reports"),"reports","33%",33);
					if(progress.IsPauseOrCancel()) {
						progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
						return false;
					}
					if(!Directory.Exists(clearinghouseClin.ResponsePath)) {
						progress.UpdateProgress(Lans.g(progress.LanThis,"Clearinghouse response path is invalid."));
						return false;
						throw new Exception("Clearinghouse response path is invalid. Go to Setup, Family/Insurance, Clearinghouses, and double-click the desired clearinghouse to update the path.");
					}
					//Only retrieving reports so do not send new claims.
					//Although the documentation we received from Denti-Cal says that the folder name should start "OXi", that was not the case for a customer
					//that we connected to and Barbara Castelli from Denti-Cal informed us that the folder name should start with "dcaprod".
					string retrievePath=homeDir+"dcaprod_"+clearinghouseClin.LoginID+"_out/";
					List<SftpEntry> fileList;
					try {
						fileList=sftpSession.ListDirectory(retrievePath);
					}
					catch(Exception ex) {
						ex.DoNothing();
						//Try again with the path as described in the documentation.
						retrievePath=homeDir+"OXi_"+clearinghouseClin.LoginID+"_out/";
						fileList=sftpSession.ListDirectory(retrievePath);
					}
					for(int i=0;i<fileList.Count;i++) {
						int percent=(i/fileList.Count)*100;
						//We re-use the bar again for importing later, hence the tag.
						progress.UpdateProgress(Lans.g(progress.LanThis,"Getting file:")+i+" / "+fileList.Count,"import",percent+"%",percent);
						if(progress.IsPauseOrCancel()) {
							progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
							return false;
						}
						if(fileList[i].IsDirectory) {
							continue;//Skip directories (including "." and "..") and focus on files.
						}
						string getFileName=fileList[i].Name;
						string getFilePath=retrievePath+getFileName;
						string exportFilePath=CodeBase.ODFileUtils.CombinePaths(clearinghouseClin.ResponsePath,getFileName);
						try {
							byte[] dataBytes=sftpSession.DownloadFile(getFilePath);
							File.WriteAllBytes(exportFilePath,dataBytes);//Creates or overwrites.
							float overallpercent=33+(i/fileList.Count)*17;//33 is starting point. 17 is the amount of bar space we have before our next major spot (50%)
							progress.UpdateProgress(Lans.g(progress.LanThis,"Getting files"),"reports",overallpercent+"%",(int)overallpercent);
							if(progress.IsPauseOrCancel()) {
								progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
								return false;
							}
						}
						catch {
							success=false;
						}
						finally {
							progress.UpdateProgress("","import","");//Clear import bar for now
						}
						if(success) {
							//Removed the processed report from the Denti-Cal SFTP so it does not get processed again in the future.
							try {
								sftpSession.DeleteFile(getFilePath);
								progress.UpdateProgress(Lans.g(progress.LanThis,"Reports downloaded successfully."));
							}
							catch {
							}
						}
					}
				}
				else { //Send batch of claims.
					progress.UpdateProgress(Lans.g(progress.LanThis,"Sending batch of claims"),"reports","33%",33);
					if(progress.IsPauseOrCancel()) {
						progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
						return false;
					}
					if(!Directory.Exists(clearinghouseClin.ExportPath)) {
						throw new Exception(Lans.g(progress.LanThis,"Clearinghouse export path is invalid. Go to Setup, Family/Insurance, Clearinghouses, and double-click the desired clearinghouse to update the path."));
					}
					string[] files=Directory.GetFiles(clearinghouseClin.ExportPath);
					//Try to find a folder that starts with "dcaprod" or "OXi".
					string uploadPath=homeDir+"dcaprod_"+clearinghouseClin.LoginID+"_in/";
					try {
						sftpSession.ListDirectory(uploadPath);
					}
					catch(Exception ex) {
						ex.DoNothing();
						//Try again with the path as described in the documentation.
						uploadPath=homeDir+"OXi_"+clearinghouseClin.LoginID+"_in/";
						sftpSession.ListDirectory(uploadPath);
					}
					//We have successfully found the folder where we need to put the files.
					for(int i=0;i<files.Length;i++) {
						float overallpercent=33+(i/files.Length)*17;//33 is starting point. 17 is the amount of bar space we have before our next major spot (50%)
						progress.UpdateProgress(Lans.g(progress.LanThis,"Sending claims"),"reports",overallpercent+"%",(int)overallpercent);
						if(progress.IsPauseOrCancel()) {
							progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
							return false;
						}
						//First upload the batch file to a temporary file name. Denti-Cal does not process file names unless they start with the Login ID.
						//Uploading to a temporary file and then renaming the file allows us to avoid partial file uploads if there is connection loss.
						string tempRemoteFilePath=uploadPath+"temp_"+Path.GetFileName(files[i]);
						sftpSession.UploadFile(files[i],tempRemoteFilePath);
						//Denti-Cal requires the file name to start with the Login ID followed by a period and end with a .txt extension.
						//The middle part of the file name can be anything.
						string remoteFilePath=uploadPath+Path.GetFileName(files[i]);
						sftpSession.RenameFile(tempRemoteFilePath,remoteFilePath);
						File.Delete(files[i]);//Remove the processed file.
					}
					progress.UpdateProgress(Lans.g(progress.LanThis,"Claims sent successfully."));
				}
			}
			catch(Exception ex) {
				success=false;
				ErrorMessage+=ex.Message;
			}
			finally {
				progress.UpdateProgress(Lans.g(progress.LanThis,"Closing connection"),"reports","50%",50);
				//Disconnect from the Denti-Cal SFTP server.
				sftpSession.Dispose();
			}
			return success;
		}

		///<summary>The Denti-Cal SFTP server from the clearinghouse's ClientProgram field: "host", "host:port", or blank for Denti-Cal's own server.
		///A missing or invalid port gives 2222.</summary>
		public static void GetRemoteHostAndPort(string clientProgram,out string remoteHost,out int remotePort) {
			remoteHost="sftp.mft.oxisaas.com";
			remotePort=2222;
			if(string.IsNullOrEmpty(clientProgram)) {
				return;
			}
			if(clientProgram.Contains(":")) {//if the user included the port number
				remoteHost=clientProgram.Split(':')[0];
				remotePort=PIn.Int(clientProgram.Split(':')[1],false);
				if(remotePort==0) {
					remotePort=2222;
				}
			}
			else {
				remoteHost=clientProgram;
			}
		}

	}
}
