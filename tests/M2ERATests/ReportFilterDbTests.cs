// v25_4-reports-era-autoprocessed-filters on a scratch database: the ERAs Automatically Processed report reads each ERA's text to hide ERAs whose
// claims are all predeterminations, and hides $0 payments from etrans835.InsPaid. Synthetic 835s only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2ERATests {
	[TestClass]
	public class ReportFilterDbTests {
		[TestInitialize]
		public void Init() {
			EraDb.Begin();
		}

		private static EraClp Clp(string claimIdentifier,string clp02,decimal insPaid) {
			EraClp eraClp=new EraClp() { ClaimIdentifier=claimIdentifier,Clp02=clp02 };
			eraClp.ListLines.Add(new EraLine("Z0120",100,insPaid));
			return eraClp;
		}

		///<summary>Stores the 835 as an ERA and its etrans835 row, the way the ERA list does (EtransL.AddMissingEtrans835s).</summary>
		private static (Etrans Etrans,Etrans835 Etrans835) InsertEra(string text) {
			Etrans etrans=Etranss.CreateEtrans(DateTime.Now,0,text,0);
			etrans.Etype=EtransType.ERA_835;
			etrans.TranSetId835="0001";
			Etranss.Insert(etrans);
			X835 x835=new X835(etrans,text,etrans.TranSetId835);
			Etrans835 etrans835=new Etrans835() { EtransNum=etrans.EtransNum };
			Etrans835s.Upsert(etrans835,x835);
			return (etrans,Etrans835s.GetByEtransNums(etrans.EtransNum).Single());
		}

		[TestMethod]
		public void RemoveHidden_ReadsTheEraTextFromTheDatabase() {
			(Etrans Etrans,Etrans835 Etrans835) eraPaid=InsertEra(Era835Builder.Build("SYNRPT1",Clp("RPTCL1","1",60)));
			(Etrans Etrans,Etrans835 Etrans835) eraPreauth=InsertEra(Era835Builder.Build("SYNRPT2",Clp("RPTPA1","25",0),Clp("RPTPA2","25",0)));
			(Etrans Etrans,Etrans835 Etrans835) eraZero=InsertEra(Era835Builder.Build("SYNRPT3",Clp("RPTCL2","4",0)));//Denied: $0, not a preauth.
			Assert.AreEqual(60,eraPaid.Etrans835.InsPaid,0.001);
			Assert.AreEqual(0,eraZero.Etrans835.InsPaid,0.001);
			var arrayEras=new[] { eraPaid,eraPreauth,eraZero };
			//PreAuths hidden.
			List<Etrans> listEtrans=arrayEras.Select(x => x.Etrans).ToList();
			List<Etrans835> listEtrans835s=arrayEras.Select(x => x.Etrans835).ToList();
			Etrans835s.RemoveHiddenForAutoProcessedReport(listEtrans,listEtrans835s,doShowPreAuths:false,doShowZeroPayments:true);
			CollectionAssert.AreEqual(new long[] { eraPaid.Etrans.EtransNum,eraZero.Etrans.EtransNum },listEtrans.Select(x => x.EtransNum).ToArray());
			CollectionAssert.AreEqual(listEtrans.Select(x => x.EtransNum).ToArray(),listEtrans835s.Select(x => x.EtransNum).ToArray());
			//$0 payments hidden (the preauth-only ERA pays $0 too).
			listEtrans=arrayEras.Select(x => x.Etrans).ToList();
			listEtrans835s=arrayEras.Select(x => x.Etrans835).ToList();
			Etrans835s.RemoveHiddenForAutoProcessedReport(listEtrans,listEtrans835s,doShowPreAuths:true,doShowZeroPayments:false);
			CollectionAssert.AreEqual(new long[] { eraPaid.Etrans.EtransNum },listEtrans.Select(x => x.EtransNum).ToArray());
			//Both shown: all three, as in 24.3.
			listEtrans=arrayEras.Select(x => x.Etrans).ToList();
			listEtrans835s=arrayEras.Select(x => x.Etrans835).ToList();
			Etrans835s.RemoveHiddenForAutoProcessedReport(listEtrans,listEtrans835s,doShowPreAuths:true,doShowZeroPayments:true);
			Assert.AreEqual(3,listEtrans.Count);
		}

		[TestMethod]
		public void Etrans835sGetFiltered_StillReturnsEveryEra() {
			//The filters work after the query, so Etrans835s.GetFiltered (also used by the ERA list) is unchanged.
			(Etrans Etrans,Etrans835 Etrans835) eraPreauth=InsertEra(Era835Builder.Build("SYNRPT4",Clp("RPTPA3","25",0)));
			List<Etrans835> listEtrans835s=Etrans835s.GetFiltered(DateTime.Today.AddDays(-1),DateTime.Today,"","",-1,-1,"");
			Assert.IsTrue(listEtrans835s.Any(x => x.EtransNum==eraPreauth.Etrans.EtransNum));
		}
	}
}
