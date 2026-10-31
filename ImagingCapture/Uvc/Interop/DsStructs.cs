// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingCapture.Uvc.Interop {
	//DirectShow structures as documented in the Windows SDK (strmif.h, amvideo.h, wingdi.h), with natural alignment.
	//Unit tests check each size and the field offsets this layer reads.

	///<summary>AM_MEDIA_TYPE. 72 bytes in a 32-bit process. pbFormat and pUnk are owned by whoever filled the structure and are
	///released with DsUtil.FreeMediaType.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct AM_MEDIA_TYPE {
		public Guid majortype;
		public Guid subtype;
		public int bFixedSizeSamples;
		public int bTemporalCompression;
		public int lSampleSize;
		public Guid formattype;
		public IntPtr pUnk;
		public int cbFormat;
		public IntPtr pbFormat;
	}

	///<summary>RECT. 16 bytes.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct RECT {
		public int left;
		public int top;
		public int right;
		public int bottom;
	}

	///<summary>SIZE. 8 bytes.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct SIZE {
		public int cx;
		public int cy;
	}

	///<summary>BITMAPINFOHEADER. 40 bytes.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct BITMAPINFOHEADER {
		public int biSize;
		public int biWidth;
		///<summary>Positive for a bottom-up RGB image, negative for a top-down one.</summary>
		public int biHeight;
		public short biPlanes;
		public short biBitCount;
		public int biCompression;
		public int biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public int biClrUsed;
		public int biClrImportant;
	}

	///<summary>VIDEOINFOHEADER, the format block of FORMAT_VideoInfo. 88 bytes. Any color table or bit masks that follow
	///bmiHeader are not part of this structure.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct VIDEOINFOHEADER {
		public RECT rcSource;
		public RECT rcTarget;
		public int dwBitRate;
		public int dwBitErrorRate;
		///<summary>REFERENCE_TIME, in 100 ns units.</summary>
		public long AvgTimePerFrame;
		public BITMAPINFOHEADER bmiHeader;
	}

	///<summary>VIDEO_STREAM_CONFIG_CAPS, returned by IAMStreamConfig::GetStreamCaps for a video pin. 128 bytes. The frame interval
	///range is in 100 ns units.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct VIDEO_STREAM_CONFIG_CAPS {
		public Guid guid;
		public int VideoStandard;
		public SIZE InputSize;
		public SIZE MinCroppingSize;
		public SIZE MaxCroppingSize;
		public int CropGranularityX;
		public int CropGranularityY;
		public int CropAlignX;
		public int CropAlignY;
		public SIZE MinOutputSize;
		public SIZE MaxOutputSize;
		public int OutputGranularityX;
		public int OutputGranularityY;
		public int StretchTapsX;
		public int StretchTapsY;
		public int ShrinkTapsX;
		public int ShrinkTapsY;
		public long MinFrameInterval;
		public long MaxFrameInterval;
		public int MinBitsPerSecond;
		public int MaxBitsPerSecond;
	}
}
