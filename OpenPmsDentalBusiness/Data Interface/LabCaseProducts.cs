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
