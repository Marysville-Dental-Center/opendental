// Open Dental 26.2 (P262-GLIDEWELL): the products of a lab case (labcaseproduct): what the lab makes, with material, shade, teeth and notes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The products of a lab case. Edited in the Edit Lab Case window and saved with the lab case; deleted with it (LabCases.Delete).</summary>
	public class LabCaseProducts {
		///<summary>The lab case's products, in the order they were added.</summary>
		public static List<LabCaseProduct> GetForLabCase(long labCaseNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<LabCaseProduct>>(MethodBase.GetCurrentMethod(),labCaseNum);
			}
			if(labCaseNum==0) {
				return new List<LabCaseProduct>();
			}
			string command="SELECT * FROM labcaseproduct WHERE LabCaseNum="+POut.Long(labCaseNum)+" ORDER BY LabCaseProductNum";
			return Crud.LabCaseProductCrud.SelectMany(command);
		}

		public static long Insert(LabCaseProduct labCaseProduct) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				labCaseProduct.LabCaseProductNum=Meth.GetLong(MethodBase.GetCurrentMethod(),labCaseProduct);
				return labCaseProduct.LabCaseProductNum;
			}
			return Crud.LabCaseProductCrud.Insert(labCaseProduct);
		}

		public static void Update(LabCaseProduct labCaseProduct) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),labCaseProduct);
				return;
			}
			Crud.LabCaseProductCrud.Update(labCaseProduct);
		}

		///<summary>Makes the database hold exactly listLabCaseProducts for the lab case: rows not in the list are deleted, rows with a key are updated
		///where they changed, rows without a key (0) are inserted and get their key. Every row gets the lab case's LabCaseNum and PatNum. Rows of other
		///lab cases are never touched. Returns the rows as saved, with their keys (under the Middle Tier the caller's objects do not get them).</summary>
		public static List<LabCaseProduct> Sync(long labCaseNum,long patNum,List<LabCaseProduct> listLabCaseProducts) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<LabCaseProduct>>(MethodBase.GetCurrentMethod(),labCaseNum,patNum,listLabCaseProducts);
			}
			if(labCaseNum==0) {
				throw new ArgumentException("The lab case must be saved before its products.");
			}
			List<LabCaseProduct> listNew=listLabCaseProducts??new List<LabCaseProduct>();
			List<LabCaseProduct> listDb=GetForLabCase(labCaseNum);
			List<long> listKeysKept=listNew.Where(x => x.LabCaseProductNum!=0).Select(x => x.LabCaseProductNum).ToList();
			List<long> listKeysDelete=listDb.Where(x => !listKeysKept.Contains(x.LabCaseProductNum)).Select(x => x.LabCaseProductNum).ToList();
			if(listKeysDelete.Count>0) {
				Crud.LabCaseProductCrud.DeleteMany(listKeysDelete);
			}
			for(int i=0;i<listNew.Count;i++) {
				listNew[i].LabCaseNum=labCaseNum;
				listNew[i].PatNum=patNum;
				LabCaseProduct labCaseProductDb=listDb.FirstOrDefault(x => x.LabCaseProductNum==listNew[i].LabCaseProductNum);
				if(listNew[i].LabCaseProductNum==0 || labCaseProductDb==null) {
					//A key that is not this lab case's row is never used to write another row: insert a new one.
					listNew[i].LabCaseProductNum=0;
					Crud.LabCaseProductCrud.Insert(listNew[i]);
					continue;
				}
				Crud.LabCaseProductCrud.Update(listNew[i],labCaseProductDb);
			}
			return listNew;
		}

		///<summary>Deletes every product of the lab case.</summary>
		public static void DeleteForLabCase(long labCaseNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),labCaseNum);
				return;
			}
			string command="DELETE FROM labcaseproduct WHERE LabCaseNum="+POut.Long(labCaseNum);
			Db.NonQ(command);
		}

		#region Teeth from the procedures
		//Open Dental's 26.2 database document: labcaseproduct.ToothRange is "Retrieved from procedure.ToothRange, formatted for display". The teeth
		//are filled in from the procedures of the appointment the lab case is attached to; what the user types in the product window is kept as
		//typed (the manual override), and an existing product's teeth are only replaced when the user asks (FormLabCaseProductEdit).

		///<summary>The procedures attached to a lab case through the appointment the program links it to: those of its scheduled appointment
		///(procedurelog.AptNum) when it has one, otherwise those of its planned appointment (procedurelog.PlannedAptNum). Deleted procedures are left
		///out. Empty when the lab case is attached to neither. Pass the lab case's AptNum and PlannedAptNum as they are on screen.</summary>
		public static List<Procedure> GetProcsForLabCase(long aptNum,long plannedAptNum) {
			Meth.NoCheckMiddleTierRole();
			List<Procedure> listProcedures;
			if(aptNum!=0) {
				listProcedures=Procedures.GetProcsForSingle(aptNum,false);
			}
			else if(plannedAptNum!=0) {
				listProcedures=Procedures.GetProcsForSingle(plannedAptNum,true);
			}
			else {
				return new List<Procedure>();
			}
			return listProcedures.Where(x => x.ProcStatus!=ProcStat.D).OrderBy(x => x.ProcNum).ToList();
		}

		///<summary>Every tooth of the procedures, formatted for display as the Chart shows a tooth range (Tooth.DisplayRange: runs of three or more
		///teeth with a dash, in the given nomenclature), for example "8-10,12". The teeth come from each procedure's ToothRange and, for a one-tooth
		///procedure, its ToothNum. Invalid entries are left out, and each tooth is listed once, in tooth order. Arches, quadrants and sextants are
		///not teeth and are not listed. "" when no procedure has a tooth.</summary>
		public static string FormatToothRange(List<Procedure> listProcedures,ToothNumberingNomenclature toothNumberingNomenclature) {
			Meth.NoCheckMiddleTierRole();
			List<string> listEntries=new List<string>();
			foreach(Procedure procedure in listProcedures??new List<Procedure>()) {
				if(procedure==null) {
					continue;
				}
				listEntries.Add(procedure.ToothRange);
				listEntries.Add(procedure.ToothNum);
			}
			return FormatTeeth(listEntries,toothNumberingNomenclature);
		}

		///<summary>The rule of FormatToothRange on the stored tooth entries themselves: each entry is a tooth number or a comma separated list of them
		///(procedurelog.ToothNum or ToothRange, internal American numbers). Invalid entries are left out, each tooth is listed once, in tooth order,
		///formatted with Tooth.DisplayRange in the given nomenclature. "" when there is no tooth.</summary>
		public static string FormatTeeth(IEnumerable<string> listToothEntries,ToothNumberingNomenclature toothNumberingNomenclature) {
			Meth.NoCheckMiddleTierRole();
			List<string> listTeeth=new List<string>();
			foreach(string entry in listToothEntries??new List<string>()) {
				foreach(string part in (entry??"").Split(',')) {
					string toothNum=part.Trim();
					if(Tooth.IsValidDB(toothNum) && !listTeeth.Contains(toothNum)) {
						listTeeth.Add(toothNum);
					}
				}
			}
			if(listTeeth.Count==0) {
				return "";
			}
			listTeeth.Sort(new ToothComparer());
			return Tooth.DisplayRange(string.Join(",",listTeeth),toothNumberingNomenclature);
		}

		///<summary>The teeth of the lab case's procedures (GetProcsForLabCase), formatted for display in the office's tooth numbering
		///(FormatToothRange). "" when there are none.</summary>
		public static string GetToothRangeFromProcs(long aptNum,long plannedAptNum) {
			Meth.NoCheckMiddleTierRole();
			List<Procedure> listProcedures=GetProcsForLabCase(aptNum,plannedAptNum);
			if(listProcedures.Count==0) {
				return "";
			}
			return FormatToothRange(listProcedures,(ToothNumberingNomenclature)PrefC.GetInt(PrefName.UseInternationalToothNumbers));
		}

		///<summary>Fills a product's blank teeth with toothRangeFromProcs and returns true; a product whose teeth are not blank keeps them (the user's
		///own entry) and false is returned.</summary>
		public static bool FillToothRangeIfBlank(LabCaseProduct labCaseProduct,string toothRangeFromProcs) {
			Meth.NoCheckMiddleTierRole();
			if(labCaseProduct==null || !string.IsNullOrWhiteSpace(labCaseProduct.ToothRange) || string.IsNullOrWhiteSpace(toothRangeFromProcs)) {
				return false;
			}
			labCaseProduct.ToothRange=toothRangeFromProcs.Trim();
			return true;
		}
		#endregion Teeth from the procedures

		///<summary>One line for lists and for the notes sent to the lab, for example "BruxZir, shade A1, teeth 8-9: Full contour". Parts that are
		///blank are left out. The product list value (GlidewellProduct) is not shown: Glidewell's list is not published.</summary>
		public static string GetSummary(LabCaseProduct labCaseProduct) {
			Meth.NoCheckMiddleTierRole();
			if(labCaseProduct==null) {
				return "";
			}
			List<string> listParts=new List<string>();
			if(!string.IsNullOrWhiteSpace(labCaseProduct.Material)) {
				listParts.Add(labCaseProduct.Material.Trim());
			}
			if(!string.IsNullOrWhiteSpace(labCaseProduct.Shade)) {
				listParts.Add(Lans.g("LabCaseProducts","shade")+" "+labCaseProduct.Shade.Trim());
			}
			if(!string.IsNullOrWhiteSpace(labCaseProduct.ToothRange)) {
				listParts.Add(Lans.g("LabCaseProducts","teeth")+" "+labCaseProduct.ToothRange.Trim());
			}
			string summary=string.Join(", ",listParts);
			if(!string.IsNullOrWhiteSpace(labCaseProduct.Notes)) {
				string notes=labCaseProduct.Notes.Trim().Replace("\r\n"," ").Replace("\n"," ").Replace("\r"," ");
				summary=(summary=="") ? notes : summary+": "+notes;
			}
			return summary;
		}
	}
}
