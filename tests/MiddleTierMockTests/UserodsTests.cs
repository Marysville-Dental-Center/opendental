// The three Middle Tier tests of the legacy UserodsTests (UnitTests/UnitTests/UserodsTests.cs), copied here by package S0-T2 of the .NET 10
// plan; the originals and the class's other tests stay in UnitTests. Through OpenDentalServerMockIIS, a wrong password given to
// Userods.CheckUserAndPassword raises FailedAttempts in the database once per call (not a second time for the call's own stale credentials),
// five calls give five, and another Middle Tier call made with the same stale credentials (Computers.UpdateHeartBeat) does not count again.
// Each test keeps its name and its arrange, act and assert; only the harness changed: the s0t2_ copy of ScratchDb's database (ScratchDbCopy.cs)
// instead of the legacy unittest database and TestBase, ScratchDb.UseMockMiddleTier and UseDirectConnection for the legacy
// RunTestsAgainstMiddleTier and RunTestsAgainstDirectConnection, and the clean-up puts back the event, the failed log-in flag, the role and
// the unit test user as the legacy clean-up did. The users and groups each test makes live in the copy only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace MiddleTierMockTests {
	[TestClass]
	public class UserodsTests {

		///<summary>This method will get invoked before every single test.</summary>
		[TestInitialize]
		public void SetupTest() {
			ScratchDbCopy.BeginTest();
		}

		///<summary>This method will get invoked after every single test.</summary>
		[TestCleanup]
		public void Cleanup() {
			ODEvent.Fired-=CredentialsFailedAfterLoginEvent_Fired1;
			ODEvent.IsCredentialsFailedAfterLogin_EventSubscribed=false;
			RemotingClient.HasLoginFailed=false;
			ScratchDb.UseDirectConnection();//The legacy RevertMiddleTierSettingsIfNeeded.
			if(ScratchDb.SkipReason==null) {
				Security.CurUser=Userods.GetFirstOrDefault(x => x.UserName==ScratchDb.UnitTestUserName);
				Security.PasswordTyped=ScratchDb.UnitTestPassword;
			}
			ScratchDbCopy.EndTest();
		}

		[ClassCleanup]
		public static void TearDownClass() {
			ScratchDbCopy.DropCopy();
		}

		[TestMethod]
		public void Userods_CheckUserAndPassword_IncreaseFailedAttemptsAfterUserHasLoggedInButPasswordIsNotCorrect() {
			//First, setup the test scenario.
			//This test is intended to be tested on middle tier.
			long group1=UserGroupT.CreateUserGroup("usergroup1");
			Userod myUser=UserodT.CreateUser(MethodBase.GetCurrentMethod().Name+DateTime.Now.Ticks,"reallystrongpassword",userGroupNumbers:new List<long>() {group1 });
			ScratchDb.UseMockMiddleTier(new OpenPmsDentalBusiness.WebServices.OpenDentalServerMockIIS(user:myUser.UserName,password:myUser.Password));
			Security.CurUser=myUser;
			Security.PasswordTyped="passwordguess#1";
			ODEvent.Fired+=CredentialsFailedAfterLoginEvent_Fired1;
			ODEvent.IsCredentialsFailedAfterLogin_EventSubscribed=true;
			//make a single bad password attempt.
			ODException.SwallowAnyException(() => {
				Userods.CheckUserAndPassword(myUser.UserName,"passwordguess#1",false);
			});
			//Get our user from the DB
			ScratchDb.UseDirectConnection();
			myUser=Userods.GetUserByNameNoCache(myUser.UserName);
			//Asssert that the failed attempt got incremented correctly.
			Assert.AreEqual(1,myUser.FailedAttempts);
		}

		private void CredentialsFailedAfterLoginEvent_Fired1(ODEventArgs e) {
			//If we don't subscribe to this event then the failed event will keep firing over and over.
			RemotingClient.HasLoginFailed=true;
			throw new Exception("Incorrect username and password");
		}

		//Test #3 Middle tier specific. Try with wrong password, try different method that calls CheckUSersAndPassword but so that we don't do it directly.
		//Make sure that we don't get to the lockout winodw, or query the DB to make sure failed attempts doesn't get past 1.
		[TestMethod]
		public void Userods_CheckUserAndPassoword_UpdateFailedAttemptsFromOtherMethods() {
			//First, setup the test scenario.
			long group1=UserGroupT.CreateUserGroup("usergroup1");
			Userod myUser=UserodT.CreateUser(MethodBase.GetCurrentMethod().Name+DateTime.Now.Ticks,"reallystrongpassword",userGroupNumbers:new List<long>() {group1 });
			Security.CurUser=myUser;
			Security.PasswordTyped="passwordguess#1";
			ODEvent.Fired+=CredentialsFailedAfterLoginEvent_Fired1;
			ODEvent.IsCredentialsFailedAfterLogin_EventSubscribed=true;
			ScratchDb.UseMockMiddleTier(new OpenPmsDentalBusiness.WebServices.OpenDentalServerMockIIS(user:myUser.UserName,password:myUser.Password));
			//try once with the wrong password. Failed attempt should get incremented to 1.
			ODException.SwallowAnyException(() => {
				Userods.CheckUserAndPassword(myUser.UserName,"passwordguess#1",false);
			});
			//Get our updated user from the DB.
			ScratchDb.UseDirectConnection();
			myUser=Userods.GetUserByNameNoCache(myUser.UserName);
			//Assert that we only have 1 failed attempt.
			Assert.AreEqual(1,myUser.FailedAttempts);
			//now wait for another method to get called
			ScratchDb.UseMockMiddleTier(new OpenPmsDentalBusiness.WebServices.OpenDentalServerMockIIS(user:myUser.UserName,password:myUser.Password));
			ODException.SwallowAnyException(() => {
				Computers.UpdateHeartBeat(ODEnvironment.MachineName,false);
			});
			ScratchDb.UseDirectConnection();
			//Get our updated user from the DB.
			myUser=Userods.GetUserByNameNoCache(myUser.UserName);
			//Assert that we only have 1 failed attempt.
			Assert.AreEqual(1,myUser.FailedAttempts);
		}

		//Test #4 Similar to #3, call method checkusersandpassword specifically over middle tier assert that failed attempts do get increased to 5.
		[TestMethod]
		public void Userods_CheckUserAndPassoword_UpdateFailedAttemptsTo5() {
			//First, setup the test scenario.
			long group1=UserGroupT.CreateUserGroup("usergroup1");
			Userod myUser=UserodT.CreateUser(MethodBase.GetCurrentMethod().Name+DateTime.Now.Ticks,"reallystrongpassword",userGroupNumbers:new List<long>() {group1 });
			Security.CurUser=myUser;
			Security.PasswordTyped="passwordguess#1";
			ODEvent.Fired+=CredentialsFailedAfterLoginEvent_Fired1;
			ODEvent.IsCredentialsFailedAfterLogin_EventSubscribed=true;
			ScratchDb.UseMockMiddleTier(new OpenPmsDentalBusiness.WebServices.OpenDentalServerMockIIS());
			//try with 5 incorrect passwords. Failed attempt should get incremented to 5.
			for(int i = 1 ;i < 6;i++) {
				ODException.SwallowAnyException(() => {
					try {
						Userods.CheckUserAndPassword(myUser.UserName,"passwordguess#"+i,false);
					}
					catch(Exception e) {

					}
				});
			}
			//Get our updated user from the DB.
			ScratchDb.UseDirectConnection();
			myUser=Userods.GetUserByNameNoCache(myUser.UserName);
			//Assert that there are 5 failed attempts.
			Assert.AreEqual(5,myUser.FailedAttempts);
		}
	}
}
