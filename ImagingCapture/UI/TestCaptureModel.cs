// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;

namespace ImagingCapture.UI {
	///<summary>The imaging device Test capture runs for: the fields of its ImagingDevice row that it needs.</summary>
	public sealed class TestCaptureDevice {
		public long ImagingDeviceNum { get; set; }
		public string Description { get; set; }="";
		///<summary>The TWAIN ProductName (ImagingDevice.TwainName).</summary>
		public string TwainName { get; set; }="";
		///<summary>The workstation the row is bound to, or "" for every computer.</summary>
		public string ComputerName { get; set; }="";
	}

	///<summary>A mount template's name and default category (MountDef.Description and DefaultCat), for the X-ray category check.</summary>
	public sealed class MountTemplateCategory {
		public string Description { get; set; }="";
		public long DefaultCat { get; set; }
	}

	///<summary>Where Test capture is, from the session's status events.</summary>
	public enum TestCaptureState {
		///<summary>The sensor software is not open.</summary>
		Closed,
		Opening,
		///<summary>Open in state 4: Take test image enables it.</summary>
		Open,
		///<summary>Enabled (states 5 to 7, or stopping).</summary>
		Enabled,
		Closing,
	}

	///<summary>The state and decisions of FrmTestCapture (design 7.8, 9.3, 13 step 8), so the window stays thin and this can be tested without
	///a sensor, a window or a database. A test image is taken of a phantom or a known ramp, without a patient; it is never saved to a patient
	///(the library keeps its journal copy). The model shows the effective device settings and their gated values with the hash, the delivered
	///bit depth, the measurements of TestCaptureAnalysis, and the 'P' category warning for the X-ray category, and builds the verification
	///record. All members are called on the UI thread; the window marshals the session's events to it.</summary>
	public sealed class TestCaptureModel {
		///<summary>The patient name the quarantine sidecars of a test capture record: there is no patient.</summary>
		public const string QuarantinePatientName="Test capture (no patient)";
		public const string TargetLabel="Test capture (no patient)";
		public const string Instructions="Place the phantom or the known ramp on the sensor, press Take test image, and expose when the sensor "
			+"software is ready. The image is measured here and never saved to a patient.";

		private readonly ImageCategoryRule _imageCategoryRule;
		private readonly List<MountTemplateCategory> _listMountTemplates;
		private readonly LocalCaptureConfig _localCaptureConfig;
		private bool _isByteOrderConfirmed;
		private bool _isPolarityConfirmed;
		private long _categoryDefNum;
		private int _bitsToRequest;

		///<summary>device: the saved ImagingDevice (ImagingDeviceNum above 0). localCaptureConfig: capture.ini as loaded; the test runs with the
		///settings read from it now, and the record carries their hash. computerName: this computer. unassignedText: the quarantine line
		///(CaptureQuarantine.GetUnassignedText), or null. settingsCheck: the hash check of design 9.3 for the device on this computer
		///(CaptureSettingsCheck); unless it is Verified the test runs with the safe values and writes no record. Null counts as not checked.</summary>
		public TestCaptureModel(TestCaptureDevice device,LocalCaptureConfig localCaptureConfig,string computerName,ImageCategoryRule imageCategoryRule,
			IEnumerable<MountTemplateCategory> listMountTemplates,string unassignedText,CaptureSettingsCheck settingsCheck)
		{
			Device=device??throw new ArgumentNullException(nameof(device));
			_localCaptureConfig=localCaptureConfig??throw new ArgumentNullException(nameof(localCaptureConfig));
			_imageCategoryRule=imageCategoryRule??throw new ArgumentNullException(nameof(imageCategoryRule));
			_listMountTemplates=(listMountTemplates??Enumerable.Empty<MountTemplateCategory>()).Where(x => x!=null).ToList();
			ComputerName=computerName??"";
			UnassignedText=unassignedText;
			SettingsCheck=settingsCheck??CaptureSettingsCheck.Unknown(device.ImagingDeviceNum,ComputerName,"they were not checked");
			//Design 9.3: the session path, Test capture included, ignores a gated block that is not verified and uses the safe values.
			Options=SettingsCheck.GetOptionsInEffect(TwainCaptureOptions.FromConfig(localCaptureConfig,device.ImagingDeviceNum));
			try {
				GatedCanonicalText=GatedSettingsHash.GetCanonicalText(localCaptureConfig,device.ImagingDeviceNum);
				GatedSettingsHashValue=GatedSettingsHash.ComputeFromCanonicalText(GatedCanonicalText);
				ImageSettingsHashValue=CaptureBitDepthDecision.GetImageSettingsHash(Options,device.ImagingDeviceNum);
			}
			catch(Exception ex) {
				GatedCanonicalText="";
				GatedSettingsHashValue="";
				ImageSettingsHashValue="";
				HashError="The device's capture settings could not be hashed: "+ex.Message;
			}
			//The device's Bits (16 by default), unless this computer forces 8 (Allow16Bit=0, or the safe values while the settings are not verified).
			_bitsToRequest=Options.Allow16Bit ? TwainBitDepth.Normalize(Options.Bits) : TwainBitDepth.Bits8;
			_categoryDefNum=GetDefaultCategory(_listMountTemplates);
			StatusText="Sensor software not open.";
		}

