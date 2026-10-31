// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
namespace ImagingDeviceManager.TwainProtocol {
	///<summary>Numeric constants from the TWAIN specification (data groups, data argument types, messages, return and
	///condition codes, capabilities). Only the values this library uses are listed.</summary>
	internal static class Tw {
		//Data groups
		public const uint DG_CONTROL=0x0001;
		public const uint DG_IMAGE=0x0002;
		//Feature flags for TW_IDENTITY.SupportedGroups
		public const uint DF_DSM2=0x10000000;
		public const uint DF_APP2=0x40000000;
		public const uint DF_DS2=0x20000000;
		//Data argument types
		public const ushort DAT_NULL=0x0000;
		public const ushort DAT_CAPABILITY=0x0001;
		public const ushort DAT_EVENT=0x0002;
		public const ushort DAT_IDENTITY=0x0003;
		public const ushort DAT_PARENT=0x0004;
		public const ushort DAT_PENDINGXFERS=0x0005;
		public const ushort DAT_SETUPMEMXFER=0x0006;
		public const ushort DAT_STATUS=0x0008;
		public const ushort DAT_USERINTERFACE=0x0009;
		public const ushort DAT_IMAGEINFO=0x0101;
		public const ushort DAT_IMAGEMEMXFER=0x0103;
		public const ushort DAT_IMAGENATIVEXFER=0x0104;
		public const ushort DAT_PALETTE8=0x010a;
		public const ushort DAT_ENTRYPOINT=0x0403;
		//Messages
		public const ushort MSG_NULL=0x0000;
		public const ushort MSG_GET=0x0001;
		public const ushort MSG_GETCURRENT=0x0002;
		public const ushort MSG_GETDEFAULT=0x0003;
		public const ushort MSG_GETFIRST=0x0004;
		public const ushort MSG_GETNEXT=0x0005;
		public const ushort MSG_SET=0x0006;
		public const ushort MSG_RESET=0x0007;
		public const ushort MSG_XFERREADY=0x0101;
		public const ushort MSG_CLOSEDSREQ=0x0102;
		public const ushort MSG_CLOSEDSOK=0x0103;
		public const ushort MSG_DEVICEEVENT=0x0104;
		public const ushort MSG_OPENDSM=0x0301;
		public const ushort MSG_CLOSEDSM=0x0302;
		public const ushort MSG_OPENDS=0x0401;
		public const ushort MSG_CLOSEDS=0x0402;
		public const ushort MSG_USERSELECT=0x0403;
		public const ushort MSG_DISABLEDS=0x0501;
		public const ushort MSG_ENABLEDS=0x0502;
		public const ushort MSG_PROCESSEVENT=0x0601;
		public const ushort MSG_ENDXFER=0x0701;
		//Return codes
		public const ushort TWRC_SUCCESS=0;
		public const ushort TWRC_FAILURE=1;
		public const ushort TWRC_CHECKSTATUS=2;
		public const ushort TWRC_CANCEL=3;
		public const ushort TWRC_DSEVENT=4;
		public const ushort TWRC_NOTDSEVENT=5;
		public const ushort TWRC_XFERDONE=6;
		public const ushort TWRC_ENDOFLIST=7;
		//Condition codes
		public const ushort TWCC_SUCCESS=0;
		public const ushort TWCC_BUMMER=1;
		public const ushort TWCC_LOWMEMORY=2;
		public const ushort TWCC_NODS=3;
		public const ushort TWCC_MAXCONNECTIONS=4;
		public const ushort TWCC_OPERATIONERROR=5;
		public const ushort TWCC_BADCAP=6;
		public const ushort TWCC_BADPROTOCOL=9;
		public const ushort TWCC_BADVALUE=10;
		public const ushort TWCC_SEQERROR=11;
		public const ushort TWCC_BADDEST=12;
		public const ushort TWCC_CAPUNSUPPORTED=13;
		public const ushort TWCC_CAPBADOPERATION=14;
		public const ushort TWCC_CAPSEQERROR=15;
		public const ushort TWCC_DENIED=16;
		public const ushort TWCC_PAPERJAM=20;
		public const ushort TWCC_PAPERDOUBLEFEED=21;
		public const ushort TWCC_CHECKDEVICEONLINE=23;
		public const ushort TWCC_NOMEDIA=29;
		//Container types
		public const ushort TWON_ARRAY=3;
		public const ushort TWON_ENUMERATION=4;
		public const ushort TWON_ONEVALUE=5;
		public const ushort TWON_RANGE=6;
		//Item types
		public const ushort TWTY_INT8=0x0000;
		public const ushort TWTY_INT16=0x0001;
		public const ushort TWTY_INT32=0x0002;
		public const ushort TWTY_UINT8=0x0003;
		public const ushort TWTY_UINT16=0x0004;
		public const ushort TWTY_UINT32=0x0005;
		public const ushort TWTY_BOOL=0x0006;
		public const ushort TWTY_FIX32=0x0007;
		public const ushort TWTY_FRAME=0x0008;
		public const ushort TWTY_STR32=0x0009;
		public const ushort TWTY_STR64=0x000a;
		public const ushort TWTY_STR128=0x000b;
		public const ushort TWTY_STR255=0x000c;
		//Capabilities
		public const ushort CAP_XFERCOUNT=0x0001;
		public const ushort ICAP_COMPRESSION=0x0100;
		public const ushort ICAP_PIXELTYPE=0x0101;
		public const ushort ICAP_UNITS=0x0102;
		public const ushort ICAP_XFERMECH=0x0103;
		public const ushort CAP_DUPLEXENABLED=0x1013;
		public const ushort ICAP_XRESOLUTION=0x1118;
		public const ushort ICAP_YRESOLUTION=0x1119;
		public const ushort ICAP_PIXELFLAVOR=0x111f;
		//ICAP_PIXELTYPE values
		public const ushort TWPT_BW=0;
		public const ushort TWPT_GRAY=1;
		public const ushort TWPT_RGB=2;
		public const ushort TWPT_PALETTE=3;
		//ICAP_XFERMECH values
		public const ushort TWSX_NATIVE=0;
		public const ushort TWSX_FILE=1;
		public const ushort TWSX_MEMORY=2;
		//ICAP_UNITS values
		public const ushort TWUN_INCHES=0;
		//ICAP_PIXELFLAVOR values
		public const ushort TWPF_CHOCOLATE=0;
		public const ushort TWPF_VANILLA=1;
		//ICAP_COMPRESSION values
		public const ushort TWCP_NONE=0;
		//TW_MEMORY.Flags
		public const uint TWMF_APPOWNS=0x0001;
		public const uint TWMF_POINTER=0x0008;
		//"Don't care" values
		public const uint TWON_DONTCARE32=0xffffffff;
		public const ushort TWON_DONTCARE16=0xffff;
		//Language and country for the application identity
		public const ushort TWLG_USA=13;
		public const ushort TWCY_USA=1;
	}
}
