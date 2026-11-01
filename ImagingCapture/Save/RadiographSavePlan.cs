// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ImagingCapture.Dicom;
using ImagingCapture.Metadata;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Save {
	///<summary>How a captured radiograph is stored.</summary>
	public enum RadiographFormat {
		///<summary>Not stored (RadiographSavePlan.Error says why); the session quarantines the image.</summary>
		None,
		///<summary>Lossless 8-bit greyscale PNG (or 24-bit color PNG for an RGB image), .png (design 7.8 "Until then").</summary>
		Png8,
		///<summary>16-bit DICOM Digital Intra-Oral X-Ray Image, For Presentation, .dcm (design 7.8).</summary>
		Dicom16,
	}

	///<summary>Where a captured image goes.</summary>
	public sealed class RadiographTarget {
		///<summary>The mount, or null for a single document with no mount (design 7.3 "With no mount").</summary>
		public Mount Mount { get; set; }
		///<summary>The slot (MountPlacement) when Mount is set. Null with a Mount: no slot is free, so the image goes to a new unmounted item
		///(ItemOrder -1), as in ControlImages.PlaceAcquiredBitmapInUI.</summary>
		public MountItem MountItem { get; set; }
		///<summary>A single document only: the tooth numbers from the banner's tooth picker (Universal, separated by commas). Ignored with a
		///Mount, where they come from the slot.</summary>
		public string ToothNumbers { get; set; }
		///<summary>With a Mount and no MountItem only: the slot the image was taken for, when it goes to a new unmounted item instead because
		///that slot already holds an image (a recovered capture, design 7.12 "Recovered: &lt;slot&gt;"). The document keeps this slot's tooth
		///numbers and rotation, as a retake keeps them (design 7.11). Null: the unmounted item's (none).</summary>
		public MountItem SlotRecovered { get; set; }

		///<summary>The image goes into a mount slot (not a text or unmounted item).</summary>
		public bool HasMountSlot => Mount!=null && MountPlacement.IsSlot(MountItem);

		///<summary>The target in words, for logs and sidecars.</summary>
		public string GetLabel() {
			if(Mount==null) {
				string toothNumbers=(ToothNumbers??"").Trim();
				return toothNumbers=="" ? "Single image" : "Single image (teeth "+toothNumbers+")";
			}
			if(MountItem==null) {
				return "Unmounted";
			}
			return MountPlacement.GetSlotLabel(MountItem);
		}

		///<summary>The DICOM writer's view of the target (orientation and anatomy from the slot, design 7.10).</summary>
		public IntraOralTarget ToIntraOralTarget() {
			IntraOralTarget intraOralTarget=new IntraOralTarget();
			intraOralTarget.HasMountSlot=HasMountSlot;
			if(HasMountSlot) {
				intraOralTarget.ToothNumbers=MountItem.ToothNumbers??"";
				intraOralTarget.DegreesRotated=MountItem.RotateOnAcquire;
				intraOralTarget.IsFlipped=Mount.FlipOnAcquire;
			}
			else {
				intraOralTarget.ToothNumbers=Mount==null ? ToothNumbers??"" : "";
			}
			return intraOralTarget;
		}
	}

	///<summary>Everything RadiographImport needs to store one captured image for a patient. The session (CAP-4) fills it on the UI thread.</summary>
	public sealed class RadiographSaveRequest {
		///<summary>The image as TwainCaptureSession raised it.</summary>
		public AcquiredImage Image { get; set; }
		///<summary>The parsed capture metadata. Null: parsed here from Image.MetadataJson.</summary>
		public CaptureMetadata Metadata { get; set; }
		///<summary>The session's bound patient. Never ControlImages' current patient (design 10.6).</summary>
		public Patient Patient { get; set; }
		///<summary>The image category (Def of DefCat.ImageCats).</summary>
		public long DocCategory { get; set; }
		///<summary>The provider from the banner (RadiographProvider.GetDefaultProvNum, editable), for Document.ProvNum and Mount.ProvNum.</summary>
		public long ProvNum { get; set; }
		public RadiographTarget Target { get; set; }
		///<summary>The caller has checked every condition of design 7.8 "When 16-bit is enabled" for this device: [Device.N] Bits=16,
		///[Twain] Allow16Bit=1, a verification record that matches the current settings, and the release conditions. Only then is a 16-bit
		///image written as DICOM; without it a 16-bit image is not stored (it goes to unassigned captures).</summary>
		public bool Is16BitAllowed { get; set; }
		///<summary>[Device.N] Polarity. Invert applies only to 16-bit samples, and only with IsInversionVerified.</summary>
		public TwainPolarity Polarity { get; set; }
		///<summary>A verification record that matches the current settings confirms the inversion (design 9.3: the session refuses an inverted
		///capture without one).</summary>
		public bool IsInversionVerified { get; set; }
		///<summary>Null: one Study per mount, a UID derived from the mount (or a new one for a single image).</summary>
		public string StudyInstanceUid { get; set; }
		///<summary>Null: one Series per session, from SessionGuid (or a new one).</summary>
		public string SeriesInstanceUid { get; set; }
		public int? SeriesNumber { get; set; }
		///<summary>Document.Description, or null to leave it empty (a slot, design 9.1). The quarantine window sets "Recovered: &lt;slot&gt;" for an
		///unmounted item that stands in for a taken slot, and the reassigned label (design 7.12).</summary>
		public string Description { get; set; }
		///<summary>The capture session's GUID, for the Series UID and the security log.</summary>
		public Guid SessionGuid { get; set; }
		///<summary>ImagingDevice.Description, for the security log.</summary>
		public string DeviceDescription { get; set; }
		public long ImagingDeviceNum { get; set; }
	}

	///<summary>What RadiographSavePlanner decided for one image: the file and the document fields, with no database call made. RadiographImport
	///then stores it.</summary>
	public sealed class RadiographSavePlan {
		///<summary>False: the image is not stored; Error says why, and the caller quarantines it (design 7.12 "Save fails").</summary>
		public bool IsSavable { get; set; }
		public string Error { get; set; }="";
		public RadiographFormat Format { get; set; }
		public byte[] FileBytes { get; set; }
		///<summary>For ImageStore.Import: "image/png" or "application/dicom".</summary>
		public string MimeType { get; set; }
		///<summary>".png" or ".dcm".</summary>
		public string FileExtension { get; set; }
		///<summary>Radiograph for a grey or black-and-white image, Photo for an RGB one (design 9.1).</summary>
		public ImageType ImgType { get; set; }
		///<summary>The exposure time from the metadata's acquiredAt, else the receive time, local (design 10.8).</summary>
		public DateTime DateCreated { get; set; }
		///<summary>True for radiographs (design 10.8).</summary>
		public bool PrintHeading { get; set; }
		///<summary>0/0 for 8-bit; the percentile window on the 0-255 scale for DICOM (design 9.1).</summary>
		public int WindowingMin { get; set; }
		public int WindowingMax { get; set; }
		///<summary>The 16-bit samples were inverted (Polarity=Invert, verified).</summary>
		public bool IsInverted { get; set; }
		///<summary>The DICOM writer's result, for Dicom16.</summary>
		public DicomWriteResult DicomResult { get; set; }
		///<summary>The image's capture metadata, or null.</summary>
		public CaptureMetadata Metadata { get; set; }
		///<summary>Png8 only: the text of the extra ImageCreate security-log entry (FKey=DocNum) that records the device, model, serial and
		///captureId of an 8-bit capture (design 7.9), and what DICOM would keep in Image Comments. Null for DICOM, whose header holds them.</summary>
		public string CaptureLogText { get; set; }
		///<summary>Things the operator should know that do not stop the save, for example a non-neutral orientation adjustment (design 7.9).</summary>
		public List<string> ListWarnings { get; set; }=new List<string>();
	}

	///<summary>The decisions of the capture saver (design 7.8, 7.9, 7.10, 9.1, 10.8), as pure functions with no database: which format, the
	///file bytes, and the document fields. RadiographImport applies the plan through OpenDentBusiness.</summary>
	public static class RadiographSavePlanner {
		public const string MimePng="image/png";
		public const string MimeDicom="application/dicom";
		public const string ExtensionPng=".png";
		public const string ExtensionDicom=".dcm";
		///<summary>The start of the extra ImageCreate entry of an 8-bit capture.</summary>
		public const string CaptureLogPrefix="Capture: ";
		///<summary>The warning for an image the sensor software rotated or flipped (metadata adjustments, design 7.9).</summary>
		public const string OrientationAdjustmentWarning="The sensor software rotated or flipped this image (its adjustments are not neutral), "
			+"and the slot's own rotation is applied on top. Set the sensor software's adjustments back to neutral (see the runbook).";

		///<summary>The plan for request. Never throws: a request that cannot be stored gives IsSavable false with Error. now is the clock, for
		///an image with no receive time.</summary>
		public static RadiographSavePlan Plan(RadiographSaveRequest request,DateTime now) {
			RadiographSavePlan plan=new RadiographSavePlan();
			try {
				PlanChecked(request,now,plan);
			}
			catch(Exception ex) {
				plan.IsSavable=false;
				plan.Format=RadiographFormat.None;
				plan.Error="The image could not be prepared for saving: "+ex.Message;
			}
			return plan;
		}

		private static void PlanChecked(RadiographSaveRequest request,DateTime now,RadiographSavePlan plan) {
			if(request==null || request.Image==null) {
				plan.Error="No image.";
				return;
			}
			if(request.Target==null) {
				plan.Error="The image has no target.";
				return;
			}
			AcquiredImage image=request.Image;
			CaptureMetadata metadata=request.Metadata??CaptureMetadataParser.Parse(image.MetadataJson);
			plan.Metadata=metadata;
			plan.DateCreated=GetDateCreated(metadata,image.ReceivedAt,now);
			if(metadata!=null && metadata.HasOrientationAdjustment) {
				plan.ListWarnings.Add(OrientationAdjustmentWarning);
			}
			if(image.IsGray16) {
				PlanDicom(request,metadata,plan);
				return;
			}
			PlanPng(request,metadata,plan);
		}

		///<summary>Design 10.8: the exposure time from the metadata's acquiredAt (converted to local time), otherwise the receive time, otherwise
		///now.</summary>
		public static DateTime GetDateCreated(CaptureMetadata metadata,DateTime receivedAt,DateTime now) {
			if(metadata?.AcquiredAt!=null) {
				return metadata.AcquiredAt.Value.LocalDateTime;
			}
			if(receivedAt.Year>1880) {
				return receivedAt;
			}
			return now;
		}

		///<summary>Design 9.1: Radiograph for GRAY or BW (TW_IMAGEINFO PixelType), Photo for RGB. A palette image, or one the source did not
		///describe, is a Radiograph when its pixels are grey.</summary>
		public static ImageType GetImgType(int pixelType,bool isGray) {
			switch(pixelType) {
				case AcquiredImage.PixelTypeBw:
				case AcquiredImage.PixelTypeGray:
					return ImageType.Radiograph;
				case AcquiredImage.PixelTypeRgb:
					return ImageType.Photo;
				default:
					return isGray ? ImageType.Radiograph : ImageType.Photo;
			}
		}

		private static void PlanPng(RadiographSaveRequest request,CaptureMetadata metadata,RadiographSavePlan plan) {
			AcquiredImage image=request.Image;
			if(image.Dib==null) {
				plan.Error="The image has no pixels.";
				return;
			}
			string error;
			DecodedImage decodedImage=DibDecoder.Decode(image.Dib,out error);
			if(decodedImage==null) {
				plan.Error=error;
				return;
			}
			plan.FileBytes=PngEncoder.Encode(decodedImage,image.XDpi,image.YDpi);
			plan.Format=RadiographFormat.Png8;
			plan.MimeType=MimePng;
			plan.FileExtension=ExtensionPng;
			plan.ImgType=GetImgType(image.PixelType,decodedImage.IsGray);
			plan.PrintHeading=plan.ImgType==ImageType.Radiograph;
			plan.WindowingMin=0;
			plan.WindowingMax=0;
			plan.CaptureLogText=BuildCaptureLogText(request,metadata);
			plan.IsSavable=true;
		}

		private static void PlanDicom(RadiographSaveRequest request,CaptureMetadata metadata,RadiographSavePlan plan) {
			AcquiredImage image=request.Image;
			if(!request.Is16BitAllowed) {
				plan.Error="A 16-bit image arrived, but 16-bit saving is not enabled for this device, so it was not saved to the patient.";
				return;
			}
			if(image.TransferMechanism!=TwainTransferMechanism.Memory || !image.IsAbove8BitInEffect) {
				//A 16bpp native DIB is RGB555/565 by definition (TWAIN 2.4 p.12-7) and is never asked for; 16-bit samples in an 8-bit enable did not
				//come through the negotiation that Test capture verified (byte order, polarity). Neither is written as DICOM (design 7.1 CAP-3).
				plan.Error="A 16-bit image arrived that did not come through the verified 16-bit memory transfer (ICAP_BITDEPTH 16 over "
					+"TWSX_MEMORY), so it was not saved to the patient.";
				return;
			}
			if(image.Width<=0 || image.Height<=0 || image.Gray16.LongLength!=(long)image.Width*image.Height) {
				plan.Error="The 16-bit image's size does not match its samples.";
				return;
			}
			ushort[] arrayPixels=image.Gray16;
			if(request.Polarity==TwainPolarity.Invert) {
				if(!request.IsInversionVerified) {
					plan.Error="Polarity=Invert is set for this device, but no verification record confirms it, so the image was not saved to the patient.";
					return;
				}
				arrayPixels=Gray16Samples.Invert(arrayPixels,metadata?.EffectiveBits);
				plan.IsInverted=true;
			}
			RadiographTarget target=request.Target;
			DicomIntraOralImage dicomImage=new DicomIntraOralImage();
			dicomImage.Width=image.Width;
			dicomImage.Height=image.Height;
			dicomImage.Pixels=arrayPixels;
			dicomImage.XDpi=image.XDpi;
			dicomImage.YDpi=image.YDpi;
			dicomImage.PixelFlavor=image.PixelFlavor;//as delivered: the writer writes verified inverted samples whatever it says
			dicomImage.IsInverted=plan.IsInverted;
			dicomImage.Target=target.ToIntraOralTarget();
			dicomImage.Metadata=metadata;
			dicomImage.StudyInstanceUid=request.StudyInstanceUid??GetStudyInstanceUid(target.Mount);
			dicomImage.StudyDateTime=target.Mount?.DateCreated??DateTime.MinValue;
			dicomImage.SeriesInstanceUid=request.SeriesInstanceUid??(request.SessionGuid==Guid.Empty ? null : DicomUid.FromGuid(request.SessionGuid));
			dicomImage.SeriesNumber=request.SeriesNumber;
			if(target.HasMountSlot) {
				dicomImage.InstanceNumber=target.MountItem.ItemOrder;
			}
			dicomImage.ReceivedAt=image.ReceivedAt.Year>1880 ? image.ReceivedAt : (DateTime?)null;
			DicomWriteResult dicomWriteResult=DicomIntraOralWriter.Write(dicomImage);
			plan.DicomResult=dicomWriteResult;
			if(!dicomWriteResult.IsWritten) {
				plan.Error=dicomWriteResult.Error;
				return;
			}
			plan.FileBytes=dicomWriteResult.Bytes;
			plan.Format=RadiographFormat.Dicom16;
			plan.MimeType=MimeDicom;
			plan.FileExtension=ExtensionDicom;
			plan.ImgType=ImageType.Radiograph;
			plan.PrintHeading=true;
			int windowingMin;
			int windowingMax;
			Gray16Samples.GetStoredWindow(dicomWriteResult.WindowLow,dicomWriteResult.WindowHigh,dicomWriteResult.BitsStored,out windowingMin,
				out windowingMax);
			plan.WindowingMin=windowingMin;
			plan.WindowingMax=windowingMax;
			plan.IsSavable=true;
		}

		///<summary>One Study per mount (design 7.8): a name-based UID from the mount's identity, so every image of a mount gets the same one,
		///including images added later. Null (a new UID per image) for a single image.</summary>
		public static string GetStudyInstanceUid(Mount mount) {
			if(mount==null || mount.MountNum==0) {
				return null;
			}
			return DicomUid.FromName("openpms-dental/mount/"+mount.MountNum.ToString(CultureInfo.InvariantCulture)+"/"
				+mount.PatNum.ToString(CultureInfo.InvariantCulture)+"/"+mount.DateCreated.ToString("yyyyMMddHHmmss",CultureInfo.InvariantCulture));
		}

		///<summary>The extra ImageCreate entry of an 8-bit capture (design 7.9: serial, model and captureId go into it), with what DICOM would
		///keep in Image Comments: the rendering and the configured exposure, labelled "configured, not measured".</summary>
		public static string BuildCaptureLogText(RadiographSaveRequest request,CaptureMetadata metadata) {
			AcquiredImage image=request.Image;
			List<string> listParts=new List<string>();
			listParts.Add("device "+Quote(request.DeviceDescription));
			listParts.Add("source "+Quote(image.SourceName));
			if(metadata==null) {
				listParts.Add("no capture metadata");
			}
			else {
				listParts.Add("manufacturer "+Quote(metadata.Manufacturer));
				listParts.Add("model "+Quote(metadata.Model));
				listParts.Add("serial "+Quote(metadata.Serial));
				listParts.Add("captureId "+Quote(metadata.CaptureId));
				if(metadata.Sequence!=null) {
					listParts.Add("sequence "+metadata.Sequence.Value.ToString(CultureInfo.InvariantCulture));
				}
				if(!string.IsNullOrEmpty(metadata.AcquiredAtText)) {
					listParts.Add("acquiredAt "+metadata.AcquiredAtText);
				}
				listParts.Add("rendering="+(string.IsNullOrEmpty(metadata.Rendering) ? "unknown" : metadata.Rendering));
				if(metadata.Adjustments!=null && !metadata.Adjustments.IsNeutral) {
					listParts.Add("adjustments not neutral");
				}
				if(metadata.Redelivered) {
					listParts.Add("redelivered");
				}
				if(metadata.ConfiguredExposure!=null && metadata.ConfiguredExposure.HasAnyValue) {
					listParts.Add(DicomIntraOralWriter.ConfiguredExposureLabel+metadata.ConfiguredExposure.ToText());
				}
			}
			listParts.Add(image.BitsPerSample.ToString(CultureInfo.InvariantCulture)+"-bit");
			if(image.IsDeliveredAs8BitIn16BitEnable) {
				listParts.Add("delivered as 8-bit in a 16-bit enable");
			}
			listParts.Add("target "+request.Target.GetLabel());
			if(request.SessionGuid!=Guid.Empty) {
				listParts.Add("session "+request.SessionGuid.ToString("D"));
			}
			return CaptureLogPrefix+string.Join(", ",listParts)+".";
		}

		private static string Quote(string text) {
			text=(text??"").Trim();
			if(text=="") {
				return "(not reported)";
			}
			StringBuilder stringBuilder=new StringBuilder();
			foreach(char c in text) {
				stringBuilder.Append(char.IsControl(c) ? ' ' : c);
			}
			return "\""+stringBuilder.ToString().Replace("\"","'")+"\"";
		}
	}
}
