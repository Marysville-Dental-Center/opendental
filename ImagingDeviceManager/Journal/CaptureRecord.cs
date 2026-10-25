// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Journal {
	///<summary>What the sidecar of a journal or quarantine image records about where the image came from.</summary>
	internal sealed class CaptureRecord {
		///<summary>Local time the image arrived.</summary>
		public DateTime ReceivedAt;
		///<summary>The source's ProductName.</summary>
		public string SourceName="";
		public string Computer="";
		public string WindowsUser="";
		///<summary>Identifies the facade call (one Acquire, or one CloseSource) the image came from, in the journal, the quarantine and the
		///capture log alike.</summary>
		public string AcquireId="";
		///<summary>The patient the caller said it files the image under; 0 when it did not say (the Mount/Acquire loop never does).</summary>
		public long PatNum;
		///<summary>The source's capture id for the image, when a later metadata interface supplies one. Null: not known.</summary>
		public string CaptureId;
		///<summary>The image's number within the source's session, when a later metadata interface supplies it. Null: not known.</summary>
		public int? Sequence;
		///<summary>The gated [Twain] values in effect (CaptureSettingsSnapshot).</summary>
		public List<SidecarField> ListSettings=new List<SidecarField>();

		///<summary>A record for an image received now on this computer by this Windows user.</summary>
		public static CaptureRecord Create(DateTime receivedAt,string sourceName,string acquireId) {
			return new CaptureRecord {
				ReceivedAt=receivedAt,
				SourceName=CaptureFiles.Clean(sourceName),
				Computer=GetComputerName(),
				WindowsUser=GetWindowsUser(),
				AcquireId=acquireId??"",
			};
		}

		public static string GetComputerName() {
			try {
				return Environment.MachineName;
			}
			catch {
				return "";
			}
		}

		///<summary>"DOMAIN\user", or the user name alone.</summary>
		public static string GetWindowsUser() {
			try {
				string domain=Environment.UserDomainName;
				return string.IsNullOrEmpty(domain) ? Environment.UserName : domain+"\\"+Environment.UserName;
			}
			catch {
				return "";
			}
		}

		///<summary>The fields every sidecar starts with.</summary>
		public List<SidecarField> GetOriginFields() {
			return new List<SidecarField> {
				new SidecarField("ReceivedAt",CaptureFiles.FormatTimestamp(ReceivedAt)),
				new SidecarField("Source",SourceName),
				new SidecarField("Computer",Computer),
				new SidecarField("WindowsUser",WindowsUser),
				new SidecarField("AcquireId",AcquireId),
			};
		}

		///<summary>CaptureId and Sequence, only when known.</summary>
		public List<SidecarField> GetMetadataFields() {
			List<SidecarField> listFields=new List<SidecarField>();
			if(CaptureId!=null) {
				listFields.Add(new SidecarField("CaptureId",CaptureId));
			}
			if(Sequence!=null) {
				listFields.Add(new SidecarField("Sequence",Sequence.Value.ToString(CultureInfo.InvariantCulture)));
			}
			return listFields;
		}

		///<summary>Size, depth, resolution and SHA-256 of the image file, so a copy can be matched to a stored document or to another copy.</summary>
		public static List<SidecarField> GetImageFields(string pathImage,DibImage dib,string sha256) {
			return new List<SidecarField> {
				new SidecarField("ImageFile",System.IO.Path.GetFileName(pathImage??"")),
				new SidecarField("ImageFormat","BMP: the DIB as received, unchanged"),
				new SidecarField("Width",dib.Width.ToString(CultureInfo.InvariantCulture)),
				new SidecarField("Height",dib.Height.ToString(CultureInfo.InvariantCulture)),
				new SidecarField("BitsPerPixel",dib.BitCount.ToString(CultureInfo.InvariantCulture)),
				new SidecarField("XDpi",dib.XResolutionDpi.ToString(CultureInfo.InvariantCulture)),
				new SidecarField("YDpi",dib.YResolutionDpi.ToString(CultureInfo.InvariantCulture)),
				new SidecarField("Sha256",sha256),
			};
		}
	}

	///<summary>The gated [Twain] values in effect for an acquisition, as a journal sidecar records them (design 7.1, 9.3). They are read
	///from capture.ini without a hash check in phases 0-2, so the sidecar is where a hand edit shows up afterwards.</summary>
	internal static class CaptureSettingsSnapshot {
		public static List<SidecarField> Build(LocalCaptureConfig config,bool isThreadModalInEffect,bool isSafeDisableInEffect) {
			List<SidecarField> listFields=new List<SidecarField>();
			if(config==null) {
				config=LocalCaptureConfig.Load((string)null);
			}
			listFields.Add(new SidecarField("Twain.ThreadModal",ToBit(config.ThreadModal)));
			listFields.Add(new SidecarField("Twain.AcquireTimeoutMinutes",config.AcquireTimeoutMinutes.ToString(CultureInfo.InvariantCulture)));
			listFields.Add(new SidecarField("Twain.Allow16Bit",ToBit(config.Allow16Bit)));
			listFields.Add(new SidecarField("Twain.RestrictSources",ToBit(config.RestrictSources)));
			listFields.Add(new SidecarField("Twain.AllowedSources",string.Join("|",config.AllowedSources)));
			listFields.Add(new SidecarField("Twain.JournalKeep",config.JournalKeep.ToString(CultureInfo.InvariantCulture)));
			listFields.Add(new SidecarField("Twain.DisableIsSafeSources",string.Join("|",config.DisableIsSafeSources)));
			listFields.Add(new SidecarField("Twain.SafeDisableCap",config.SafeDisableCap==null ? "" : TwainSession.ToHex(config.SafeDisableCap.Value)));
			listFields.Add(new SidecarField("Twain.RefusedDisableCodes",string.Join("|",config.RefusedDisableCodes.Select(x => TwainSession.ToHex(x)))));
			listFields.Add(new SidecarField("ThreadModalInEffect",ToBit(isThreadModalInEffect)));
			listFields.Add(new SidecarField("SafeDisableInEffect",ToBit(isSafeDisableInEffect)));
			listFields.Add(new SidecarField("ConfigFile",config.PathLoaded??"(none: defaults)"));
			if(!string.IsNullOrEmpty(config.LastError)) {
				listFields.Add(new SidecarField("ConfigError",config.LastError));
			}
			return listFields;
		}

		private static string ToBit(bool value) {
			return value ? "1" : "0";
		}
	}
}
