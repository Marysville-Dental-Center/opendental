using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Xml;
using CodeBase;

namespace OpenDentBusiness{
	///<summary></summary>
	public class DocumentMiscs{
		public static DocumentMisc GetByTypeAndFileName(string fileName,DocumentMiscType documentMiscType) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<DocumentMisc>(MethodBase.GetCurrentMethod(),fileName,documentMiscType);
			}
			string command="SELECT * FROM documentmisc "
				+"WHERE DocMiscType="+POut.Int((int)documentMiscType)+" "
				+"AND FileName='"+POut.String(fileName)+"'";
			return Crud.DocumentMiscCrud.SelectOne(command);
		}

		public static List<DocumentMisc> GetByType(DocumentMiscType documentMiscType,bool canIncludeRawBase64=false,string nameStartWith="") {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DocumentMisc>>(MethodBase.GetCurrentMethod(),documentMiscType,canIncludeRawBase64,nameStartWith);
			}
			string command="SELECT ";
			if(canIncludeRawBase64) {
				command+="* ";
			}
			else {
				command+="DocMiscNum,DateCreated,FileName,DocMiscType,'' RawBase64 ";
			}
			command+="FROM documentmisc WHERE DocMiscType="+POut.Int((int)documentMiscType);
			if(!string.IsNullOrEmpty(nameStartWith)) {
				command+=" AND FileName LIKE '"+POut.String(nameStartWith)+"%'";
			}
			return Crud.DocumentMiscCrud.SelectMany(command);
		}

		///<summary></summary>
		public static long Insert(DocumentMisc documentMisc) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				documentMisc.DocMiscNum=Meth.GetLong(MethodBase.GetCurrentMethod(),documentMisc);
				return documentMisc.DocMiscNum;
			}
			return Crud.DocumentMiscCrud.Insert(documentMisc);
		}

		///<summary>Appends the passed in rawBase64 string to the RawBase64 column in the db for the UpdateFiles DocMiscType row.</summary>
		public static void AppendRawBase64ForUpdateFiles(string rawBase64) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),rawBase64);
				return;
			}
			string command="UPDATE documentmisc SET RawBase64=CONCAT("+DbHelper.IfNull("RawBase64","")+","+DbHelper.ParamChar+"paramRawBase64) "
				+"WHERE DocMiscType="+POut.Int((int)DocumentMiscType.UpdateFiles);
			OdSqlParameter sqlParameterRawBase64=new OdSqlParameter("paramRawBase64",OdDbType.Text,rawBase64);
			Db.NonQ(command,sqlParameterRawBase64);
		}

		///<summary></summary>
		public static void DeleteAllForType(DocumentMiscType documentMiscType) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),documentMiscType);
				return;
			}
			string command="DELETE FROM documentmisc WHERE DocMiscType="+POut.Int((int)documentMiscType);
			Db.NonQ(command);
		}

		public static void DeleteMany(List<long> listDocMiscNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listDocMiscNums);
				return;
			}
			Crud.DocumentMiscCrud.DeleteMany(listDocMiscNums);
		}

		//LaunchShareScreen() was removed. It downloaded a remote support program from Open Dental's website (or unpacked a copy stored in this
		//table) and ran it. Existing DocumentMiscType.ShareScreenExeSegment rows are left alone for schema compatibility and are never executed.

		/*
		Only pull out the methods below as you need them.  Otherwise, leave them commented out.

		///<summary></summary>
		public static List<DocumentMisc> Refresh(long patNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DocumentMisc>>(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="SELECT * FROM documentmisc WHERE PatNum = "+POut.Long(patNum);
			return Crud.DocumentMiscCrud.SelectMany(command);
		}

		///<summary></summary>
		public static long Insert(DocumentMisc documentMisc){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				documentMisc.DocMiscNum=Meth.GetLong(MethodBase.GetCurrentMethod(),documentMisc);
				return documentMisc.DocMiscNum;
			}
			return Crud.DocumentMiscCrud.Insert(documentMisc);
		}

		///<summary></summary>
		public static void Update(DocumentMisc documentMisc){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				Meth.GetVoid(MethodBase.GetCurrentMethod(),documentMisc);
				return;
			}
			Crud.DocumentMiscCrud.Update(documentMisc);
		}

		///<summary></summary>
		public static void Delete(long docMiscNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),docMiscNum);
				return;
			}
			string command= "DELETE FROM documentmisc WHERE DocMiscNum = "+POut.Long(docMiscNum);
			Db.NonQ(command);
		}
		*/
	}
}