		#region Properties
		public TestCaptureDevice Device { get; }
		///<summary>The [Device.N] and [Twain] settings in effect, as the capture session reads them: the file's when SettingsCheck is Verified,
		///otherwise the safe values.</summary>
		public TwainCaptureOptions Options { get; }
		///<summary>Whether the gated settings are the ones last saved in the Capture settings screen (design 9.3).</summary>
		public CaptureSettingsCheck SettingsCheck { get; }
		///<summary>"Capture settings were changed outside Open Dental; review them in Setup. ...", or "" when the settings are verified.</summary>
		public string SettingsCheckText => SettingsCheck.Text;
		///<summary>GatedSettingsHash.Compute for the device, or "" when it could not be computed (HashError). The record keeps it for the audit
		///trail; it does not compare it.</summary>
		public string GatedSettingsHashValue { get; }
		///<summary>CaptureImageSettingsHash of Options ([Device.N] Bits, ByteOrder16, Polarity in effect), which the record covers, or "" (HashError).</summary>
		public string ImageSettingsHashValue { get; }
		public string GatedCanonicalText { get; }
		public string HashError { get; }="";
		public string ComputerName { get; }
		public string UnassignedText { get; private set; }
		public TestCaptureState State { get; private set; }=TestCaptureState.Closed;
		///<summary>The indicator: the session's own status text, or Test capture's.</summary>
		public string StatusText { get; private set; }
		///<summary>How the last open or enable ended, or "".</summary>
		public string MessageText { get; private set; }="";
		public TwainCaptureSourceInfo SourceInfo { get; private set; }
		public TwainCaptureEnded LastEnded { get; private set; }
		///<summary>The enable that is running, or 0.</summary>
		public int EnableIdRunning { get; private set; }
		public int ImageCount { get; private set; }
		///<summary>The measurements of the last image, or null.</summary>
		public TestCaptureAnalysis Analysis { get; private set; }
		///<summary>The last record written in this window, or null.</summary>
		public CaptureVerificationRecord RecordWritten { get; private set; }
		///<summary>Changes whenever something the window shows changed.</summary>
		public int Version { get; private set; }

		///<summary>The quarantine line read again (CaptureQuarantine.GetUnassignedText) after the unassigned captures window closed, or null.</summary>
		public void SetUnassignedText(string unassignedText) {
			if(unassignedText==UnassignedText) {
				return;
			}
			UnassignedText=unassignedText;
			Version++;
		}

		///<summary>16, 14 or 8: what the next enable asks for. By default the device's Bits (16 by default), the depth the capture session will ask
		///for and a record must have passed at; the operator may pick another one to measure. Always 8 when this computer forces 8 bits
		///(Allow16Bit=0, or the safe values): the library then falls back from 16 to 14 or 8 by what the source lists, and the image says which
		///depth was used.</summary>
		public int BitsToRequest {
			get {
				return _bitsToRequest;
			}
			set {
				_bitsToRequest=Options.Allow16Bit ? TwainBitDepth.Normalize(value) : TwainBitDepth.Bits8;
				Version++;
			}
		}

		///<summary>The depths the window offers: 16, 14 and 8, or 8 only when this computer forces 8 bits.</summary>
		public List<int> ListBitsChoices => Options.Allow16Bit ? new List<int> { TwainBitDepth.Bits16,TwainBitDepth.Bits14,TwainBitDepth.Bits8 }
			: new List<int> { TwainBitDepth.Bits8 };

