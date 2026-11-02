using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBase;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace OpenPmsDentalStorage {
	///<summary>One SFTP connection with a user name and password (SSH.NET), for the clearinghouse transfers (Denti-Cal, Mercury) and the HL7 SFTP
	///mode, which used SharpSSH before. The host key is checked against the store by SshHostKeyCheck. Synchronous: callers run on a background
	///thread or a progress window's worker. Dispose disconnects.</summary>
	public class SftpSession:IDisposable {
		private readonly SftpClient _sftpClient;

		///<summary>store: the host keys the server's key is checked against (in the program, SshHostKeys.Store). connectTimeout: for connecting and
		///signing in; null keeps SSH.NET's 30 seconds. SFTP requests have no time limit, as with SharpSSH.</summary>
		public SftpSession(string host,int port,string userName,string password,ISshHostKeyStore store,TimeSpan? connectTimeout=null) {
			ConnectionInfo connectionInfo=new ConnectionInfo(host,port,userName,new PasswordAuthenticationMethod(userName,password??""));
			if(connectTimeout!=null) {
				connectionInfo.Timeout=connectTimeout.Value;
			}
			_sftpClient=new SftpClient(connectionInfo);
			SshHostKeyCheck.Attach(_sftpClient,store);
		}

		///<summary>Connects and signs in, through the host key check. Throws if the server can't be reached or refuses the password, and
		///SshHostKeyRefusedException (whose message names the server and both keys) if its host key is not the stored one.</summary>
		public void Connect() {
			SshHostKeyCheck.Connect(_sftpClient);
		}

		///<summary>Every entry of a remote folder as the server lists it, "." and ".." (folders) included. Throws if the folder does not exist.
		///</summary>
		public List<SftpEntry> ListDirectory(string path) {
			return _sftpClient.ListDirectory(path).Select(x => new SftpEntry(x.Name,x.FullName,x.IsDirectory)).ToList();
		}

		///<summary>The whole remote file. Throws if it does not exist or can't be read; nothing is written anywhere then.</summary>
		public byte[] DownloadFile(string remotePath) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				_sftpClient.DownloadFile(remotePath,memoryStream);
				return memoryStream.ToArray();
			}
		}

		///<summary>Uploads a local file, replacing a remote file of that name.</summary>
		public void UploadFile(string localPath,string remotePath) {
			using(FileStream fileStream=File.OpenRead(localPath)) {
				_sftpClient.UploadFile(fileStream,remotePath,canOverride:true);
			}
		}

		///<summary>An SFTP rename. Most servers refuse it when newPath exists.</summary>
		public void RenameFile(string oldPath,string newPath) {
			_sftpClient.RenameFile(oldPath,newPath);
		}

		public void DeleteFile(string remotePath) {
			_sftpClient.DeleteFile(remotePath);
		}

		///<summary>mode as chmod's three octal digits written in decimal, e.g. 777 for rwxrwxrwx or 644 for rw-r--r--.</summary>
		public void ChangePermissions(string remotePath,short mode) {
			_sftpClient.ChangePermissions(remotePath,mode);
		}

		public void Dispose() {
			try {
				if(_sftpClient.IsConnected) {
					_sftpClient.Disconnect();
				}
			}
			catch(Exception ex) {
				ex.DoNothing();//The connection may already be gone.
			}
			_sftpClient.Dispose();
		}
	}

	///<summary>An entry of a remote folder listing.</summary>
	public class SftpEntry {
		///<summary>The file or folder name, without the folder it is in.</summary>
		public string Name { get; }
		///<summary>The path the server gives for the entry.</summary>
		public string FullName { get; }
		public bool IsDirectory { get; }

		public SftpEntry(string name,string fullName,bool isDirectory) {
			Name=name;
			FullName=fullName;
			IsDirectory=isDirectory;
		}
	}
}
