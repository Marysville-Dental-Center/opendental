// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImagingCapture.Metadata;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.Dicom {
	///<summary>A 16-bit intra-oral X-ray to write as DICOM: the samples as the source delivered them, where they go, and what the source
	///said about them.</summary>
	public sealed class DicomIntraOralImage {
		public int Width { get; set; }
		public int Height { get; set; }
		///<summary>Width*Height raw samples, rows top to bottom, each right-justified as delivered (AcquiredImage.Gray16, after any verified
		///lossless inversion the saver applied). Written unchanged: never shifted, windowed or rescaled.</summary>
		public ushort[] Pixels { get; set; }
		///<summary>Horizontal resolution from DAT_IMAGEINFO, pixels per inch. 0 when unknown.</summary>
		public double XDpi { get; set; }
		public double YDpi { get; set; }
		///<summary>The pixel flavor the source delivered (AcquiredImage.PixelFlavor): PixelFlavorChocolate (0 is black) or PixelFlavorVanilla.</summary>
		public int PixelFlavor { get; set; }
		///<summary>The saver inverted the samples, as Polarity=Invert allows once it has been verified and logged (design 7.8). Inverted samples
		///are written whatever PixelFlavor says.</summary>
		public bool IsInverted { get; set; }
		///<summary>The source reports values that are logarithmic, not linear, in detector signal (W5): Pixel Intensity Relationship LOG
		///instead of LIN.</summary>
		public bool IsLogarithmic { get; set; }
		///<summary>The slot the image goes into. Required.</summary>
		public IntraOralTarget Target { get; set; }
		///<summary>The image's capture metadata, or null.</summary>
		public CaptureMetadata Metadata { get; set; }
		///<summary>One Study per mount (design 7.8), for example DicomUid.FromName of the mount's identity. Null makes a new one.</summary>
		public string StudyInstanceUid { get; set; }
		///<summary>The mount's date and time, local. DateTime.MinValue leaves Study Date and Time empty (Type 2).</summary>
		public DateTime StudyDateTime { get; set; }
		///<summary>One Series per capture session (design 7.8), for example DicomUid.FromGuid of the session GUID. Null makes a new one.</summary>
		public string SeriesInstanceUid { get; set; }
		///<summary>Series Number, or null for an empty value (Type 2).</summary>
		public int? SeriesNumber { get; set; }
		///<summary>Instance Number, for example the slot's ItemOrder, or null for an empty value (Type 2).</summary>
		public int? InstanceNumber { get; set; }
		///<summary>Null makes a new one, which is the usual case.</summary>
		public string SopInstanceUid { get; set; }
		///<summary>When the image was received (AcquiredImage.ReceivedAt), local: the content time when the metadata has no acquiredAt.</summary>
		public DateTime? ReceivedAt { get; set; }
		///<summary>Instance Creation Date and Time, local. Null means now.</summary>
		public DateTime? CreatedAt { get; set; }
	}

	///<summary>What DicomIntraOralWriter.Write made, or why it made nothing.</summary>
	public sealed class DicomWriteResult {
		public bool IsWritten { get; set; }
		///<summary>Why nothing was written, as a sentence for the log and the session window. "" when IsWritten.</summary>
		public string Error { get; set; }="";
		///<summary>The complete PS3.10 file, for ImageStore.Import as application/dicom with extension .dcm. Null when not written.</summary>
		public byte[] Bytes { get; set; }
		public string SopInstanceUid { get; set; }
		public string StudyInstanceUid { get; set; }
		public string SeriesInstanceUid { get; set; }
		///<summary>12 or 16 (DicomIntraOralWriter.GetBitsStored).</summary>
		public int BitsStored { get; set; }
		///<summary>The 0.5 and 99.5 percentile of the non-zero raw values (DicomIntraOralWriter.GetWindow), as written in Window Center and
		///Width. The saver stores Document.WindowingMin/Max from these on the 0-255 scale (design 7.8, CAP-W).</summary>
		public int WindowLow { get; set; }
		public int WindowHigh { get; set; }
		public string PatientOrientation { get; set; }
		public string ImageLaterality { get; set; }
		public string ImageComments { get; set; }
	}

	///<summary>Writes a 16-bit intra-oral X-ray as a DICOM file: Digital Intra-Oral X-Ray Image Storage - For Presentation
	///(1.2.840.10008.5.1.4.1.1.1.3), Explicit VR Little Endian (1.2.840.10008.1.2.1), uncompressed, as a PS3.10 file (128-byte preamble,
	///"DICM", File Meta Information). Written from NEMA PS3.3, PS3.5, PS3.6 and PS3.10 only; it uses no DICOM library (design 7.8).
	///
	///Modules (PS3.3 A.28.3) and their attributes, as design 7.8 lists them:
	///- Patient: Patient's Name, ID, Birth Date and Sex, all Type 2 empty, so the header never disagrees with the database after a document
	///  moves to another patient.
	///- General Study: Study Instance UID, Study Date and Time, Referring Physician's Name, Study ID and Accession Number (the last three
	///  empty).
	///- General, DX and Intra-oral Series: Modality IO, Series Instance UID, Series Number, Presentation Intent Type FOR PRESENTATION.
	///- General Equipment: Manufacturer (Type 2), and Manufacturer's Model Name and Device Serial Number when the metadata has them.
	///- General Image, DX Image: Image Type ORIGINAL\PRIMARY\ (value 3 present and empty), Instance Number, Patient Orientation, Content Date
	///  and Time, Acquisition DateTime (from the metadata only), Image Comments; MONOCHROME2, Bits Allocated 16, Bits Stored 12 or 16, High
	///  Bit, Pixel Representation 0, Rescale 0/1/US, Pixel Intensity Relationship LIN (or LOG) with Sign -1, Presentation LUT Shape IDENTITY,
	///  Burned In Annotation NO, Lossy Image Compression 00.
	///- DX Detector: Imager Pixel Spacing (25.4/dpi, or the metadata's pixelSpacingMm), Detector Type (Type 2, from the metadata when it is
	///  a DICOM term), Detector ID (the serial when it fits 16 characters).
	///- DX Anatomy Imaged, Intra-oral Image: Image Laterality, Anatomic Region Sequence, Primary Anatomic Structure Sequence with ISO 3950
	///  tooth codes, Positioner Type NONE.
	///- VOI LUT: Window Center and Width (FOR PRESENTATION only).
	///- Acquisition Context: an empty Acquisition Context Sequence (Type 2).
	///- SOP Common: SOP Class and Instance UIDs, Instance Creation Date and Time.
	///Configured exposure goes only into Image Comments, labelled "configured, not measured", never into KVP, tube current, exposure time or
	///dose attributes (design 7.9). Image Comments also records rendering=original.
	///
	///Polarity: Pixel Intensity Relationship Sign is always -1 (higher stored values mean less beam intensity, more radiopaque), which with
	///MONOCHROME2 shows radiopaque structures white. That holds for samples delivered as TWPF_CHOCOLATE (0 is black), and for samples the
	///saver inverted under a verified Polarity=Invert, whatever flavor the source reported: the stored values follow the polarity the clinic
	///verified (design 7.8), not the flavor label, which twain-contract 1.1 keeps at TWPF_CHOCOLATE until its clinic run says whether the
	///values follow it. The writer refuses TWPF_VANILLA samples that were not inverted rather than label them wrongly.
	///
	///The writer refuses (IsWritten false, with Error) a target with no mount slot or no tooth numbers (design 7.10: those are captured at
	///8 bits and never written as DICOM; CanWrite checks this before an enable), and an image it cannot describe truthfully. It never
	///throws for bad input.</summary>
	public static class DicomIntraOralWriter {
		///<summary>File Meta Implementation Version Name (SH).</summary>
		public const string ImplementationVersionName="OPENPMSD_CAP1";
		///<summary>The Image Comments label of configured exposure values.</summary>
		public const string ConfiguredExposureLabel="configured exposure (configured, not measured): ";
		private static readonly string[] _arrayDetectorTypes={ "DIRECT","SCINTILLATOR","STORAGE","FILM" };

		///<summary>Whether an image for this target could be written as DICOM. The session asks for 16 bits only when it can (design 7.8,
		///7.10), and otherwise sets ICAP_BITDEPTH 8 for that enable.</summary>
		public static bool CanWrite(IntraOralTarget target,out string reason) {
			IntraOralAnatomy anatomy=IntraOralAnatomy.FromTarget(target,out reason);
			return anatomy!=null;
		}

		///<summary>The DICOM file for image, or the reason there is none.</summary>
		public static DicomWriteResult Write(DicomIntraOralImage image) {
			DicomWriteResult result=new DicomWriteResult();
			try {
				string error;
				IntraOralAnatomy anatomy;
				string imagerPixelSpacingRow;
				string imagerPixelSpacingColumn;
				if(!Check(image,out anatomy,out imagerPixelSpacingRow,out imagerPixelSpacingColumn,out error)) {
					result.Error=error;
					return result;
				}
				result.StudyInstanceUid=string.IsNullOrEmpty(image.StudyInstanceUid) ? DicomUid.NewUid() : image.StudyInstanceUid;
				result.SeriesInstanceUid=string.IsNullOrEmpty(image.SeriesInstanceUid) ? DicomUid.NewUid() : image.SeriesInstanceUid;
				result.SopInstanceUid=string.IsNullOrEmpty(image.SopInstanceUid) ? DicomUid.NewUid() : image.SopInstanceUid;
				result.BitsStored=GetBitsStored(image.Pixels,image.Metadata?.EffectiveBits);
				int windowLow;
				int windowHigh;
				GetWindow(image.Pixels,out windowLow,out windowHigh);
				result.WindowLow=windowLow;
				result.WindowHigh=windowHigh;
				result.PatientOrientation=anatomy.PatientOrientation;
				result.ImageLaterality=anatomy.ImageLaterality;
				result.ImageComments=GetImageComments(image);
				DicomDatasetBuilder dataset=BuildDataset(image,anatomy,result,imagerPixelSpacingRow,imagerPixelSpacingColumn);
				result.Bytes=BuildFile(dataset,result.SopInstanceUid);
				result.IsWritten=true;
				return result;
			}
			catch(Exception ex) {
				//Not expected: every input is checked first. A writer bug must not lose the image; the saver quarantines it.
				return new DicomWriteResult { Error="The DICOM file could not be written: "+ex.Message };
			}
		}

		///<summary>The BitsStored rule (design 7.8): 12 when every value is 4095 or less and the source reports 12 or fewer effective bits,
		///otherwise 16. Data of 13 to 15 effective bits is stored as 16, because the loader accepts only 8, 12 and 16. Values are never
		///rescaled, so an unknown effective depth (null) gives 16.</summary>
		public static int GetBitsStored(ushort[] pixels,int? effectiveBits) {
			if(effectiveBits==null || effectiveBits.Value>12 || pixels==null) {
				return 16;
			}
			for(int i=0;i<pixels.Length;i++) {
				if(pixels[i]>4095) {
					return 16;
				}
			}
			return 12;
		}

		///<summary>The presentation window of design 7.8: the 0.5 and 99.5 percentile of the raw values, zero-valued pixels excluded (masked
		///corners and dead areas). Percentiles are nearest-rank: of n non-zero values in ascending order, low is the one at rank
		///ceiling(0.005*n) and high the one at rank ceiling(0.995*n), ranks counted from 1. With no non-zero value both are 0.</summary>
		public static void GetWindow(ushort[] pixels,out int low,out int high) {
			low=0;
			high=0;
			if(pixels==null) {
				return;
			}
			int[] arrayHistogram=new int[65536];
			long count=0;
			for(int i=0;i<pixels.Length;i++) {
				if(pixels[i]!=0) {
					arrayHistogram[pixels[i]]++;
					count++;
				}
			}
			if(count==0) {
				return;
			}
			//ceiling(5*count/1000) and ceiling(995*count/1000) in whole numbers, so no rounding error moves a rank.
			long rankLow=Math.Max(1,(5*count+999)/1000);
			long rankHigh=Math.Max(1,(995*count+999)/1000);
			long cumulative=0;
			bool isLowFound=false;
			for(int value=1;value<65536;value++) {
				cumulative+=arrayHistogram[value];
				if(!isLowFound && cumulative>=rankLow) {
					low=value;
					isLowFound=true;
				}
				if(cumulative>=rankHigh) {
					high=value;
					return;
				}
			}
		}

		///<summary>Window Center and Width (PS3.3 C.11.2.1.2.1) that map exactly [low, high] onto the output range: width = high-low+1 and
		///center = (low+high+1)/2, so low is the last value shown black and high the first shown white.</summary>
		public static void GetWindowCenterWidth(int low,int high,out double center,out double width) {
			if(high<low) {
				high=low;
			}
			width=high-low+1;
			center=(low+high+1)/2.0;
		}

		private static bool Check(DicomIntraOralImage image,out IntraOralAnatomy anatomy,out string imagerPixelSpacingRow,
			out string imagerPixelSpacingColumn,out string error)
		{
			anatomy=null;
			imagerPixelSpacingRow=null;
			imagerPixelSpacingColumn=null;
			error="";
			if(image==null) {
				error="No image.";
				return false;
			}
			anatomy=IntraOralAnatomy.FromTarget(image.Target,out error);
			if(anatomy==null) {
				return false;
			}
			if(image.Pixels==null) {
				error="The image has no 16-bit samples; only a 16-bit image is written as DICOM.";
				return false;
			}
			if(image.Width<1 || image.Height<1 || image.Width>ushort.MaxValue || image.Height>ushort.MaxValue) {
				error="The image size "+image.Width+" x "+image.Height+" cannot be written as DICOM (1 to 65535 pixels each way).";
				return false;
			}
			if((long)image.Width*image.Height!=image.Pixels.Length) {
				error="The image has "+image.Pixels.Length+" samples, not "+image.Width+" x "+image.Height+".";
				return false;
			}
			if((long)image.Pixels.Length*2>int.MaxValue-1024*1024) {
				error="The image is too large for one file.";
				return false;
			}
			if(image.PixelFlavor!=AcquiredImage.PixelFlavorChocolate && image.PixelFlavor!=AcquiredImage.PixelFlavorVanilla) {
				error="The image's pixel flavor ("+image.PixelFlavor+") is not known.";
				return false;
			}
			//Samples the saver inverted follow the verified polarity, whatever flavor the source reported: the operator confirmed on the phantom,
			//after the inversion, that radiopaque markers show white. twain-contract 1.1 section 6.2 keeps TWPF_CHOCOLATE and publishes only after
			//the clinic run whether the values follow it, so Invert must work for a CHOCOLATE source too.
			if(!image.IsInverted && image.PixelFlavor!=AcquiredImage.PixelFlavorChocolate) {
				error="The stored samples would have 0 as white (TWPF_VANILLA). DICOM MONOCHROME2 with Pixel Intensity Relationship Sign -1 needs "
					+"higher values to be more radiopaque; a vanilla source needs the verified Polarity=Invert (design 7.8).";
				return false;
			}
			CaptureMetadata captureMetadata=image.Metadata;
			if(captureMetadata!=null) {
				if(captureMetadata.Rendering!=null && captureMetadata.Rendering!=CaptureMetadata.RenderingOriginal) {
					error="The metadata says the image is the '"+captureMetadata.Rendering+"' rendering; only the sensor software's original is written "
						+"as 16-bit DICOM.";
					return false;
				}
				if(captureMetadata.Adjustments!=null && !captureMetadata.Adjustments.IsNeutral) {
					error="The metadata says installation adjustments were applied; they never apply to a 16-bit original, so the image is not "
						+"written as DICOM.";
					return false;
				}
			}
			//Imager Pixel Spacing is row spacing (between rows, vertical) then column spacing (between columns, horizontal).
			if(image.XDpi>0 && image.YDpi>0) {
				imagerPixelSpacingRow=DicomText.FormatDecimal(25.4/image.YDpi);
				imagerPixelSpacingColumn=DicomText.FormatDecimal(25.4/image.XDpi);
			}
			else if(captureMetadata?.PixelSpacingXMm!=null && captureMetadata.PixelSpacingYMm!=null) {
				imagerPixelSpacingRow=DicomText.FormatDecimal(captureMetadata.PixelSpacingYMm.Value);
				imagerPixelSpacingColumn=DicomText.FormatDecimal(captureMetadata.PixelSpacingXMm.Value);
			}
			else {
				error="The image has no resolution and no pixel spacing, so Imager Pixel Spacing (Type 1) is unknown.";
				return false;
			}
			foreach(string uid in new[] { image.StudyInstanceUid,image.SeriesInstanceUid,image.SopInstanceUid }) {
				if(!string.IsNullOrEmpty(uid) && !DicomUid.IsValid(uid)) {
					error="Not a valid DICOM UID: "+uid;
					return false;
				}
			}
			return true;
		}

		///<summary>rendering=original; polarity=inverted when the saver inverted the samples; then the configured exposure, labelled. ASCII.</summary>
		private static string GetImageComments(DicomIntraOralImage image) {
			StringBuilder stringBuilder=new StringBuilder("rendering="+CaptureMetadata.RenderingOriginal);
			if(image.IsInverted) {
				stringBuilder.Append("; polarity=inverted");
			}
			CaptureMetadataExposure exposure=image.Metadata?.ConfiguredExposure;
			if(exposure!=null && exposure.HasAnyValue) {
				stringBuilder.Append("; ").Append(ConfiguredExposureLabel).Append(exposure.ToText());
			}
			return stringBuilder.ToString();
		}

		private static DicomDatasetBuilder BuildDataset(DicomIntraOralImage image,IntraOralAnatomy anatomy,DicomWriteResult result,
			string imagerPixelSpacingRow,string imagerPixelSpacingColumn)
		{
			CaptureMetadata captureMetadata=image.Metadata;
			DateTime dateTimeCreated=image.CreatedAt ?? DateTime.Now;
			DateTime dateTimeContent;
			if(captureMetadata?.AcquiredAt!=null) {
				dateTimeContent=captureMetadata.AcquiredAt.Value.LocalDateTime;
			}
			else {
				dateTimeContent=image.ReceivedAt ?? dateTimeCreated;
			}
			DicomDatasetBuilder dataset=new DicomDatasetBuilder();
			//SOP Common
			dataset.AddString(0x0008,0x0016,"UI",DicomUid.SopClassIntraOralForPresentation);
			dataset.AddString(0x0008,0x0018,"UI",result.SopInstanceUid);
			dataset.AddString(0x0008,0x0012,"DA",DicomText.FormatDate(dateTimeCreated));
			dataset.AddString(0x0008,0x0013,"TM",DicomText.FormatTime(dateTimeCreated));
			//Patient: Type 2, empty on purpose
			dataset.AddString(0x0010,0x0010,"PN","");
			dataset.AddString(0x0010,0x0020,"LO","");
			dataset.AddString(0x0010,0x0030,"DA","");
			dataset.AddString(0x0010,0x0040,"CS","");
			//General Study
			dataset.AddString(0x0020,0x000D,"UI",result.StudyInstanceUid);
			bool hasStudyDate=image.StudyDateTime>DateTime.MinValue;
			dataset.AddString(0x0008,0x0020,"DA",hasStudyDate ? DicomText.FormatDate(image.StudyDateTime) : "");
			dataset.AddString(0x0008,0x0030,"TM",hasStudyDate ? DicomText.FormatTime(image.StudyDateTime) : "");
			dataset.AddString(0x0008,0x0090,"PN","");
			dataset.AddString(0x0020,0x0010,"SH","");
			dataset.AddString(0x0008,0x0050,"SH","");
			//General Series, DX Series, Intra-oral Series
			dataset.AddString(0x0008,0x0060,"CS","IO");
			dataset.AddString(0x0020,0x000E,"UI",result.SeriesInstanceUid);
			dataset.AddString(0x0020,0x0011,"IS",image.SeriesNumber==null ? "" : DicomText.FormatInteger(image.SeriesNumber.Value));
			dataset.AddString(0x0008,0x0068,"CS","FOR PRESENTATION");
			//General Equipment
			dataset.AddString(0x0008,0x0070,"LO",DicomText.Clean(captureMetadata?.Manufacturer,64));
			string model=DicomText.Clean(captureMetadata?.Model,64);
			if(model!="") {
				dataset.AddString(0x0008,0x1090,"LO",model);
			}
			string serial=DicomText.Clean(captureMetadata?.Serial,64);
			if(serial!="") {
				dataset.AddString(0x0018,0x1000,"LO",serial);
			}
			//General Image, DX Image
			dataset.AddString(0x0008,0x0008,"CS","ORIGINAL","PRIMARY","");
			dataset.AddString(0x0020,0x0013,"IS",image.InstanceNumber==null ? "" : DicomText.FormatInteger(image.InstanceNumber.Value));
			dataset.AddString(0x0020,0x0020,"CS",anatomy.OrientationRow,anatomy.OrientationColumn);
			dataset.AddString(0x0008,0x0023,"DA",DicomText.FormatDate(dateTimeContent));
			dataset.AddString(0x0008,0x0033,"TM",DicomText.FormatTime(dateTimeContent));
			if(captureMetadata?.AcquiredAt!=null) {
				dataset.AddString(0x0008,0x002A,"DT",DicomText.FormatDateTime(captureMetadata.AcquiredAt.Value));
			}
			dataset.AddString(0x0020,0x4000,"LT",result.ImageComments);
			dataset.AddString(0x0028,0x0301,"CS","NO");
			dataset.AddString(0x0028,0x2110,"CS","00");
			dataset.AddString(0x0028,0x1040,"CS",image.IsLogarithmic ? "LOG" : "LIN");
			dataset.AddSS(0x0028,0x1041,-1);
			dataset.AddString(0x0028,0x1052,"DS","0");
			dataset.AddString(0x0028,0x1053,"DS","1");
			dataset.AddString(0x0028,0x1054,"LO","US");
			dataset.AddString(0x2050,0x0020,"CS","IDENTITY");
			//Image Pixel
			dataset.AddUS(0x0028,0x0002,1);
			dataset.AddString(0x0028,0x0004,"CS","MONOCHROME2");
			dataset.AddUS(0x0028,0x0010,(ushort)image.Height);
			dataset.AddUS(0x0028,0x0011,(ushort)image.Width);
			dataset.AddUS(0x0028,0x0100,16);
			dataset.AddUS(0x0028,0x0101,(ushort)result.BitsStored);
			dataset.AddUS(0x0028,0x0102,(ushort)(result.BitsStored-1));
			dataset.AddUS(0x0028,0x0103,0);
			dataset.AddOW(0x7FE0,0x0010,image.Pixels);
			//VOI LUT, FOR PRESENTATION only
			double windowCenter;
			double windowWidth;
			GetWindowCenterWidth(result.WindowLow,result.WindowHigh,out windowCenter,out windowWidth);
			dataset.AddString(0x0028,0x1050,"DS",DicomText.FormatDecimal(windowCenter));
			dataset.AddString(0x0028,0x1051,"DS",DicomText.FormatDecimal(windowWidth));
			//DX Detector
			dataset.AddString(0x0018,0x1164,"DS",imagerPixelSpacingRow,imagerPixelSpacingColumn);
			string detectorType=(captureMetadata?.DetectorType ?? "").Trim().ToUpperInvariant();
			dataset.AddString(0x0018,0x7004,"CS",_arrayDetectorTypes.Contains(detectorType) ? detectorType : "");
			string serialShort=DicomText.Clean(captureMetadata?.Serial,int.MaxValue);
			if(serialShort!="" && serialShort.Length<=16) {
				dataset.AddString(0x0018,0x700A,"SH",serialShort);
			}
			//DX Anatomy Imaged, Intra-oral Image
			dataset.AddString(0x0020,0x0062,"CS",anatomy.ImageLaterality);
			dataset.AddSequence(0x0008,0x2218,new List<DicomDatasetBuilder> { BuildCodeItem(anatomy.AnatomicRegion) });
			dataset.AddSequence(0x0008,0x2228,anatomy.ListToothCodes.Select(x => BuildCodeItem(x)).ToList());
			dataset.AddString(0x0018,0x1508,"CS","NONE");
			//Acquisition Context: Type 2, empty
			dataset.AddSequence(0x0040,0x0555,new List<DicomDatasetBuilder>());
			return dataset;
		}

		private static DicomDatasetBuilder BuildCodeItem(DicomCode dicomCode) {
			DicomDatasetBuilder item=new DicomDatasetBuilder();
			item.AddString(0x0008,0x0100,"SH",dicomCode.CodeValue);
			item.AddString(0x0008,0x0102,"SH",dicomCode.CodingSchemeDesignator);
			item.AddString(0x0008,0x0104,"LO",DicomText.Clean(dicomCode.CodeMeaning,64));
			return item;
		}

		///<summary>Preamble, "DICM", File Meta Information (always Explicit VR Little Endian, PS3.10 7.1), then the data set.</summary>
		private static byte[] BuildFile(DicomDatasetBuilder dataset,string sopInstanceUid) {
			DicomDatasetBuilder meta=new DicomDatasetBuilder();
			meta.AddOB(0x0002,0x0001,new byte[] { 0x00,0x01 });
			meta.AddString(0x0002,0x0002,"UI",DicomUid.SopClassIntraOralForPresentation);
			meta.AddString(0x0002,0x0003,"UI",sopInstanceUid);
			meta.AddString(0x0002,0x0010,"UI",DicomUid.TransferSyntaxExplicitVrLittleEndian);
			meta.AddString(0x0002,0x0012,"UI",DicomUid.ImplementationClassUid);
			meta.AddString(0x0002,0x0013,"SH",ImplementationVersionName);
			byte[] bytesMeta=meta.ToBytes();
			DicomDatasetBuilder groupLength=new DicomDatasetBuilder();
			groupLength.AddUL(0x0002,0x0000,(uint)bytesMeta.Length);
			byte[] bytesGroupLength=groupLength.ToBytes();
			byte[] bytesDataset=dataset.ToBytes();
			using(MemoryStream memoryStream=new MemoryStream(132+bytesGroupLength.Length+bytesMeta.Length+bytesDataset.Length)) {
				memoryStream.Write(new byte[128],0,128);
				memoryStream.Write(Encoding.ASCII.GetBytes("DICM"),0,4);
				memoryStream.Write(bytesGroupLength,0,bytesGroupLength.Length);
				memoryStream.Write(bytesMeta,0,bytesMeta.Length);
				memoryStream.Write(bytesDataset,0,bytesDataset.Length);
				return memoryStream.ToArray();
			}
		}
	}
}