		///<summary>The operator checked the byte order on the known ramp or the phantom. Reset by every new image.</summary>
		public bool IsByteOrderConfirmed {
			get {
				return _isByteOrderConfirmed;
			}
			set {
				_isByteOrderConfirmed=value;
				Version++;
			}
		}

		///<summary>The operator checked that the phantom's radiopaque markers show white. Reset by every new image.</summary>
		public bool IsPolarityConfirmed {
			get {
				return _isPolarityConfirmed;
			}
			set {
				_isPolarityConfirmed=value;
				Version++;
			}
		}

		///<summary>The X-ray category checked for the 'P' letter; 0 for none.</summary>
		public long CategoryDefNum {
			get {
				return _categoryDefNum;
			}
			set {
				_categoryDefNum=value;
				Version++;
			}
		}

		public bool CanStart => State==TestCaptureState.Closed || State==TestCaptureState.Open;
		///<summary>The unassigned captures window (design 7.12) opens from the notice only while no test image is being taken.</summary>
		public bool CanOpenUnassignedCaptures => UnassignedText!=null && (State==TestCaptureState.Closed || State==TestCaptureState.Open);
		public bool CanStop => State==TestCaptureState.Enabled;
		///<summary>A measured image and the settings hash exist, and the gated settings are verified: a record states that the test ran with
		///them, which is not true while the safe values are in effect. The record needs the Setup permission too, which the window checks.</summary>
		public bool CanWriteRecord => Analysis!=null && Analysis.IsValid && GatedSettingsHashValue!="" && ImageSettingsHashValue!=""
			&& SettingsCheck.IsVerified && State!=TestCaptureState.Closing;
		///<summary>The byte-order confirmation applies to a 16-bit image only.</summary>
		public bool IsByteOrderCheckable => Analysis!=null && Analysis.IsValid && Analysis.IsGray16;
		public bool IsPolarityCheckable => Analysis!=null && Analysis.IsValid;
		///<summary>The safe-disable capability was accepted for the last enable.</summary>
		public bool IsSafeDisableInEffect => LastEnded?.IsSafeDisableInEffect??false;
		#endregion Properties

		#region Methods - session events
		///<summary>The window is about to call Open.</summary>
		public void OnOpening() {
			State=TestCaptureState.Opening;
			StatusText="Opening the sensor software...";
			MessageText="";
			Version++;
		}

		public void OnOpenResult(TwainCaptureOpenResult result) {
			if(result!=null && result.IsOpen) {
				State=TestCaptureState.Open;
				SourceInfo=result.SourceInfo;
				StatusText=TwainCaptureSession.GetStateText(TwainCaptureState.Open);
				MessageText="";
			}
			else {
				State=TestCaptureState.Closed;
				StatusText="Sensor software not open.";
				MessageText=CaptureEndText.GetOpenText(result,Device.TwainName,Options.OpenTimeout);
			}
			Version++;
		}

		///<summary>The request for the next enable: BitsToRequest, the configured byte order and window setting, and the test target.</summary>
		public TwainEnableRequest CreateEnableRequest() {
			TwainEnableRequest request=new TwainEnableRequest();
			request.Bits=BitsToRequest;
			request.Tag=this;
			request.TargetLabel=TargetLabel;
			return request;
		}

		///<summary>Enable returned enableId (0: the session was not open).</summary>
		public void OnEnablePosted(int enableId) {
			if(enableId==0) {
				MessageText="The sensor software is not open.";
				State=TestCaptureState.Closed;
			}
			else {
				EnableIdRunning=enableId;
				State=TestCaptureState.Enabled;
				MessageText="";
				StatusText="Enabling the sensor software...";
			}
			Version++;
		}

		public void OnStatus(TwainCaptureStatus status) {
			if(status==null) {
				return;
			}
			if(!string.IsNullOrWhiteSpace(status.Text)) {
				StatusText=status.Text;
			}
			if(State!=TestCaptureState.Closing) {
				switch(status.State) {
					case TwainCaptureState.Closed:
						State=TestCaptureState.Closed;
						EnableIdRunning=0;
						break;
					case TwainCaptureState.Opening:
						State=TestCaptureState.Opening;
						break;
					case TwainCaptureState.Open:
						State=TestCaptureState.Open;
						break;
					case TwainCaptureState.Closing:
						State=TestCaptureState.Closing;
						break;
					default:
						State=TestCaptureState.Enabled;
						break;
				}
			}
			Version++;
		}

