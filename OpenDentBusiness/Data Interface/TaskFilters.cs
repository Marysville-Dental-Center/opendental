// The "Unassigned" clinic and region choices of the Task Filter window (package B-TASK, Open Dental 25.2 parity:
// v25_2-manage-task-filter-unassigned). Kept in its own file so that Tasks.cs, which other packages edit at the same time, only needs a short hook.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness {
	///<summary>Helpers for the task filter lists that the Task Filter window hands to the task queries in Tasks.cs. In those lists 0 already
	///means "no filter" (Headquarters): a one-item list holding 0 is the unfiltered default, and the queries always show tasks whose patient or
	///appointment has clinic 0. So "Unassigned" is the separate key UnassignedNum (-1), never 0.
	///Clinic: Unassigned matches tasks whose attached patient or appointment has no clinic (ClinicNum 0).
	///Region: Unassigned matches tasks whose attached patient's or appointment's clinic has no region (clinic.Region 0), plus the tasks with no
	///clinic, because a missing clinic has no region either.
	///Tasks with nothing attached are never filtered out, as before.</summary>
	public class TaskFilters {
		///<summary>The key of the "Unassigned" choice, in both the clinic list (in place of a ClinicNum) and the region list (in place of a
		///DefNum). Real ClinicNums and DefNums are never negative.</summary>
		public const long UnassignedNum=-1;

		///<summary>True if the list holds the "Unassigned" key.</summary>
		public static bool ContainsUnassigned(List<long> listKeys) {
			return listKeys!=null && listKeys.Contains(UnassignedNum);
		}

		///<summary>True when the task queries filter by this clinic or region list: it is not empty (All) and it is not the one-item list holding 0
		///(the Headquarters default). This is the test Tasks.BuildFilterWhereClause applies, and the clinic list wins over the region list.</summary>
		public static bool IsFilteringBy(List<long> listKeys) {
			if(listKeys==null || listKeys.Count==0) {
				return false;
			}
			return !(listKeys.Count==1 && listKeys[0]==0);
		}

		///<summary>Called by Tasks.BuildFilterWhereClause once it has listed the clinics whose tasks it shows (listClinicNumsForQuery, which always
		///holds 0 already). For an "Unassigned" region, adds the clinics of listClinicsUnrestricted (the clinics the user may see) that have no
		///region. Returns true when an "Unassigned" clinic or region is in effect: the query must then keep the clinic condition even when
		///listClinicNumsForQuery holds nothing but 0, which would otherwise mean "do not filter".</summary>
		public static bool AddUnassignedClinicNums(List<long> listClinicNumsForQuery,List<long> listClinicNumsFilter,List<long> listDefNumsRegionFilter,
			List<Clinic> listClinicsUnrestricted)
		{
			if(listClinicNumsForQuery==null) {
				return false;
			}
			if(IsFilteringBy(listClinicNumsFilter)) {
				//Filtering by clinic. Clinic 0 is in listClinicNumsForQuery already, so there is nothing to add.
				return ContainsUnassigned(listClinicNumsFilter);
			}
			if(!IsFilteringBy(listDefNumsRegionFilter) || !ContainsUnassigned(listDefNumsRegionFilter)) {
				return false;
			}
			if(listClinicsUnrestricted!=null) {
				List<long> listClinicNumsNoRegion=listClinicsUnrestricted.FindAll(x => x.Region==0).Select(x => x.ClinicNum).ToList();
				for(int i=0;i<listClinicNumsNoRegion.Count;i++) {
					if(!listClinicNumsForQuery.Contains(listClinicNumsNoRegion[i])) {
						listClinicNumsForQuery.Add(listClinicNumsNoRegion[i]);
					}
				}
			}
			return true;
		}

		///<summary>The text the Task Filter window and the task list show for "Unassigned".</summary>
		public static string GetUnassignedText() {
			return Lans.g("TaskFilters","Unassigned");
		}

		///<summary>The text the task list shows for one key of the clinic filter list: "Unassigned" or the clinic's abbreviation.</summary>
		public static string GetClinicFilterText(long clinicNum) {
			if(clinicNum==UnassignedNum) {
				return GetUnassignedText();
			}
			return Clinics.GetAbbr(clinicNum);
		}

		///<summary>The text the task list shows for one key of the region filter list: "Unassigned" or the region's name.</summary>
		public static string GetRegionFilterText(long defNum) {
			if(defNum==UnassignedNum) {
				return GetUnassignedText();
			}
			return Defs.GetName(DefCat.Regions,defNum);
		}

		///<summary>A dummy clinic for the Task Filter window's Clinic list: ClinicNum is UnassignedNum and Abbr and Description are "Unassigned".</summary>
		public static Clinic GetUnassignedClinic() {
			Clinic clinic=new Clinic();
			clinic.ClinicNum=UnassignedNum;
			clinic.Abbr=GetUnassignedText();
			clinic.Description=GetUnassignedText();
			return clinic;
		}

		///<summary>A dummy region definition for the Task Filter window's Region list: DefNum is UnassignedNum and ItemName is "Unassigned".</summary>
		public static Def GetUnassignedRegion() {
			Def def=new Def();
			def.DefNum=UnassignedNum;
			def.Category=DefCat.Regions;
			def.ItemName=GetUnassignedText();
			return def;
		}

		///<summary>How the Task Filter window shows a chosen patient: "Last, First - PatNum". Empty for no patient.</summary>
		public static string GetPatientText(Patient patient) {
			if(patient==null) {
				return "";
			}
			return patient.GetNameLF()+" - "+patient.PatNum.ToString();
		}
	}
}
