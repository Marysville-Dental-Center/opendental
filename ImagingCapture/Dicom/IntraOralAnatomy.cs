// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImagingCapture.Dicom {
	///<summary>Where an intra-oral image goes: the mount slot the capture session targeted (design 7.3, 7.10). The writer needs it for the
	///anatomy and orientation attributes, so a target with no slot, or a slot with no tooth numbers, is never written as DICOM; the
	///session captures it at 8 bits instead.</summary>
	public sealed class IntraOralTarget {
		///<summary>The image goes into a mount slot (MountItem). False for a single image with no mount (design 7.10).</summary>
		public bool HasMountSlot { get; set; }
		///<summary>The slot's tooth numbers as stored: Universal numbers separated by commas (MountItem.ToothNumbers, copied to the document).</summary>
		public string ToothNumbers { get; set; }
		///<summary>The display rotation the slot applies, clockwise degrees (MountItem.RotateOnAcquire, which becomes Document.DegreesRotated):
		///0, 90, 180 or 270.</summary>
		public int DegreesRotated { get; set; }
		///<summary>The display mirror the mount applies (Mount.FlipOnAcquire, which becomes Document.IsFlipped).</summary>
		public bool IsFlipped { get; set; }
	}

	///<summary>Which way the labial-mounted display shows a slot (design 7.10).</summary>
	public enum IntraOralView {
		///<summary>Incisors and canines: the image's rows run toward the patient's left (L\F as displayed).</summary>
		Anterior,
		///<summary>Premolars and molars on the patient's right: the rows run toward the front (A\F as displayed).</summary>
		RightPosterior,
		///<summary>Premolars and molars on the patient's left: the rows run toward the back (P\F as displayed).</summary>
		LeftPosterior,
	}

	///<summary>A coded entry (PS3.3 Table 8.8-1, Code Sequence Macro): Code Value, Coding Scheme Designator and Code Meaning.</summary>
	public sealed class DicomCode {
		public string CodeValue { get; set; }
		public string CodingSchemeDesignator { get; set; }
		public string CodeMeaning { get; set; }
	}

	///<summary>The anatomy attributes of an intra-oral image, derived from its target slot (design 7.8, 7.10):
	///- Image Laterality from the teeth: R when every tooth is on the patient's right (Universal 1-8, 25-32, A-E, P-T), L when every tooth is
	///  on the left, B (both, the midline) otherwise. DICOM has no "unknown" laterality.
	///- the anatomic region (CID 4016): maxilla, mandible, or both;
	///- the tooth codes (ISO 3950) for the Primary Anatomic Structure Sequence;
	///- Patient Orientation of the stored, unrotated rows and columns: the slot's view as the labial mount displays it (anterior L\F,
	///  patient-right posterior A\F, patient-left posterior P\F) with the slot's display rotation and mirror undone.
	///A slot whose teeth mix anterior and posterior teeth, or posterior teeth of both sides, has no single view; like a target with no slot,
	///its orientation is unknown and it is not written as DICOM.</summary>
	public sealed class IntraOralAnatomy {
		///<summary>The coding scheme of the tooth codes: ISO 3950 two-digit tooth numbers.</summary>
		public const string CodingSchemeIso3950="ISO3950";
		public const string CodingSchemeSnomedCt="SCT";

		public List<ToothInfo> ListTeeth { get; set; }
		public IntraOralView View { get; set; }
		///<summary>R, L or B.</summary>
		public string ImageLaterality { get; set; }
		///<summary>The single Anatomic Region Sequence item.</summary>
		public DicomCode AnatomicRegion { get; set; }
		///<summary>One Primary Anatomic Structure Sequence item per tooth, in the slot's order.</summary>
		public List<DicomCode> ListToothCodes { get; set; }
		///<summary>Patient Orientation value 1: the patient direction of the stored rows (left to right along a row). One of L, R, A, P, H, F.</summary>
		public string OrientationRow { get; set; }
		///<summary>Patient Orientation value 2: the direction of the stored columns (top to bottom along a column).</summary>
		public string OrientationColumn { get; set; }

		///<summary>"row\column", for example "L\F".</summary>
		public string PatientOrientation => OrientationRow+"\\"+OrientationColumn;

		///<summary>The anatomy of a target, or null with error when it cannot be written as DICOM: no target, no mount slot, no tooth
		///numbers, a tooth list that is not permanent or primary Universal numbers, teeth with no single view, or a rotation that is not a
		///multiple of 90 degrees.</summary>
		public static IntraOralAnatomy FromTarget(IntraOralTarget target,out string error) {
			error="";
			if(target==null || !target.HasMountSlot) {
				error="The image has no mount slot, so its orientation is unknown (design 7.10). It is captured at 8 bits and never written as DICOM.";
				return null;
			}
			List<ToothInfo> listTeeth;
			string errorTeeth;
			if(!ToothNumbers.TryParseList(target.ToothNumbers,out listTeeth,out errorTeeth)) {
				error="The slot's tooth numbers cannot be coded: "+errorTeeth;
				return null;
			}
			if(listTeeth.Count==0) {
				error="The slot has no tooth numbers, so the image has no anatomy (design 7.8). It is captured at 8 bits and never written as DICOM.";
				return null;
			}
			IntraOralView view;
			if(listTeeth.All(x => x.IsAnterior)) {
				view=IntraOralView.Anterior;
			}
			else if(listTeeth.All(x => !x.IsAnterior && x.IsPatientRight)) {
				view=IntraOralView.RightPosterior;
			}
			else if(listTeeth.All(x => !x.IsAnterior && !x.IsPatientRight)) {
				view=IntraOralView.LeftPosterior;
			}
			else {
				error="The slot's teeth ("+target.ToothNumbers+") mix anterior and posterior teeth or both sides, so the slot has no single view "
					+"and its orientation is unknown (design 7.10). It is captured at 8 bits and never written as DICOM.";
				return null;
			}
			string orientation=GetPatientOrientation(view,target.DegreesRotated,target.IsFlipped);
			if(orientation==null) {
				error="The slot's rotation ("+target.DegreesRotated+" degrees) is not 0, 90, 180 or 270, so its orientation is unknown.";
				return null;
			}
			IntraOralAnatomy anatomy=new IntraOralAnatomy();
			anatomy.ListTeeth=listTeeth;
			anatomy.View=view;
			if(listTeeth.All(x => x.IsPatientRight)) {
				anatomy.ImageLaterality="R";
			}
			else if(listTeeth.All(x => !x.IsPatientRight)) {
				anatomy.ImageLaterality="L";
			}
			else {
				anatomy.ImageLaterality="B";
			}
			if(listTeeth.All(x => x.IsMaxillary)) {
				anatomy.AnatomicRegion=DicomCodes.Maxilla;
			}
			else if(listTeeth.All(x => !x.IsMaxillary)) {
				anatomy.AnatomicRegion=DicomCodes.Mandible;
			}
			else {
				anatomy.AnatomicRegion=DicomCodes.MaxillaAndMandible;
			}
			anatomy.ListToothCodes=listTeeth.Select(x => new DicomCode { CodeValue=x.Iso,CodingSchemeDesignator=CodingSchemeIso3950,CodeMeaning=x.Name })
				.ToList();
			string[] arrayParts=orientation.Split('\\');
			anatomy.OrientationRow=arrayParts[0];
			anatomy.OrientationColumn=arrayParts[1];
			return anatomy;
		}

		///<summary>Patient Orientation (0020,0020) of the stored rows and columns, "row\column", or null when degreesRotated is not a multiple
		///of 90. The display (ImageHelper) mirrors the stored image left to right first when isFlipped, then turns it degreesRotated clockwise,
		///and shows the view's directions: the row direction of the view (L, A or P) to the right and F downward. Undoing that: the stored
		///+x and +y unit vectors are mirrored and turned the same way, and the screen direction each lands on names its patient direction.</summary>
		public static string GetPatientOrientation(IntraOralView view,int degreesRotated,bool isFlipped) {
			int degrees=((degreesRotated%360)+360)%360;
			if(degrees%90!=0) {
				return null;
			}
			string rowDisplayed;
			switch(view) {
				case IntraOralView.Anterior:
					rowDisplayed="L";
					break;
				case IntraOralView.RightPosterior:
					rowDisplayed="A";
					break;
				default:
					rowDisplayed="P";
					break;
			}
			int turns=degrees/90;
			string row=GetDirection(isFlipped ? -1 : 1,0,turns,rowDisplayed);
			string column=GetDirection(0,1,turns,rowDisplayed);
			return row+"\\"+column;
		}

		///<summary>The patient direction of the screen vector (x, y) after turns quarter turns clockwise (screen y points down).</summary>
		private static string GetDirection(int x,int y,int turns,string rowDisplayed) {
			for(int i=0;i<turns;i++) {
				int xTurned=-y;
				y=x;
				x=xTurned;
			}
			if(x==1) {
				return rowDisplayed;
			}
			if(x==-1) {
				return Opposite(rowDisplayed);
			}
			return y==1 ? "F" : "H";
		}

		private static string Opposite(string direction) {
			switch(direction) {
				case "L": return "R";
				case "R": return "L";
				case "A": return "P";
				case "P": return "A";
				case "H": return "F";
				default: return "H";
			}
		}
	}

	///<summary>The coded concepts the intra-oral writer uses. Kept in one place for review against PS3.16 (design 13: the written files are
	///checked with an external validator before 16-bit is enabled).</summary>
	public static class DicomCodes {
		///<summary>CID 4016, Anatomic Region for Intra-oral Radiography: the maxilla.</summary>
		public static DicomCode Maxilla => new DicomCode { CodeValue="70925003",CodingSchemeDesignator=IntraOralAnatomy.CodingSchemeSnomedCt,
			CodeMeaning="Maxilla" };
		///<summary>CID 4016: the mandible.</summary>
		public static DicomCode Mandible => new DicomCode { CodeValue="91609006",CodingSchemeDesignator=IntraOralAnatomy.CodingSchemeSnomedCt,
			CodeMeaning="Mandible" };
		///<summary>CID 4016: both jaws, for a slot with upper and lower teeth such as a bitewing.</summary>
		public static DicomCode MaxillaAndMandible => new DicomCode { CodeValue="661005",CodingSchemeDesignator=IntraOralAnatomy.CodingSchemeSnomedCt,
			CodeMeaning="Jaw region" };
	}
}