		///<summary>A test image arrived: it is measured, and the operator's confirmations start over for it.</summary>
		public void OnImage(AcquiredImage image) {
			if(image==null || State==TestCaptureState.Closing) {
				return;
			}
			ImageCount++;
			Analysis=TestCaptureAnalysis.Analyze(image,Options.Polarity);
			_isByteOrderConfirmed=false;
			_isPolarityConfirmed=false;
			Version++;
		}

		public void OnEnded(TwainCaptureEnded ended) {
			if(ended==null) {
				return;
			}
			LastEnded=ended;
			MessageText=CaptureEndText.GetEndedText(ended);
			if(ended.Reason==TwainCaptureEndReason.Completed && ended.ImageCount>0) {
				MessageText=(ended.ImageCount==1 ? "Test image received." : ended.ImageCount+" test images received; the last one is shown.")
					+(MessageText=="" ? "" : " "+MessageText);
			}
			if(ended.EnableId==EnableIdRunning) {
				EnableIdRunning=0;
				if(State==TestCaptureState.Enabled) {
					State=TestCaptureState.Open;
				}
			}
			Version++;
		}

		///<summary>The window is closing: no more images are measured.</summary>
		public void OnClosing() {
			State=TestCaptureState.Closing;
			Version++;
		}
		#endregion Methods - session events

		#region Methods - verification record
		///<summary>The record for the last image, or null with error.</summary>
		public CaptureVerificationRecord BuildRecord(DateTime now,out string error) {
			error="";
			if(GatedSettingsHashValue=="") {
				error=HashError;
				return null;
			}
			if(Analysis==null || !Analysis.IsValid) {
				error="Take a test image first.";
				return null;
			}
			if(!SettingsCheck.IsVerified) {
				error="No verification record can be written while safe values are in effect. "+SettingsCheck.Text;
				return null;
			}
			if(ImageSettingsHashValue=="") {
				error=HashError;
				return null;
			}
			if(SourceInfo?.DriverIdentity==null) {
				error="The TWAIN source did not report its driver version, so no record can say which driver it covers.";
				return null;
			}
			CaptureVerificationRecord record=new CaptureVerificationRecord();
			record.ImagingDeviceNum=Device.ImagingDeviceNum;
			record.DeviceDescription=Device.Description??"";
			record.SourceName=(Device.TwainName??"").Trim();//The row value the session compares (Matches).
			record.SourceProductName=SourceInfo.ProductName??"";
			record.DriverIdentity=SourceInfo.DriverIdentity;//compared: the build the source reports at open
			record.ImageSettingsHash=ImageSettingsHashValue;//compared: Bits, ByteOrder16, Polarity
			record.ComputerName=ComputerName;//the audit trail only: the record covers every computer that matches
			record.GatedSettingsHash=GatedSettingsHashValue;//the audit trail only
			record.DateTimeVerified=now;
			record.BitsRequested=Analysis.BitsRequested;
			record.BitDepthInEffect=Analysis.BitDepthInEffect;
			record.BitsDelivered=Analysis.BitsDelivered;
			record.TransferMechanism=Analysis.TransferMechanism.ToString();
			record.Width=Analysis.Width;
			record.Height=Analysis.Height;
			record.XDpi=Analysis.XDpi;
			record.YDpi=Analysis.YDpi;
			record.ByteOrder16=Options.ByteOrder16.ToString();
			record.Polarity=Options.Polarity.ToString();
			record.PixelFlavor=Analysis.PixelFlavor==AcquiredImage.PixelFlavorVanilla ? "Vanilla" : "Chocolate";
			record.IsPolarityConfirmed=IsPolarityConfirmed;
			record.MinValue=Analysis.MinValue;
			record.MaxValue=Analysis.MaxValue;
			record.EffectiveBitsMeasured=Analysis.EffectiveBitsMeasured;
			record.EffectiveBitsReported=Analysis.Metadata?.EffectiveBits;
			if(Analysis.IsGray16) {
				record.ByteOrderCheck=Analysis.ByteOrderVerdict.ToString();
				record.IsByteOrderConfirmed=IsByteOrderConfirmed;
				record.BitsStoredRule=Analysis.BitsStoredRule;
				record.WindowLow=Analysis.WindowLow;
				record.WindowHigh=Analysis.WindowHigh;
			}
			record.IsMetadataPresent=Analysis.Metadata!=null;
			record.Serial=Analysis.Metadata?.Serial??"";
			record.Model=Analysis.Metadata?.Model??"";
			record.DsVersion=Analysis.Metadata?.DsVersion??"";
			return record;
		}

