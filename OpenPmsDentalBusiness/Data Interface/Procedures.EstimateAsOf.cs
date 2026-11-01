// M2-EST: treatment plan estimates as of the scheduled appointment date (Open Dental 25.4): no estimate after the subscriber's coverage ended.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	public partial class Procedures {
		///<summary>Set by ScopeCoverageEndCheck for the code running on this thread.</summary>
		[ThreadStatic]
		private static bool _isCoverageEndChecked;

		///<summary>Until the returned object is disposed, estimates that ComputeEstimates figures on this thread give no insurance estimate for a plan whose
		///subscriber's coverage ended (inssub.DateTerm) before the procedure's date. The Treatment Plan module opens it around the estimate of a
		///procedure on a scheduled appointment, whose date is then the appointment date (Open Dental 25.4, rule 5 of the spec). Everywhere else
		///estimates ignore DateTerm, as before. Scopes nest; disposing restores the previous state.</summary>
		public static IDisposable ScopeCoverageEndCheck(bool isChecked=true) {
			Meth.NoCheckMiddleTierRole();
			return new CoverageEndScope(isChecked);
		}

		///<summary>True when the subscriber's coverage ends (DateTerm) before the date. A blank DateTerm means the coverage has not ended.</summary>
		public static bool IsCoverageEnded(InsSub insSub,DateTime dateAsOf) {
			Meth.NoCheckMiddleTierRole();
			if(insSub==null || insSub.DateTerm.Year<1880 || dateAsOf.Year<1880) {
				return false;
			}
			return insSub.DateTerm.Date<dateAsOf.Date;
		}

		///<summary>Called by ComputeForOrdinal for every claimproc it estimates. Does nothing outside ScopeCoverageEndCheck. Inside it, zeroes the
		///estimate of an unsent estimate whose subscriber's coverage ended before the procedure's date, so that later plans and later procedures of the
		///treatment plan see nothing paid by it.</summary>
		private static void ZeroEstimateIfCoverageEnded(ClaimProc claimProc,Procedure procedure,List<InsSub> listInsSubs,bool hasEstimateCalculation) {
			if(!_isCoverageEndChecked || !hasEstimateCalculation || claimProc==null || procedure==null || listInsSubs==null) {
				return;
			}
			if(claimProc.ClaimNum!=0 || !claimProc.Status.In(ClaimProcStatus.Estimate,ClaimProcStatus.CapEstimate)) {
				return;
			}
			InsSub insSub=listInsSubs.FirstOrDefault(x => x.InsSubNum==claimProc.InsSubNum);
			if(!IsCoverageEnded(insSub,procedure.ProcDate)) {
				return;
			}
			ZeroEstimateForCoverageEnded(claimProc,insSub.DateTerm);
		}

		///<summary>Zeroes the computed estimate fields of a claimproc whose coverage ended and adds "Coverage ended (date)" to its estimate note. The
		///user's overrides are kept, as they are for frequency limitations' notes in the Treatment Plan grid (INFERRED).</summary>
		public static void ZeroEstimateForCoverageEnded(ClaimProc claimProc,DateTime dateTerm) {
			Meth.NoCheckMiddleTierRole();
			claimProc.BaseEst=0;
			claimProc.InsEstTotal=0;
			claimProc.DedEst=0;
			claimProc.DedApplied=0;
			if(claimProc.Status==ClaimProcStatus.CapEstimate) {
				claimProc.WriteOff=0;
				claimProc.WriteOffEst=0;
			}
			else if(claimProc.WriteOffEst!=-1) {
				claimProc.WriteOffEst=0;//No contract with the carrier after the coverage ended, so no write-off.
			}
			string note=Lans.g("Procedures","Coverage ended")+" "+dateTerm.ToShortDateString();
			if(claimProc.EstimateNote==null) {
				claimProc.EstimateNote="";
			}
			if(claimProc.EstimateNote.Contains(note)) {
				return;
			}
			if(claimProc.EstimateNote!="") {
				claimProc.EstimateNote+=", ";
			}
			claimProc.EstimateNote+=note;
		}

		private class CoverageEndScope:IDisposable {
			private readonly bool _isCheckedPrevious;
			private bool _isDisposed;

			public CoverageEndScope(bool isChecked) {
				_isCheckedPrevious=_isCoverageEndChecked;
				_isCoverageEndChecked=isChecked;
			}

			public void Dispose() {
				if(_isDisposed) {
					return;
				}
				_isDisposed=true;
				_isCoverageEndChecked=_isCheckedPrevious;
			}
		}
	}
}
