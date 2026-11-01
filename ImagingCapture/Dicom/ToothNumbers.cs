// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ImagingCapture.Dicom {
	///<summary>One tooth of the permanent (1-32) or primary (A-T) dentition, with its ISO 3950 (FDI) number. The ISO number is the code
	///value of the tooth in DICOM's Primary Anatomic Structure Sequence (design 7.8).</summary>
	public sealed class ToothInfo {
		///<summary>The Universal number as Open Dental stores it: "1" to "32", or "A" to "T".</summary>
		public string Universal { get; set; }
		///<summary>The ISO 3950 two-digit number: quadrant then position, for example "18" for Universal 1 and "55" for Universal A.</summary>
		public string Iso { get; set; }
		///<summary>ISO 3950 quadrant: 1-4 permanent (upper right, upper left, lower left, lower right), 5-8 primary in the same order.</summary>
		public int Quadrant { get; set; }
		///<summary>ISO 3950 position from the midline: 1-8 permanent, 1-5 primary.</summary>
		public int Position { get; set; }
		public bool IsPrimary { get; set; }
		///<summary>An upper tooth (quadrants 1, 2, 5, 6).</summary>
		public bool IsMaxillary { get; set; }
		///<summary>On the patient's right (quadrants 1, 4, 5, 8).</summary>
		public bool IsPatientRight { get; set; }
		///<summary>An incisor or a canine (position 1-3).</summary>
		public bool IsAnterior { get; set; }
		///<summary>For example "Permanent maxillary right central incisor" or "Deciduous mandibular left second molar". ASCII.</summary>
		public string Name { get; set; }
	}

	///<summary>Universal to ISO 3950 (FDI two-digit) tooth numbers and back, for the permanent teeth 1-32 and the primary teeth A-T. Open
	///Dental stores tooth numbers as Universal numbers whatever the display nomenclature, so these are the only inputs. Supernumerary
	///teeth (Open Dental's 51-82 and AS-TS) have no ISO 3950 two-digit number and are not mapped.</summary>
	public static class ToothNumbers {
		private static readonly string[] _arrayPermanentNames={ "central incisor","lateral incisor","canine","first premolar","second premolar",
			"first molar","second molar","third molar" };
		private static readonly string[] _arrayPrimaryNames={ "central incisor","lateral incisor","canine","first molar","second molar" };

		///<summary>The ISO 3950 number of a Universal tooth number ("1"-"32", "A"-"T", letters in either case), or null for anything else.</summary>
		public static string UniversalToIso(string universal) {
			ToothInfo toothInfo=GetInfo(universal);
			return toothInfo?.Iso;
		}

		///<summary>The Universal number of an ISO 3950 number ("11"-"48" with positions 1-8, "51"-"85" with positions 1-5), or null for
		///anything else.</summary>
		public static string IsoToUniversal(string iso) {
			if(iso==null) {
				return null;
			}
			iso=iso.Trim();
			if(iso.Length!=2 || iso[0]<'1' || iso[0]>'8' || iso[1]<'1' || iso[1]>'8') {
				return null;
			}
			int quadrant=iso[0]-'0';
			int position=iso[1]-'0';
			if(quadrant<=4) {
				switch(quadrant) {
					case 1: return Num(9-position);
					case 2: return Num(8+position);
					case 3: return Num(25-position);
					default: return Num(24+position);
				}
			}
			if(position>5) {
				return null;
			}
			switch(quadrant) {
				case 5: return Letter(5-position);
				case 6: return Letter(4+position);
				case 7: return Letter(15-position);
				default: return Letter(14+position);
			}
		}

		///<summary>Everything about a Universal tooth number, or null when it is not "1"-"32" or "A"-"T". A number with a leading zero, a
		///sign or spaces inside is not a tooth number; surrounding spaces are ignored.</summary>
		public static ToothInfo GetInfo(string universal) {
			if(universal==null) {
				return null;
			}
			universal=universal.Trim().ToUpperInvariant();
			if(universal.Length==1 && universal[0]>='A' && universal[0]<='T') {
				return GetPrimary(universal[0]-'A');
			}
			if(universal.Length<1 || universal.Length>2 || universal[0]=='0') {
				return null;
			}
			int number=0;
			foreach(char c in universal) {
				if(c<'0' || c>'9') {
					return null;
				}
				number=number*10+(c-'0');
			}
			if(number<1 || number>32) {
				return null;
			}
			return GetPermanent(number);
		}

		///<summary>Reads a tooth list as Open Dental stores it on a mount item or document: Universal numbers separated by commas, for
		///example "2,3,30,31". Spaces and empty entries are ignored, and a tooth listed twice counts once (the first one keeps its place).
		///A blank list gives an empty list. False, with error, when an entry is not a tooth this class maps (a range such as "2-5" is not the
		///stored format, and supernumerary teeth have no ISO 3950 number).</summary>
		public static bool TryParseList(string toothNumbers,out List<ToothInfo> listTeeth,out string error) {
			listTeeth=new List<ToothInfo>();
			error="";
			if(string.IsNullOrWhiteSpace(toothNumbers)) {
				return true;
			}
			foreach(string entry in toothNumbers.Split(',')) {
				string entryTrimmed=entry.Trim();
				if(entryTrimmed=="") {
					continue;
				}
				ToothInfo toothInfo=GetInfo(entryTrimmed);
				if(toothInfo==null) {
					listTeeth.Clear();
					error="'"+entryTrimmed+"' is not a permanent (1-32) or primary (A-T) Universal tooth number.";
					return false;
				}
				if(!listTeeth.Exists(x => x.Universal==toothInfo.Universal)) {
					listTeeth.Add(toothInfo);
				}
			}
			return true;
		}

		private static ToothInfo GetPermanent(int number) {
			int quadrant;
			int position;
			if(number<=8) {
				quadrant=1;
				position=9-number;
			}
			else if(number<=16) {
				quadrant=2;
				position=number-8;
			}
			else if(number<=24) {
				quadrant=3;
				position=25-number;
			}
			else {
				quadrant=4;
				position=number-24;
			}
			return Build(Num(number),quadrant,position,false);
		}

		///<summary>index 0 is A.</summary>
		private static ToothInfo GetPrimary(int index) {
			int quadrant;
			int position;
			if(index<=4) {
				quadrant=5;
				position=5-index;
			}
			else if(index<=9) {
				quadrant=6;
				position=index-4;
			}
			else if(index<=14) {
				quadrant=7;
				position=15-index;
			}
			else {
				quadrant=8;
				position=index-14;
			}
			return Build(Letter(index),quadrant,position,true);
		}

		private static ToothInfo Build(string universal,int quadrant,int position,bool isPrimary) {
			int quadrantPermanent=isPrimary ? quadrant-4 : quadrant;
			ToothInfo toothInfo=new ToothInfo {
				Universal=universal,
				Iso=quadrant.ToString(CultureInfo.InvariantCulture)+position.ToString(CultureInfo.InvariantCulture),
				Quadrant=quadrant,
				Position=position,
				IsPrimary=isPrimary,
				IsMaxillary=quadrantPermanent==1 || quadrantPermanent==2,
				IsPatientRight=quadrantPermanent==1 || quadrantPermanent==4,
				IsAnterior=position<=3,
			};
			toothInfo.Name=(isPrimary ? "Deciduous " : "Permanent ")+(toothInfo.IsMaxillary ? "maxillary " : "mandibular ")
				+(toothInfo.IsPatientRight ? "right " : "left ")+(isPrimary ? _arrayPrimaryNames[position-1] : _arrayPermanentNames[position-1]);
			return toothInfo;
		}

		private static string Num(int number) {
			return number.ToString(CultureInfo.InvariantCulture);
		}

		private static string Letter(int index) {
			return ((char)('A'+index)).ToString();
		}
	}
}
