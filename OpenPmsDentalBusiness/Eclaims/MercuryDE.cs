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
using OpenPmsDentalBusiness;
using OpenPmsDentalStorage;
using CodeBase;

namespace OpenPmsDentalBusiness.Eclaims
{
	/// <summary>
	/// aka Mercury Dental Exchange.
	/// </summary>
	public class MercuryDE{

		private static string remoteHost="qaftp.mercurydataexchange.com";//Change to ftp.mercurydataexchange.com when released.
		private static string rootFolderName="Testing";//Change to "Production" when released.
		private static Clearinghouse _clearinghouseClin=null;
		public static string ErrorMessage="";

		///<summary></summary>
		public MercuryDE(){			
		}

		///<summary>Returns true if the communications were successful, and false if they failed. Both sends and retrieves.</summary>
		public static bool Launch(Clearinghouse clearinghouseClin,int batchNum,IODProgressExtended progress=null) {//called from Eclaims and FormClaimReports.cs. Clinic-level clearinghouse passed in.
			progress=progress??new ODProgressExtendedNull();
			_clearinghouseClin=clearinghouseClin;
			//Before this function is called, the X12 file for the current batch has already been generated in
			//the clearinghouse export folder. The export folder will also contain batch files which have failed
			//to upload from previous attempts and we must attempt to upload these older batch files again if
			//there are any.
			//Step 1: Retrieve reports regarding the existing pending claim statuses.
			//Step 2: Send new claims in a new batch.
			bool success=true;
			//Connect to the MDE SFTP server.
			SftpSession sftpSession=null;
			progress.UpdateProgress(Lans.g(progress.LanThis,"Contacting web server"),"reports","17%",17);
			if(progress.IsPauseOrCancel()) {
				progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
				return false;
			}
			try{
				sftpSession=new SftpSession(remoteHost,22,_clearinghouseClin.LoginID,_clearinghouseClin.Password);
				sftpSession.Connect();
			}
			catch(Exception ex){
				progress.UpdateProgress(Lans.g(progress.LanThis,"Connection Failed"));
				ErrorMessage=Lans.g("MercuryDE","Connection Failed")+": "+ex.Message;
				sftpSession?.Dispose();
				return false;
			}
			progress.UpdateProgress(Lans.g(progress.LanThis,"Web server contact successful."));
			try{
				//At this point we are connected to the MDE SFTP server.
				if(batchNum==0){
					if(!Directory.Exists(clearinghouseClin.ResponsePath)){
						throw new Exception(Lans.g(progress.LanThis,"Clearinghouse response path is invalid. Go to Setup, Family/Insurance, Clearinghouses, and double-click the desired clearinghouse to update the path."));
					}
					progress.UpdateProgress(Lans.g(progress.LanThis,"Getting files"),"reports","33%",33);
					if(progress.IsPauseOrCancel()) {
						progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
						return false;
					}
					//Only retrieving reports so do not send new claims.
					string retrievePath="/"+rootFolderName+"/Out/997/";
					List<SftpEntry> fileList=sftpSession.ListDirectory(retrievePath);
					for(int i=0;i<fileList.Count;i++){
						int percent=(i/fileList.Count)*100;
						//We re-use the bar again for importing later, hence the tag.
						progress.UpdateProgress(Lans.g(progress.LanThis,"Getting file:")+i+" / "+fileList.Count,"import",percent+"%",percent);
						if(progress.IsPauseOrCancel()) {
							progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
							return false;
						}
						if(fileList[i].IsDirectory){
							continue;//Skip directories (including "." and "..") and focus on files.
						}
						string getFileName=fileList[i].Name;
						string getFilePath=retrievePath+getFileName;
						string exportFilePath=CodeBase.ODFileUtils.CombinePaths(clearinghouseClin.ResponsePath,getFileName);
						try{
							byte[] dataBytes=sftpSession.DownloadFile(getFilePath);
							File.WriteAllBytes(exportFilePath,dataBytes);//Creates or overwrites.
							float overallpercent=33+(i/fileList.Count)*17;//33 is starting point. 17 is the amount of bar space we have before our next major spot (50%)
							progress.UpdateProgress(Lans.g(progress.LanThis,"Getting files"),"reports",overallpercent+"%",(int)overallpercent);
							if(progress.IsPauseOrCancel()) {
								progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
								return false;
							}
						}
						catch(Exception ex) {
							ErrorMessage=ex.Message;
							success=false;
						}
						string archiveFilePath=retrievePath+"Archive/"+getFileName;
						try{
							sftpSession.DeleteFile(archiveFilePath);
						}
						catch {
							//Remove any destination files by the same exact name. The file will most likely not be present.
						}
						sftpSession.RenameFile(getFilePath,archiveFilePath);
					}
				}
				else {
					if(!Directory.Exists(clearinghouseClin.ExportPath)){
							throw new Exception(Lans.g(progress.LanThis,"Clearinghouse export path is invalid. Go to Setup, Family/Insurance, Clearinghouses, and double-click the desired clearinghouse to update the path."));
					}
					//First upload the batch to the temporary directory.
					progress.UpdateProgress(Lans.g(progress.LanThis,"Uploading files to temp directory"),"reports","33%",33);
					if(progress.IsPauseOrCancel()) {
						progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
						return false;
					}
					string[] files=Directory.GetFiles(clearinghouseClin.ExportPath);
					for(int i=0;i<files.Length;i++){
						int percent=(i/files.Length)*100;
						//We re-use the bar again for importing later, hence the tag.
						progress.UpdateProgress(Lans.g(progress.LanThis,"Uploading file:")+i+" / "+files.Length,"import",percent+"%",percent);
						if(progress.IsPauseOrCancel()) {
							progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
							return false;
						}
						string accountNumber=_clearinghouseClin.ISA08;
						string dateTimeStr=DateTime.Now.ToString("yyyyMMddhhmmss");
						string remoteFileName=accountNumber+dateTimeStr+i.ToString().PadLeft(3,'0')+".837D.txt";
						string remoteTempFilePath="/"+rootFolderName+"/In/837D/Temp/"+remoteFileName;
						sftpSession.UploadFile(files[i],remoteTempFilePath);
						//Read, Write and Execute permissions for everyone (octal 777). This appears to cause no effect.
						sftpSession.ChangePermissions(remoteTempFilePath,777);
						string remoteFilePath="/"+rootFolderName+"/In/837D/"+remoteFileName;
						sftpSession.RenameFile(remoteTempFilePath,remoteFilePath);
						File.Delete(files[i]);//Remove the processed file.
						float overallpercent=33+(i/files.Length)*17;//33 is starting point. 17 is the amount of bar space we have before our next major spot (50%)
						progress.UpdateProgress(Lans.g(progress.LanThis,"Uploading files"),"reports",overallpercent+"%",(int)overallpercent);
						if(progress.IsPauseOrCancel()) {
							progress.UpdateProgress(Lans.g(progress.LanThis,"Canceled by user."));
							return false;
						}
					}
				}
			}
			catch (Exception ex){
				if(ErrorMessage!="") {
					ErrorMessage+="\r\n";
				}
				ErrorMessage=ex.Message;
				success=false;
			}
			finally {
				//Disconnect from the MDE SFTP server.
				progress.UpdateProgress("","import","");//Clear import bar for now.
				sftpSession.Dispose();
			}
			return success;
		}

	}
}
