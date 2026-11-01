// The Middle Tier's dispatch by name, in process: a DTO is serialized as the client sends it, read back as the server does, and resolved to the
// method the server would run. No database, web server or network; synthetic values only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BSERVERTests {
	///<summary>The names the Middle Tier sends are built from the types (Meth, Security.LogInWebMethodName), and the server takes the first part
	///as the assembly name and the rest as namespace, class and method (DtoProcessor). These tests take the server's own resolution
	///(DtoProcessor.DtoInformation) through a full serialize and deserialize, so a hard-coded name that a rename leaves behind fails here.</summary>
	[TestClass]
	public class MiddleTierNamesTests {
		[TestMethod]
		public void LogInWeb_RoundTripsAndResolvesToSecurityLogInWeb() {
			MethodInfo methodLogInWeb=typeof(Security).GetMethod(nameof(Security.LogInWeb));
			DtoGetObject dto=new DtoGetObject();
			dto.Credentials=new Credentials { Username="synthuser",Password="synthpass" };
			dto.ComputerName="SYNTH-PC";
			dto.MethodName=Security.LogInWebMethodName;
			dto.ObjectType=typeof(Userod).FullName;
			dto.Params=DtoObject.ConstructArray(methodLogInWeb,new object[] { "synthuser","synthpass","","24.3.63.0",false });
			DataTransferObject dtoRead=DataTransferObject.Deserialize(dto.Serialize());
			Assert.IsInstanceOfType(dtoRead,typeof(DtoGetObject),"The DTO type is found from the XML node name.");
			Assert.AreEqual(typeof(Security).FullName+".LogInWeb",dtoRead.MethodName);
			Assert.AreEqual("synthuser",dtoRead.Credentials.Username);
			object dtoInformation=ResolveOnServer(dtoRead);
			Assert.AreEqual(typeof(Security).Assembly.GetName().Name,GetField<string>(dtoInformation,"AssemblyName"));
			//DtoProcessor treats this class and method specially (it passes the server's config path), by these two names.
			Assert.AreEqual(nameof(Security),GetField<string>(dtoInformation,"ClassName"));
			Assert.AreEqual(nameof(Security.LogInWeb),GetField<string>(dtoInformation,"MethodName"));
			Assert.AreEqual(methodLogInWeb,GetField<MethodInfo>(dtoInformation,"MethodInfo"));
			object[] arrayParams=GetField<object[]>(dtoInformation,"ParamObjs");
			Assert.AreEqual("synthuser",arrayParams[0]);
			Assert.AreEqual(false,arrayParams[4]);
		}

		///<summary>Clients older than 14.3 send "Class.Method"; the server puts its own assembly name in front.</summary>
		[TestMethod]
		public void LegacyTwoPartName_ResolvesInTheBusinessLayer() {
			MethodInfo methodLogInWeb=typeof(Security).GetMethod(nameof(Security.LogInWeb));
			DtoGetObject dto=new DtoGetObject();
			dto.Credentials=new Credentials { Username="synthuser",Password="synthpass" };
			dto.MethodName=nameof(Security)+"."+nameof(Security.LogInWeb);
			dto.Params=DtoObject.ConstructArray(methodLogInWeb,new object[] { "synthuser","synthpass","","24.3.63.0",false });
			object dtoInformation=ResolveOnServer(DataTransferObject.Deserialize(dto.Serialize()));
			Assert.AreEqual(methodLogInWeb,GetField<MethodInfo>(dtoInformation,"MethodInfo"));
		}

		///<summary>A name built the way Meth builds it, with a business-layer type as a parameter: the parameter's type is found by its full name,
		///whose first part must be its assembly's name.</summary>
		[TestMethod]
		public void MethStyleName_WithABusinessTypeParameter_Resolves() {
			MethodInfo methodUpdate=typeof(Patients).GetMethod(nameof(Patients.Update),new[] { typeof(Patient),typeof(Patient) });
			Assert.IsNotNull(methodUpdate);
			Patient patient=new Patient { PatNum=12,LName="Synthetic",FName="Pat" };
			DtoGetBool dto=new DtoGetBool();
			dto.Credentials=new Credentials { Username="synthuser",Password="synthpass" };
			dto.MethodName=methodUpdate.DeclaringType.Namespace+"."+methodUpdate.DeclaringType.Name+"."+methodUpdate.Name;
			dto.Params=DtoObject.ConstructArray(methodUpdate,new object[] { patient,patient.Copy() });
			object dtoInformation=ResolveOnServer(DataTransferObject.Deserialize(dto.Serialize()));
			Assert.AreEqual(methodUpdate,GetField<MethodInfo>(dtoInformation,"MethodInfo"));
			Type[] arrayParamTypes=GetField<Type[]>(dtoInformation,"ParamTypes");
			Assert.AreEqual(typeof(Patient),arrayParamTypes[0]);
			Assert.AreEqual("Synthetic",((Patient)GetField<object[]>(dtoInformation,"ParamObjs")[0]).LName);
		}

		///<summary>A class in a sub-namespace: the server takes the first part as the assembly and keeps "Eclaims.Eclaims" as the class.</summary>
		[TestMethod]
		public void SubNamespaceName_KeepsTheRestOfTheNamespaceWithTheClass() {
			MethodInfo method=typeof(OpenPmsDentalBusiness.Eclaims.Eclaims).GetMethod(nameof(OpenPmsDentalBusiness.Eclaims.Eclaims.GetNoProceduresOnClaimMessage));
			DtoGetString dto=new DtoGetString();
			dto.Credentials=new Credentials { Username="synthuser",Password="synthpass" };
			dto.MethodName=method.DeclaringType.Namespace+"."+method.DeclaringType.Name+"."+method.Name;
			dto.Params=DtoObject.ConstructArray(method,new object[0]);
			object dtoInformation=ResolveOnServer(DataTransferObject.Deserialize(dto.Serialize()));
			Assert.AreEqual(typeof(Patient).Assembly.GetName().Name,GetField<string>(dtoInformation,"AssemblyName"));
			Assert.AreEqual("Eclaims.Eclaims",GetField<string>(dtoInformation,"ClassName"));
			Assert.AreEqual(method,GetField<MethodInfo>(dtoInformation,"MethodInfo"));
		}

		#region Helpers
		///<summary>Builds DtoProcessor's private DtoInformation for the DTO: what the server resolves before it runs anything.</summary>
		private static object ResolveOnServer(DataTransferObject dto) {
			Type typeDtoInformation=typeof(DtoProcessor).GetNestedType("DtoInformation",BindingFlags.NonPublic);
			Assert.IsNotNull(typeDtoInformation,"DtoProcessor.DtoInformation was not found.");
			ConstructorInfo constructorInfo=typeDtoInformation.GetConstructor(new[] { typeof(DataTransferObject) });
			Assert.IsNotNull(constructorInfo);
			try {
				return constructorInfo.Invoke(new object[] { dto });
			}
			catch(TargetInvocationException ex) {
				throw ex.InnerException??ex;
			}
		}

		private static T GetField<T>(object obj,string fieldName) {
			FieldInfo fieldInfo=obj.GetType().GetField(fieldName,BindingFlags.Public|BindingFlags.Instance);
			Assert.IsNotNull(fieldInfo,fieldName);
			return (T)fieldInfo.GetValue(obj);
		}
		#endregion Helpers
	}
}