		///<summary>Why record does not verify capture of more than 8 bits, or not at the device's Bits, for a confirmation before it is written, or
		///"" when it verifies the depth the session will ask for.</summary>
		public string GetRecordWarning(CaptureVerificationRecord record) {
			string warning=GetRecordWarningPassed(record);
			if(warning!="" || record==null) {
				return warning;
			}
			int bitsSession=Options.Allow16Bit ? TwainBitDepth.Normalize(Options.Bits) : TwainBitDepth.Bits8;
			if(bitsSession>8 && record.BitDepthVerified!=bitsSession) {
				return "This record verifies "+record.BitDepthVerified+"-bit capture, but this device's Bits is "+bitsSession+", so the capture session "
					+"will not ask for more than 8 bits with it. Set Bits="+record.BitDepthVerified+" in Capture settings, or take the test image at "
					+bitsSession+" bits.";
			}
			return "";
		}

		///<summary>Why record does not verify capture of more than 8 bits at its own depth, or "" when it does.</summary>
		public static string GetRecordWarningPassed(CaptureVerificationRecord record) {
			if(record==null || record.IsPassed) {
				return "";
			}
			List<string> listReasons=new List<string>();
			if(record.BitDepthVerified<=8) {
				if(record.BitsDelivered<=8) {
					listReasons.Add("the image is 8-bit");
				}
				else {
					listReasons.Add("the image of more than 8 bits did not come from an enable of more than 8 bits over memory transfer");
				}
			}
			if(record.ByteOrderCheck==ByteOrderVerdict.LooksSwapped.ToString()) {
				listReasons.Add("the byte order looks swapped");
			}
			if(record.BitsDelivered>8 && !record.IsByteOrderConfirmed) {
				listReasons.Add("the byte order is not confirmed");
			}
			if(!record.IsPolarityConfirmed) {
				listReasons.Add("the polarity is not confirmed");
			}
			if(record.IsVanillaNotInverted) {
				listReasons.Add("the source delivers TWPF_VANILLA samples, which are stored as DICOM only with Polarity=Invert");
			}
			return "This record does not verify capture of more than 8 bits: "+string.Join(", ",listReasons)+".";
		}

		///<summary>What record covers, for the result box: the depth, the driver version and the image settings, on every computer that runs them.</summary>
		public static string GetRecordScopeText(CaptureVerificationRecord record) {
			if(record==null) {
				return "";
			}
			return "It covers "+record.BitDepthVerified+"-bit capture"+(record.BitDepthVerified==TwainBitDepth.Bits14 ? " (stored in a 16-bit file)" : "")
				+" for driver "+(record.DriverIdentity==null ? "(not reported)" : record.DriverIdentity.Describe())
				+" with these image settings (Bits, ByteOrder16, Polarity), on every computer that runs them.";
		}

		public void OnRecordWritten(CaptureVerificationRecord record) {
			RecordWritten=record;
			Version++;
		}
		#endregion Methods - verification record

		#region Methods - texts
		public string GetDeviceText() {
			string text="Device: "+Show(Device.Description)+" (ImagingDeviceNum "+Device.ImagingDeviceNum.ToString(CultureInfo.InvariantCulture)
				+"), TWAIN source \""+(Device.TwainName??"")+"\". This computer: "+ComputerName+".";
			if(!string.IsNullOrWhiteSpace(Device.ComputerName)
				&& !string.Equals(Device.ComputerName.Trim(),ComputerName,StringComparison.OrdinalIgnoreCase))
			{
				text+=" The device is set up for computer "+Device.ComputerName.Trim()+"; capture.ini is this computer's.";
			}
			return text+" A verification record covers every computer that runs the same driver version with the same image settings (Bits, "
				+"ByteOrder16, Polarity).";
		}

