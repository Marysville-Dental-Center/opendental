// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingDeviceManager.TwainProtocol {
	//TWAIN data structures as laid out on 32-bit Windows: two-byte packing, 4-byte pointers and handles.
	//Unit tests check every size against the TWAIN specification.

	///<summary>TW_FIX32: fixed point number, whole part plus 1/65536 fraction.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_FIX32 {
		public short Whole;
		public ushort Frac;

		public static TW_FIX32 FromDouble(double value) {
			int fixedValue=(int)Math.Round(value*65536.0);
			return new TW_FIX32 { Whole=(short)(fixedValue>>16),Frac=(ushort)(fixedValue&0xffff) };
		}

		public double ToDouble() {
			return Whole+Frac/65536.0;
		}

		///<summary>The 32-bit pattern used when a FIX32 is stored in the Item field of a TW_ONEVALUE.</summary>
		public uint ToUInt32() {
			return (uint)(ushort)Whole|((uint)Frac<<16);
		}

		public static TW_FIX32 FromUInt32(uint bits) {
			return new TW_FIX32 { Whole=(short)(bits&0xffff),Frac=(ushort)(bits>>16) };
		}
	}

	///<summary>TW_VERSION. 42 bytes.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2,CharSet=CharSet.Ansi)]
	internal struct TW_VERSION {
		public ushort MajorNum;
		public ushort MinorNum;
		public ushort Language;
		public ushort Country;
		[MarshalAs(UnmanagedType.ByValTStr,SizeConst=34)]
		public string Info;
	}

	///<summary>TW_IDENTITY. 156 bytes. Identifies the application and each data source.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2,CharSet=CharSet.Ansi)]
	internal struct TW_IDENTITY {
		public uint Id;
		public TW_VERSION Version;
		public ushort ProtocolMajor;
		public ushort ProtocolMinor;
		public uint SupportedGroups;
		[MarshalAs(UnmanagedType.ByValTStr,SizeConst=34)]
		public string Manufacturer;
		[MarshalAs(UnmanagedType.ByValTStr,SizeConst=34)]
		public string ProductFamily;
		[MarshalAs(UnmanagedType.ByValTStr,SizeConst=34)]
		public string ProductName;
	}

	///<summary>TW_USERINTERFACE. 8 bytes.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_USERINTERFACE {
		public ushort ShowUI;
		public ushort ModalUI;
		public IntPtr hParent;
	}

	///<summary>TW_EVENT. 6 bytes. pEvent points to a Windows MSG.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_EVENT {
		public IntPtr pEvent;
		public ushort TWMessage;
	}

	///<summary>TW_PENDINGXFERS. 6 bytes. Count is -1 when the source does not know how many transfers remain.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_PENDINGXFERS {
		public short Count;
		public uint EOJ;
	}

	///<summary>TW_STATUS. 4 bytes.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_STATUS {
		public ushort ConditionCode;
		public ushort Data;
	}

	///<summary>TW_STATUSUTF8. 12 bytes. Status is the input; Size (bytes including the terminating NUL) and the UTF8string handle,
	///which the application frees (TWAIN 2.4 p.8-61), are the output.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_STATUSUTF8 {
		public TW_STATUS Status;
		public uint Size;
		public IntPtr UTF8string;
	}

	///<summary>TW_CAPABILITY. 8 bytes. hContainer is a handle to a TW_ONEVALUE, TW_ENUMERATION, TW_ARRAY or TW_RANGE.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_CAPABILITY {
		public ushort Cap;
		public ushort ConType;
		public IntPtr hContainer;
	}

	///<summary>TW_IMAGEINFO. 42 bytes.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_IMAGEINFO {
		public TW_FIX32 XResolution;
		public TW_FIX32 YResolution;
		public int ImageWidth;
		public int ImageLength;
		public short SamplesPerPixel;
		[MarshalAs(UnmanagedType.ByValArray,SizeConst=8)]
		public short[] BitsPerSample;
		public short BitsPerPixel;
		public ushort Planar;
		public short PixelType;
		public ushort Compression;
	}

	///<summary>TW_SETUPMEMXFER. 12 bytes.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_SETUPMEMXFER {
		public uint MinBufSize;
		public uint MaxBufSize;
		public uint Preferred;
	}

	///<summary>TW_MEMORY. 12 bytes.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_MEMORY {
		public uint Flags;
		public uint Length;
		public IntPtr TheMem;
	}

	///<summary>TW_IMAGEMEMXFER. 38 bytes. One strip (or tile) of a memory transfer.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_IMAGEMEMXFER {
		public ushort Compression;
		public uint BytesPerRow;
		public uint Columns;
		public uint Rows;
		public uint XOffset;
		public uint YOffset;
		public uint BytesWritten;
		public TW_MEMORY Memory;
	}

	///<summary>TW_PALETTE8. 1028 bytes. Colors holds 256 entries of {Index, Channel1, Channel2, Channel3}.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_PALETTE8 {
		public ushort NumColors;
		public ushort PaletteType;
		[MarshalAs(UnmanagedType.ByValArray,SizeConst=1024)]
		public byte[] Colors;
	}

	///<summary>TW_ENTRYPOINT. 24 bytes. Memory management functions provided by a version 2 Data Source Manager.</summary>
	[StructLayout(LayoutKind.Sequential,Pack=2)]
	internal struct TW_ENTRYPOINT {
		public uint Size;
		public IntPtr DSM_Entry;
		public IntPtr DSM_MemAllocate;
		public IntPtr DSM_MemFree;
		public IntPtr DSM_MemLock;
		public IntPtr DSM_MemUnlock;
	}

	///<summary>Byte offsets inside capability containers (two-byte packing).</summary>
	internal static class TwContainer {
		///<summary>TW_ONEVALUE: ItemType(2) Item(4).</summary>
		public const int OneValueSize=6;
		public const int OneValueItemOffset=2;
		///<summary>TW_ENUMERATION: ItemType(2) NumItems(4) CurrentIndex(4) DefaultIndex(4) ItemList[].</summary>
		public const int EnumNumItemsOffset=2;
		public const int EnumCurrentIndexOffset=6;
		public const int EnumDefaultIndexOffset=10;
		public const int EnumItemListOffset=14;
		///<summary>TW_RANGE: ItemType(2) MinValue(4) MaxValue(4) StepSize(4) DefaultValue(4) CurrentValue(4).</summary>
		public const int RangeCurrentValueOffset=18;

		///<summary>Size in bytes of one item of the given TWTY_ type inside an enumeration or array.</summary>
		public static int ItemSize(ushort itemType) {
			switch(itemType) {
				case Tw.TWTY_INT8:
				case Tw.TWTY_UINT8:
					return 1;
				case Tw.TWTY_INT16:
				case Tw.TWTY_UINT16:
				case Tw.TWTY_BOOL:
					return 2;
				case Tw.TWTY_INT32:
				case Tw.TWTY_UINT32:
				case Tw.TWTY_FIX32:
					return 4;
				case Tw.TWTY_FRAME:
					return 16;
				case Tw.TWTY_STR32:
					return 34;
				case Tw.TWTY_STR64:
					return 66;
				case Tw.TWTY_STR128:
					return 130;
				case Tw.TWTY_STR255:
					return 256;
				default:
					return 0;
			}
		}
	}
}
