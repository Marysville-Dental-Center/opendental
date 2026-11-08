using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace UnitTests.AlertItems_Tests {
	[TestClass]
	public class AlertItemsTests:TestBase {
		[ClassInitialize]
		public static void SetupClass(TestContext testContext) {
			//Add anything here that you want to run once before the tests in this class run.
		}

		[TestInitialize]
		public void SetupTest() {
			//Clear out the alertitem table before every test.
			AlertSubT.ClearAlertSubTable();
			AlertItemT.ClearAlertItemTable();
			AlertCategoryT.ClearAlertCategoryTable();
			//Creates and inserts an alert category and links for all alert types.
			AlertCategoryT.CreateAlertCategory();
		}
		
		///<summary>Here we test the CheckUniqueAlerts method to determine that the correct number of unique alerts are being returned in the the list
		///utilized to display alerts in the alerts menu.
		///</summary>
		[TestMethod]
		public void AlertItems_GetUniqueAlerts_Addclinic() {
			//This test will check the funcionality of alert items which are marked to show in all clinics(AlertItem.ClinicNum==-1).
			//Expected behaviour is that a user subscribed to all alert categories, and all clinics(AlertSub.ClinicNum==-1), 
			//will see the alert no matter which clinic they are in.
			//In addition, when a new clinic is added, the user will be able to see alerts in that new clinic without the need to reenter
			//FormUserEdit and select "All" again under clinics for alert subscriptions.
			//Create Users
			Userod userAdmin=UserodT.CreateUser();
			Userod userNormal=UserodT.CreateUser();
			//Create Clinics
			List<Clinic> listClinics=new List<Clinic>();
			for(int i=0;i<2;i++) {
				listClinics.Add(ClinicT.CreateClinic());
			}
			//Create AlertItems
			//First alert Item is an alert item for all clinics(ClinicNum==-1).
			CreateAlertItem(true);
			//Second AlertItem is an AlertItem for HQ(ClinicNum==0).
			CreateAlertItem(false);
			List<AlertCategory> listAlertCats=AlertCategories.GetDeepCopy();
			List<AlertSub> listAlertSubNew=new List<AlertSub>();
			foreach(AlertCategory alertCat in listAlertCats) {
				AlertSub alSub=new AlertSub(userAdmin.UserNum,-1,alertCat.AlertCategoryNum);
				listAlertSubNew.Add(alSub);
			}
			AlertSubs.Sync(listAlertSubNew,new List<AlertSub>());
			//Check number of alerts which will display in headquarters clinic.
			//Call CheckUniqueAlerts for user subscribed to all alert categories
			List<AlertItem> listUniqueAlertsAll=AlertItems.GetAlertsItemsForUser(userAdmin.UserNum,0);
			List<AlertItem> listUniqueAlertsOne=AlertItems.GetAlertsItemsForUser(userNormal.UserNum,0);
			//Assert lists are correct
			//UserAdmin should see two alerts, one for the generic headquarters alert and one for the eConnector all clinics alert.
			Assert.AreEqual(2,listUniqueAlertsAll.Count());
			//UserNormal is not subscribed to any clinics or alert categories and should not see any alerts.
			Assert.AreEqual(0,listUniqueAlertsOne.Count());
			//Add clinic
			listClinics.Add(ClinicT.CreateClinic());
			//Check that alert for all clinics is included for userAdmin(subscribed to all clinics)
			listUniqueAlertsAll=AlertItems.GetAlertsItemsForUser(userAdmin.UserNum,listClinics.LastOrDefault().ClinicNum);
			Assert.AreEqual(1,listUniqueAlertsAll.Count());
			//Check new clinic for user who is not subscribed to all alerts. 
			listUniqueAlertsOne=AlertItems.GetAlertsItemsForUser(userNormal.UserNum,listClinics.LastOrDefault().ClinicNum);
			Assert.AreEqual(0,listUniqueAlertsOne.Count());
			//Add new alert for new clinic only.
			CreateAlertItem(false,listClinics.LastOrDefault().ClinicNum);
			//Check that userAdmin sees new alert item in new clinic. Should have 2, one all clinic econnector alert and the new clinic specific alert.
			listUniqueAlertsAll=AlertItems.GetAlertsItemsForUser(userAdmin.UserNum,listClinics.LastOrDefault().ClinicNum);
			Assert.AreEqual(2,listUniqueAlertsAll.Count());
			//Check that userNormal sees no alerts in new clinic, as they are not subscribed to any alert categories, nor clinics.
			listUniqueAlertsOne=AlertItems.GetAlertsItemsForUser(userNormal.UserNum,listClinics.LastOrDefault().ClinicNum);
			Assert.AreEqual(0,listUniqueAlertsOne.Count());
		}

		///<summary>Creates an alert item. Setting allClinicAlert bool to true creates an eConnector alert which will have a clinicNum of -1
		///and appear in all clinics for users subscribed to the alert category containing econnector alerts. </summary>
		private static void CreateAlertItem(bool allClinicAlert,long clinicNum=0) {
			if(allClinicAlert) {
				AlertItems.Insert(new AlertItem {
					Actions=ActionType.MarkAsRead | ActionType.MarkAsRead,
					Description=Lans.g("Econnector","eConnector needs to be restarted"),
					Severity=SeverityType.High,
					Type=AlertType.EConnectorDown,
					ClinicNum=-1,//Show for all clinics
					FormToOpen=FormType.FormEServicesEConnector,
				});
			}
			else if(clinicNum!=0) {
				AlertItems.Insert(new AlertItem() {
					Type=AlertType.Generic,
					Actions=ActionType.MarkAsRead | ActionType.Delete | ActionType.ShowItemValue,
					Description="A generic alert.",
					Severity=SeverityType.Low,
					ItemValue="A generic alert created for testing alert Items for all clinics. ClinicNum==" + clinicNum,
					ClinicNum=clinicNum,
				}); ;
			}
			else {
				AlertItems.CreateGenericAlert("A generic alert.","A generic alert created for testing alert Items for all clinics. ClinicNum==0");
			}
		}
	}
}