		///<summary>The effective device settings, the gated values with their hash, and what the source reported at open.</summary>
		public string GetSettingsText() {
			TwainCaptureOptions options=Options;
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine("capture.ini: "+Show(options.ConfigPath));
			if(!SettingsCheck.IsVerified) {
				stringBuilder.AppendLine("Safe values in effect (design 9.3), not the file's gated values: "+SettingsCheck.Status+".");
			}
			stringBuilder.AppendLine("Settings in effect for this device:");
			stringBuilder.AppendLine("  SessionMode="+options.SessionMode+", Bits="+options.Bits+", ByteOrder16="+options.ByteOrder16
				+", Polarity="+options.Polarity+", Headless="+ToBit(options.Headless));
			stringBuilder.AppendLine("  DisableIsSafe="+ToBit(options.DisableIsSafe)+", SessionTimeoutMinutes="
				+((int)options.SessionTimeout.TotalMinutes).ToString(CultureInfo.InvariantCulture));
			stringBuilder.AppendLine("  SafeDisableCap="+ToHex(options.SafeDisableCap)+", RefusedDisableCodes="
				+(options.RefusedDisableCodes.Count==0 ? "(none)" : string.Join("|",options.RefusedDisableCodes.Select(x => CaptureEndText.ToHex(x)))));
			stringBuilder.AppendLine("  ContinuousCap="+ToHex(options.ContinuousCap)+", RedeliverCap="+ToHex(options.RedeliverCap)
				+", NoActivateCap="+ToHex(options.NoActivateCap));
			stringBuilder.AppendLine("  Allow16Bit="+ToBit(options.Allow16Bit)+", RestrictSources="+ToBit(options.RestrictSources)
				+", AllowedSources="+(options.AllowedSources.Count==0 ? "(none)" : string.Join("|",options.AllowedSources))
				+", JournalKeep="+options.JournalKeep.ToString(CultureInfo.InvariantCulture));
			stringBuilder.AppendLine("  More than 8 bits configured: "+(options.IsAbove8BitConfigured ? "yes, "+options.Bits+" bits"
				: "no (needs Bits=16 or 14, the default 16, and Allow16Bit=1, the default)")
				+". The session also needs a Test capture record for this sensor's driver version and image settings that passed at that depth.");
			foreach(string warning in options.ListWarnings) {
				stringBuilder.AppendLine("  Warning: "+warning);
			}
			stringBuilder.AppendLine();
			if(ImageSettingsHashValue!="") {
				stringBuilder.AppendLine("Image settings a record covers: Bits="+options.Bits+", ByteOrder16="+options.ByteOrder16+", Polarity="+options.Polarity
					+", SHA-256 "+ImageSettingsHashValue+".");
			}
			if(GatedSettingsHashValue=="") {
				stringBuilder.AppendLine(HashError);
			}
			else {
				stringBuilder.AppendLine("Gated settings, SHA-256 "+GatedSettingsHashValue+":");
				foreach(string line in GatedCanonicalText.Split(new[] { '\n' },StringSplitOptions.RemoveEmptyEntries).Skip(1)) {
					stringBuilder.AppendLine("  "+line);
				}
			}
			if(SourceInfo!=null) {
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("Source: "+Show(SourceInfo.ProductName)+", "+Show(SourceInfo.Manufacturer)+", version "+SourceInfo.VersionMajor+"."
					+SourceInfo.VersionMinor+" ("+Show(SourceInfo.VersionInfo)+"), protocol "+SourceInfo.ProtocolMajor+"."+SourceInfo.ProtocolMinor+", DSM "
					+Show(SourceInfo.DsmName)+". A record names this driver version.");
				stringBuilder.AppendLine("  Device online: "+ShowBool(SourceInfo.DeviceOnline)+", UI controllable: "+ShowBool(SourceInfo.UiControllable)
					+", pixel type "+ShowInt(SourceInfo.PixelType)+", pixel flavor "+ShowInt(SourceInfo.PixelFlavor)+".");
				stringBuilder.AppendLine("  Bit depths at open: "+(SourceInfo.ListBitDepths==null || SourceInfo.ListBitDepths.Count==0 ? "-"
					: string.Join(", ",SourceInfo.ListBitDepths))+" (a source may list 16 only with memory transfer).");
				stringBuilder.AppendLine("  Extended image info settable: "+(SourceInfo.IsExtImageInfoSettable ? "yes" : "no")
					+", capture metadata listed: "+(SourceInfo.IsMetadataListed ? "yes" : "no")+".");
				foreach(TwainCustomCapabilityInfo capabilityInfo in SourceInfo.ListCustomCapabilities) {
					stringBuilder.AppendLine("  "+capabilityInfo.Key+" "+CaptureEndText.ToHex(capabilityInfo.Number)+": "
						+(capabilityInfo.QuerySupport==null
							? "not supported"+(capabilityInfo.ConditionCode==null ? "" : " ("+CaptureEndText.ToHex(capabilityInfo.ConditionCode.Value)+")")
							: (capabilityInfo.IsSettable ? "settable" : "not settable")));
				}
			}
			return stringBuilder.ToString().TrimEnd();
		}

