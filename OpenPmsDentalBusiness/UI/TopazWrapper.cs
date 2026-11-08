using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental.UI {
	///<summary>The Topaz signature pad control (Topaz.SigPlusNET in Topaz's SigPlusNET.dll). That file has no published redistribution terms, so the
	///program never references or ships it: it is loaded at run time from the program folder or from Topaz's own installation, and an office that
	///signs with a Topaz pad installs Topaz's software. Without it, GetTopaz throws a TopazNotInstalledException whose message starts with
	///NOT_INSTALLED_MESSAGE, callers keep a null control, and every other method here does nothing (or returns 0 or "") for a null control. Every
	///call goes to the control's own public methods by name, as the compiled calls did, so signatures behave as before when Topaz is installed.
	///</summary>
	public class TopazWrapper {
		///<summary>Shown when a Topaz signature is to be captured or shown and Topaz's software cannot be loaded.</summary>
		public const string NOT_INSTALLED_MESSAGE="Topaz signature pad software is not installed.";
		///<summary>Topaz's .NET signature control assembly.</summary>
		public const string ASSEMBLY_FILE_NAME="SigPlusNET.dll";
		///<summary>The signature control in that assembly.</summary>
		public const string CONTROL_TYPE_NAME="Topaz.SigPlusNET";
		///<summary>How many folder levels below a "Topaz Systems" folder are searched for the assembly.</summary>
		private const int SEARCH_DEPTH=3;
		private static readonly object _lock=new object();
		private static bool _isLoadAttempted;
		private static Type _typeSigPlusNET;
		private static string _loadError="";
		private static List<string> _listSearchFoldersOverride;

		#region Loading
		///<summary>The folders searched for SigPlusNET.dll, in order: the program folder, every "Topaz Systems" folder under Program Files (x86) and
		///Program Files (and up to three levels below it), and the Windows SigPlus folder. INFERRED: Topaz's installers have put the file in these
		///places; an office whose Topaz software lives elsewhere can copy SigPlusNET.dll into the program folder.</summary>
		public static List<string> GetSearchFolders() {
			if(_listSearchFoldersOverride!=null) {
				return new List<string>(_listSearchFoldersOverride);
			}
			List<string> listFolders=new List<string>();
			listFolders.Add(AppDomain.CurrentDomain.BaseDirectory);
			List<string> listProgramFiles=new List<string>() {
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				Environment.GetEnvironmentVariable("ProgramW6432"),
			};
			foreach(string programFiles in listProgramFiles.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)) {
				AddFolderAndSubfolders(listFolders,Path.Combine(programFiles,"Topaz Systems"),SEARCH_DEPTH);
			}
			string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			if(!string.IsNullOrWhiteSpace(windows)) {
				listFolders.Add(Path.Combine(windows,"SigPlus"));
			}
			return listFolders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		private static void AddFolderAndSubfolders(List<string> listFolders,string folder,int depth) {
			if(!Directory.Exists(folder)) {
				return;
			}
			listFolders.Add(folder);
			if(depth<=0) {
				return;
			}
			string[] arraySubfolders;
			try {
				arraySubfolders=Directory.GetDirectories(folder);
			}
			catch(Exception ex) {
				ex.DoNothing();//A folder we may not list is skipped.
				return;
			}
			foreach(string subfolder in arraySubfolders.OrderBy(x => x,StringComparer.OrdinalIgnoreCase)) {
				AddFolderAndSubfolders(listFolders,subfolder,depth-1);
			}
		}

		///<summary>The first SigPlusNET.dll in GetSearchFolders, or "" when there is none.</summary>
		public static string FindAssemblyPath() {
			foreach(string folder in GetSearchFolders()) {
				string path=Path.Combine(folder,ASSEMBLY_FILE_NAME);
				if(File.Exists(path)) {
					return path;
				}
			}
			return "";
		}

		///<summary>The Topaz.SigPlusNET control type, loaded once from the first SigPlusNET.dll found. Null when Topaz's software is not installed or
		///its assembly could not be loaded (GetNotInstalledMessage says why).</summary>
		public static Type GetControlType() {
			lock(_lock) {
				if(_isLoadAttempted) {
					return _typeSigPlusNET;
				}
				_isLoadAttempted=true;
				string path=FindAssemblyPath();
				if(path=="") {
					_loadError="";
					return null;
				}
				try {
					Assembly assembly=Assembly.LoadFrom(path);
					_typeSigPlusNET=assembly.GetType(CONTROL_TYPE_NAME,throwOnError:false);
					if(_typeSigPlusNET==null || !typeof(Control).IsAssignableFrom(_typeSigPlusNET)) {
						_typeSigPlusNET=null;
						_loadError=path+" has no "+CONTROL_TYPE_NAME+" control.";
					}
				}
				catch(Exception ex) {
					_typeSigPlusNET=null;
					_loadError=path+" could not be loaded: "+ex.Message;
				}
				return _typeSigPlusNET;
			}
		}

		///<summary>True when Topaz's signature control can be created on this computer.</summary>
		public static bool IsInstalled() {
			return GetControlType()!=null;
		}

		///<summary>NOT_INSTALLED_MESSAGE, followed by the load error when a SigPlusNET.dll was found but could not be used.</summary>
		public static string GetNotInstalledMessage() {
			return NOT_INSTALLED_MESSAGE+GetLoadErrorLine();
		}

		///<summary>"" when no SigPlusNET.dll was found, else a new line and why the one found could not be used.</summary>
		public static string GetLoadErrorLine() {
			lock(_lock) {
				return _loadError=="" ? "" : "\r\n"+_loadError;
			}
		}

		///<summary>Forgets the loaded control type so the next call searches again. With a list, only those folders are searched from then on; null
		///restores the default search. For tests.</summary>
		public static void ResetLoader(List<string> listSearchFolders=null) {
			lock(_lock) {
				_listSearchFoldersOverride=listSearchFolders==null ? null : new List<string>(listSearchFolders);
				_isLoadAttempted=false;
				_typeSigPlusNET=null;
				_loadError="";
			}
		}

		///<summary>Calls the control's public instance method of that name whose parameter count matches, converting the arguments to its parameter
		///types (an exact match is preferred). Exceptions thrown by the method are rethrown as they are, as a compiled call would.</summary>
		private static object Call(Control topaz,string methodName,params object[] arrayArgs) {
			List<MethodInfo> listMethodInfos=topaz.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance)
				.Where(x => x.Name==methodName && x.GetParameters().Length==arrayArgs.Length).ToList();
			MethodInfo methodInfo=listMethodInfos.FirstOrDefault(x => x.GetParameters().Select(y => y.ParameterType)
				.SequenceEqual(arrayArgs.Select(y => y.GetType())))
				??listMethodInfos.FirstOrDefault();
			if(methodInfo==null) {
				throw new MissingMethodException(topaz.GetType().FullName,methodName);
			}
			ParameterInfo[] arrayParameterInfos=methodInfo.GetParameters();
			object[] arrayConverted=new object[arrayArgs.Length];
			for(int i=0;i<arrayArgs.Length;i++) {
				Type typeParameter=arrayParameterInfos[i].ParameterType;
				if(typeParameter.IsInstanceOfType(arrayArgs[i])) {
					arrayConverted[i]=arrayArgs[i];
				}
				else if(typeParameter.IsEnum) {
					arrayConverted[i]=Enum.ToObject(typeParameter,arrayArgs[i]);
				}
				else {
					arrayConverted[i]=Convert.ChangeType(arrayArgs[i],typeParameter);
				}
			}
			try {
				return methodInfo.Invoke(topaz,arrayConverted);
			}
			catch(TargetInvocationException ex) when(ex.InnerException!=null) {
				ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
				throw;//Not reached.
			}
		}

		///<summary>Thrown by GetTopaz when Topaz's software is not installed. The message is GetNotInstalledMessage.</summary>
		public class TopazNotInstalledException:Exception {
			public TopazNotInstalledException(string message):base(message) {
			}
		}
		#endregion Loading

		#region GetTopaz... Methods
		//The plugin calls in this class were added in order to use a Topaz pad in a RDP/Citrix remote session environment so that signature pads can be
		//plugged in on the client computer while the signature displays in the SigPlusNET UI control on the terminal server.  The signature pad and the
		//UI control are always kept in sync, i.e. the state, encryption and compression modes, key data and key string, signature string etc.  This means
		//the GetTopaz... methods could retrieve data from the UI control and not the remote signature pad.  The GetTopazNumberOfTabletPoints method returns
		//0 if sent to the remote device, so it must be called on the local UI control.

		///<summary>A new Topaz signature control. Throws a TopazNotInstalledException when Topaz's software is not installed.</summary>
		public static Control GetTopaz() {
			Type type=GetControlType();
			if(type==null) {
				throw new TopazNotInstalledException(GetNotInstalledMessage());
			}
			Control retVal=(Control)Activator.CreateInstance(type);
			retVal.HandleDestroyed+=new EventHandler(topazWrapper_HandleDestroyed);
			return retVal;
		}

		///<summary>This allows third-party software to unload device/clean-up.</summary>
		private static void topazWrapper_HandleDestroyed(object sender,EventArgs e) {
			Plugins.HookMethod(null,"TopazWrapper.topazWrapper_HandleDestroyed");
		}

		///<summary>0=disable signature capture.  1=enable capture.</summary>
		public static int GetTopazState(Control topaz) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return 0;
			}
			int stateCur = 0;
			object[] paramArray = { topaz,stateCur };
			if(Plugins.HookMethod(null,"TopazWrapper.GetTopazState",paramArray)) {
				stateCur=(int)paramArray[1];
			}
			else {
				stateCur=Convert.ToInt32(Call(topaz,"GetTabletState"));
			}
			return stateCur;
		}

		///<summary>Returns 0 if the signature is not valid, i.e. the compression or encryption modes are not the same used when the signature was saved,
		///or the key data is different than that used to save the signature, or the signature string is blank, etc.  Valid signatures should consist of
		///at least 200 points, as this represents approximately 1 second of active signature time.  We will consider any value greater than 0 to mean a
		///signature is valid.</summary>
		public static int GetTopazNumberOfTabletPoints(Control topaz) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return 0;
			}
			return Convert.ToInt32(Call(topaz,"NumberOfTabletPoints"));
		}

		public static string GetTopazString(Control topaz) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return "";
			}
			//if in remote session, always get the sig string from the UI control, not the remote sig pad, because a form can have more than one sig box
			//and the sig pad will only have the most recent sig string.
			return (string)Call(topaz,"GetSigString");
		}
		#endregion GetTopaz... Methods

		#region SetTopaz... Methods
		//The SetTopaz... methods have a plugin call in order for HSB signature pads to be used in a RDP/Citrix remote session environment.  It may not be
		//necessary to send all of the SetTopaz... methods to the device connected to the client machine.  The encryption and compression modes and the
		//key data and key string are only necessary when saving the signature, not during signature capture.  Since the captured signature is duplicated
		//on the server in the SigPlusNET control in the OD UI, we can set the compression, encryption, key data, and key string on the control before
		//getting the signature string and validate it using the control as well.  We will leave the plugin calls just in case they are needed in the future.

		public static void ClearTopaz(Control topaz) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			Call(topaz,"ClearTablet");
			Call(topaz,"SetTabletLogicalXSize",2000);
			Call(topaz,"SetTabletLogicalYSize",600);
			Plugins.HookAddCode(null,"TopazWrapper.ClearTopaz_end",topaz);
		}

		public static void SetTopazCompressionMode(Control topaz,int compressionMode) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			Call(topaz,"SetSigCompressionMode",compressionMode);
			Plugins.HookAddCode(null,"TopazWrapper.SetTopazCompressionMode_end",topaz,compressionMode);
		}

		public static void SetTopazEncryptionMode(Control topaz,int encryptionMode) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			Call(topaz,"SetEncryptionMode",encryptionMode);
			Plugins.HookAddCode(null,"TopazWrapper.SetTopazEncryptionMode_end",topaz,encryptionMode);
		}

		public static void SetTopazKeyString(Control topaz,string str) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			Call(topaz,"SetKeyString",str);
			Plugins.HookAddCode(null,"TopazWrapper.SetTopazKeyString_end",topaz,str);
		}

		public static void SetTopazAutoKeyANSIData(Control topaz,string data) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			Call(topaz,"AutoKeyStart");
			Call(topaz,"SetAutoKeyANSIData",data);
			Call(topaz,"AutoKeyFinish");
			Plugins.HookAddCode(null,"TopazWrapper.SetTopazAutoKeyANSIData_end",topaz,data);
		}

		///<summary>Topaz Systems Inc. deprecated SetAutoKeyData() and AutoKeyAddData(). Use SetAutoKeyANSIData() instead.</summary>
		public static void SetTopazAutoKeyData(Control topaz,string data) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			Call(topaz,"AutoKeyStart");
			Call(topaz,"SetAutoKeyData",data);
			Call(topaz,"AutoKeyFinish");
			Plugins.HookAddCode(null,"TopazWrapper.SetTopazAutoKeyData_end",topaz,data);
		}

		public static void SetTopazSigString(Control topaz,string signature) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			try {
				Call(topaz,"SetSigString",signature);
			}
			catch(Exception ex) {
				//The signature could have become corrupted within the database (or from wherever it is coming from).
				//The Topaz dll can throw exceptions for invalid signatures even if the signature data is a valid ASCII string.
				//E.g. A customer had a plethora of treatment plan signatures with a value of '1' which is a valid ASCII string but is not a valid signature.
				//This caused Topaz to throw a System.IndexOutOfRangeException: 'Index was outside the bounds of the array.'
				//No 'tablet points' should be present on the signature pad at this time which is how calling methods will know the signature was invalid.
				ex.DoNothing();//Do nothing so that the program doesn't crash.
			}
			//this hook shouldn't be implemented or necessary, we only set/get the sig string on the local UI control, so this command shouldn't be sent to
			//the remote connection sig pad
			Plugins.HookAddCode(null,"TopazWrapper.SetTopazSigString_end",topaz,signature);
		}

		///<summary>0=disable signature capture.  1=enable.</summary>
		public static void SetTopazState(Control topaz,int state) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			//the topaz state should only be set on the actual sig pad device, not the local UI control if using in remote session
			if(Plugins.HookMethod(null,"TopazWrapper.SetTopazState",topaz,state)) {
				return;
			}
			Call(topaz,"SetTabletState",state);
		}
		#endregion SetTopaz... Methods

		#region Helper Methods

		public static void FillSignatureANSI(Control topaz,string keyData,string signature,OpenPmsDental.UI.SignatureBoxWrapper.SigMode sigMode) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			ClearTopaz(topaz);
			SetTopazCompressionMode(topaz,0);
			SetTopazEncryptionMode(topaz,0);
			SetTopazKeyString(topaz,"0000000000000000");//Clear out the key string
			SetTopazEncryptionMode(topaz,2);//high encryption
			SetTopazCompressionMode(topaz,2);//high compression
			string hashedKeyData;
			switch(sigMode) {
				case OpenPmsDental.UI.SignatureBoxWrapper.SigMode.TreatPlan:
					hashedKeyData=TreatPlans.GetHashStringForSignature(keyData);//Passed in key data has not been hashed yet.
					SetTopazAutoKeyANSIData(topaz,hashedKeyData);
					break;
				case OpenPmsDental.UI.SignatureBoxWrapper.SigMode.OrthoChart:
					hashedKeyData=OrthoCharts.GetHashStringForSignature(keyData);//Passed in key data has not been hashed yet.
					SetTopazAutoKeyANSIData(topaz,hashedKeyData);
					break;
				case OpenPmsDental.UI.SignatureBoxWrapper.SigMode.Document:
				case OpenPmsDental.UI.SignatureBoxWrapper.SigMode.Default:
				default:
					SetTopazAutoKeyANSIData(topaz,keyData);
					break;
			}
			SetTopazSigString(topaz,signature);
		}

		///<summary>Use as a last resort. Enumerates all encodings to find the first encoding that successfully decrypts the signature.</summary>
		public static void FillSignatureEncodings(Control topaz,string keyData,string signature,OpenPmsDental.UI.SignatureBoxWrapper.SigMode sigMode) {
			if(topaz==null) {//Topaz's software is not installed (GetTopaz threw), so there is no control.
				return;
			}
			//Figure out if key data needs to be hashed depending on the SigMode.
			string keyDataHashed;
			switch(sigMode) {
				case OpenPmsDental.UI.SignatureBoxWrapper.SigMode.TreatPlan:
					keyDataHashed=TreatPlans.GetHashStringForSignature(keyData);
					break;
				case OpenPmsDental.UI.SignatureBoxWrapper.SigMode.OrthoChart:
					keyDataHashed=OrthoCharts.GetHashStringForSignature(keyData);
					break;
				case OpenPmsDental.UI.SignatureBoxWrapper.SigMode.Document:
				case OpenPmsDental.UI.SignatureBoxWrapper.SigMode.Default:
				default:
					keyDataHashed=keyData;
					break;
			}
			//Use reflection to directly set encoded bytes as the KeyData that Topaz should use when decrypting the signature.
			FieldInfo fieldInfoSig;
			object topazSignature;
			MethodInfo methodInfoSetAutoKeyData;
			try {
				//Get the field info for SigPlusNET's private Sig field.
				fieldInfoSig=topaz.GetType().GetField("Sig",BindingFlags.NonPublic | BindingFlags.Instance);
				//Get the value of the Sig field from the instance of the SigPlusNET object passed in.
				topazSignature=fieldInfoSig.GetValue(topaz);
				//Get the method info of the private SetAutoKeyData() method that accepts a byte array from the Sig field.
				//This method allows us to bypass the incorrect encodings that are forced on us in the public methods.
				methodInfoSetAutoKeyData=fieldInfoSig.FieldType.GetMethod("SetAutoKeyData",BindingFlags.NonPublic | BindingFlags.Instance,null,new Type[] { typeof(Byte[]) },null);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return;
			}
			List<Encoding> listEncodings=Encoding.GetEncodings().Select(x => x.GetEncoding()).ToList();
			for(int i=0;i<listEncodings.Count;i++) {
				//Completely clear and reset the signature pad in-between each encoding.
				ClearTopaz(topaz);
				SetTopazCompressionMode(topaz,0);
				SetTopazEncryptionMode(topaz,0);
				SetTopazKeyString(topaz,"0000000000000000");//Clear out the key string
				SetTopazEncryptionMode(topaz,2);//high encryption
				SetTopazCompressionMode(topaz,2);//high compression
				try {
					Call(topaz,"AutoKeyStart");
					//Invoke SetAutoKeyData() with the encoded bytes of keyDataHashed.
					methodInfoSetAutoKeyData.Invoke(topazSignature,new object[] { listEncodings[i].GetBytes(keyDataHashed) });
					Call(topaz,"AutoKeyFinish");
				}
				catch(Exception ex) {
					ex.DoNothing();
					continue;
				}
				SetTopazSigString(topaz,signature);
				if(GetTopazNumberOfTabletPoints(topaz) > 0) {
					break;
				}
			}
		}

		#endregion

	}
}
