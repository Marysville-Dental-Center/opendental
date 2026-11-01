// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Text;
using ImagingCapture.Uvc.Interop;

namespace ImagingCapture.Uvc {
	///<summary>One stream format a camera offers on its capture pin: frame size, subtype and the fastest frame rate. Immutable.</summary>
	public sealed class UvcMode {
		///<summary>The backend's index for this format (for DirectShow, the IAMStreamConfig::GetStreamCaps index).</summary>
		public int Index { get; }
		public int Width { get; }
		public int Height { get; }
		///<summary>The media subtype, for example MEDIASUBTYPE_MJPG.</summary>
		public Guid Subtype { get; }
		///<summary>Short name of the subtype: the FourCC ("MJPG", "YUY2", "NV12"), "RGB24" or "RGB32" and the like, or the GUID.</summary>
		public string SubtypeName { get; }
		public int BitCount { get; }
		///<summary>The shortest frame interval the device supports for this format, in 100 ns units, or 0 when it reports none.</summary>
		public long FrameIntervalMin { get; }

		public UvcMode(int index,int width,int height,Guid subtype,int bitCount,long frameIntervalMin) {
			Index=index;
			Width=width;
			Height=height;
			Subtype=subtype;
			SubtypeName=GetSubtypeName(subtype);
			BitCount=bitCount;
			FrameIntervalMin=Math.Max(0,frameIntervalMin);
		}

		///<summary>The fastest frame rate of this format, or 0 when unknown.</summary>
		public double MaxFramesPerSecond {
			get {
				if(FrameIntervalMin<=0) {
					return 0;
				}
				return 10000000.0/FrameIntervalMin;
			}
		}

		///<summary>Width times height.</summary>
		public long Area {
			get {
				return (long)Width*Height;
			}
		}

		///<summary>True when other has the same size and subtype.</summary>
		public bool IsSameFormat(UvcMode other) {
			return other!=null && other.Width==Width && other.Height==Height && other.Subtype==Subtype;
		}

		///<summary>Example: "1920x1080 MJPG 30 fps".</summary>
		public override string ToString() {
			string text=Width.ToString(CultureInfo.InvariantCulture)+"x"+Height.ToString(CultureInfo.InvariantCulture)+" "+SubtypeName;
			if(FrameIntervalMin>0) {
				text+=" "+MaxFramesPerSecond.ToString("0.#",CultureInfo.InvariantCulture)+" fps";
			}
			return text;
		}

		///<summary>The short name of a video subtype. FourCC subtypes (Data1 holds four printable characters and the rest is the
		///FourCC base GUID) give the FourCC. Uncompressed RGB subtypes give their DirectShow names. Anything else gives the GUID in
		///braces.</summary>
		public static string GetSubtypeName(Guid subtype) {
			if(subtype==Ds.MEDIASUBTYPE_RGB24) {
				return "RGB24";
			}
			if(subtype==Ds.MEDIASUBTYPE_RGB32) {
				return "RGB32";
			}
			if(subtype==Ds.MEDIASUBTYPE_ARGB32) {
				return "ARGB32";
			}
			if(subtype==Ds.MEDIASUBTYPE_RGB565) {
				return "RGB565";
			}
			if(subtype==Ds.MEDIASUBTYPE_RGB555) {
				return "RGB555";
			}
			byte[] arrayBytes=subtype.ToByteArray();
			byte[] arrayBase=Ds.FourCCBase.ToByteArray();
			bool isFourCC=true;
			for(int i=4;i<16;i++) {
				if(arrayBytes[i]!=arrayBase[i]) {
					isFourCC=false;
					break;
				}
			}
			for(int i=0;i<4 && isFourCC;i++) {
				if(arrayBytes[i]<0x20 || arrayBytes[i]>0x7E) {
					isFourCC=false;
				}
			}
			if(isFourCC) {
				string fourCC=Encoding.ASCII.GetString(arrayBytes,0,4).TrimEnd(' ');
				if(fourCC!="") {
					return fourCC;
				}
			}
			return subtype.ToString("B").ToUpperInvariant();
		}
	}
}