		///<summary>The results of the last image, and the last record written.</summary>
		public string GetResultText() {
			List<string> listLines=new List<string>();
			if(Analysis==null) {
				listLines.Add(Instructions);
			}
			else {
				listLines.Add("Test image "+ImageCount.ToString(CultureInfo.InvariantCulture)+":");
				listLines.AddRange(Analysis.GetResultLines());
			}
			if(RecordWritten!=null) {
				listLines.Add("");
				string warning=GetRecordWarning(RecordWritten);
				listLines.Add("Verification record written "+RecordWritten.DateTimeVerified.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)+": "
					+(RecordWritten.IsPassed ? "verifies "+RecordWritten.BitDepthVerified+"-bit capture. "+GetRecordScopeText(RecordWritten) : "")
					+(warning=="" ? "" : (RecordWritten.IsPassed ? " " : "")+warning));
			}
			return string.Join("\r\n",listLines);
		}

		///<summary>The 'P' warning for the chosen X-ray category, and for every mount template whose default category carries 'P' (design 9.1,
		///10.6: an X-ray saved in such a category can become the patient's picture).</summary>
		public string GetCategoryWarning() {
			List<string> listWarnings=new List<string>();
			string warning=_imageCategoryRule.GetWarning(CategoryDefNum,"X-ray","X-rays");
			if(warning!="") {
				listWarnings.Add(warning);
			}
			foreach(MountTemplateCategory mountTemplate in _listMountTemplates) {
				if(mountTemplate.DefaultCat==0 || mountTemplate.DefaultCat==CategoryDefNum || !_imageCategoryRule.IsPatientPictureCategory(mountTemplate.DefaultCat)) {
					continue;
				}
				ImageCategoryItem item=_imageCategoryRule.Find(mountTemplate.DefaultCat);
				listWarnings.Add("Mount template \""+mountTemplate.Description+"\" saves its X-rays into \""+item?.Name+"\" (usage P).");
			}
			return string.Join(" ",listWarnings);
		}

		///<summary>The categories the picker offers (not hidden, plus the current one).</summary>
		public List<ImageCategoryItem> GetCategoryPickList() {
			return _imageCategoryRule.GetPickList(CategoryDefNum);
		}

		///<summary>The X-ray category to check first: the default category most mount templates use (the first of equals), or 0.</summary>
		public static long GetDefaultCategory(IEnumerable<MountTemplateCategory> listMountTemplates) {
			List<MountTemplateCategory> listWithCategory=(listMountTemplates??Enumerable.Empty<MountTemplateCategory>())
				.Where(x => x!=null && x.DefaultCat!=0).ToList();
			if(listWithCategory.Count==0) {
				return 0;
			}
			return listWithCategory.GroupBy(x => x.DefaultCat)
				.Select(x => new { DefNum=x.Key,Count=x.Count(),First=listWithCategory.FindIndex(y => y.DefaultCat==x.Key) })
				.OrderByDescending(x => x.Count).ThenBy(x => x.First)
				.First().DefNum;
		}

		///<summary>The text to confirm before a Cancel, or null when the cancel needs none (safe disable in effect).</summary>
		public string GetCancelWarning() {
			return IsSafeDisableInEffect ? null : CaptureEndText.CancelWarning;
		}
		#endregion Methods - texts

		private static string ToBit(bool value) {
			return value ? "1" : "0";
		}

		private static string ToHex(ushort? value) {
			return value==null ? "(none)" : CaptureEndText.ToHex(value.Value);
		}

		private static string Show(string text) {
			return string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
		}

		private static string ShowBool(bool? value) {
			return value==null ? "-" : (value.Value ? "yes" : "no");
		}

		private static string ShowInt(int? value) {
			return value==null ? "-" : value.Value.ToString(CultureInfo.InvariantCulture);
		}
	}
}
