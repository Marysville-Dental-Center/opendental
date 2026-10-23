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

		//AppendRawBase64ForUpdateFiles() was removed. It wrote Open Dental's single-row UpdateFiles archive. The fork never writes or reads
		//DocumentMiscType.UpdateFiles or UpdateFilesSegment rows; it publishes its own releases with the fork-only types (see ForkReleases).

		#region Fork releases
		///<summary>The fork's published release manifest row (DocumentMiscType.ForkReleaseManifest), or null when no release was published. Never
		///reads Open Dental's update rows.</summary>
		public static DocumentMisc GetForkReleaseManifest() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<DocumentMisc>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT * FROM documentmisc WHERE DocMiscType="+POut.Int((int)DocumentMiscType.ForkReleaseManifest)
				+" ORDER BY DocMiscNum DESC LIMIT 1";
			return Crud.DocumentMiscCrud.SelectOne(command);
		}

		///<summary>All segments of the fork's published release archive (DocumentMiscType.ForkReleaseSegment), with their data, ordered by segment
		///number. Never reads Open Dental's update rows.</summary>
		public static List<DocumentMisc> GetForkReleaseSegments() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DocumentMisc>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT * FROM documentmisc WHERE DocMiscType="+POut.Int((int)DocumentMiscType.ForkReleaseSegment)
				+" ORDER BY FileName,DocMiscNum";
			return Crud.DocumentMiscCrud.SelectMany(command);
		}

		///<summary>Replaces the published fork release: deletes the old manifest and segments, inserts the new segments, then the manifest last, so a
		///reader that finds a manifest finds its segments too (the manifest's archive hash catches a half-written release). Direct connection only.
		///</summary>
		public static void ReplaceForkRelease(string manifestBase64,List<string> listSegmentsBase64) {
			Meth.NoCheckMiddleTierRole();
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				throw new ApplicationException("Releases can only be published over a direct database connection.");
			}
			DeleteAllForType(DocumentMiscType.ForkReleaseManifest);
			DeleteAllForType(DocumentMiscType.ForkReleaseSegment);
			for(int i=0;i<listSegmentsBase64.Count;i++) {
				DocumentMisc documentMiscSegment=new DocumentMisc();
				documentMiscSegment.DateCreated=DateTime.Today;
				documentMiscSegment.DocMiscType=DocumentMiscType.ForkReleaseSegment;
				documentMiscSegment.FileName=(i+1).ToString().PadLeft(4,'0');
				documentMiscSegment.RawBase64=listSegmentsBase64[i];
				Crud.DocumentMiscCrud.Insert(documentMiscSegment);
			}
			DocumentMisc documentMiscManifest=new DocumentMisc();
			documentMiscManifest.DateCreated=DateTime.Today;
			documentMiscManifest.DocMiscType=DocumentMiscType.ForkReleaseManifest;
			documentMiscManifest.FileName="ForkRelease.manifest";
			documentMiscManifest.RawBase64=manifestBase64;
			Crud.DocumentMiscCrud.Insert(documentMiscManifest);
		}

		///<summary>Deletes Open Dental's update file rows (DocumentMiscType.UpdateFiles and UpdateFilesSegment) without reading them. Used only by
		///database adoption. Returns the number of rows deleted.</summary>
		public static long DeleteOpenDentalUpdateFiles() {
			Meth.NoCheckMiddleTierRole();
			string command="DELETE FROM documentmisc WHERE DocMiscType IN ("+POut.Int((int)DocumentMiscType.UpdateFiles)+","
				+POut.Int((int)DocumentMiscType.UpdateFilesSegment)+")";
			return Db.NonQ(command);
		}

		///<summary>Number of rows of the given type, without reading their data.</summary>
		public static long CountForType(DocumentMiscType documentMiscType) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),documentMiscType);
			}
			string command="SELECT COUNT(*) FROM documentmisc WHERE DocMiscType="+POut.Int((int)documentMiscType);
			return PIn.Long(Db.GetScalar(command));
		}
		#endregion Fork releases

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