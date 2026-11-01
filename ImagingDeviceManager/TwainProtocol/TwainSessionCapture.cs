// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ImagingDeviceManager.TwainProtocol {
	//The capture path of design 7.1 CAP-3, used by TwainCaptureSession on its worker thread: MSG_GET and MSG_QUERYSUPPORT for the
	//capabilities the capture session reads, and MSG_SET for the custom capabilities named in the device profile.
	internal sealed partial class TwainSession {
		///<summary>MSG_QUERYSUPPORT answers of the open source, by capability: the TWQC_ bits, or null when the source did not answer (the
		///condition code is in _dictQuerySupportCondition). Capabilities return to their defaults only at MSG_OPENDS (twain-contract 1.1
		///section 4), and what a source supports does not change while it is open, so each is asked once per open source.</summary>
		private readonly Dictionary<ushort,int?> _dictQuerySupport=new Dictionary<ushort,int?>();
		private readonly Dictionary<ushort,ushort> _dictQuerySupportCondition=new Dictionary<ushort,ushort>();

		#region Capabilities
		///<summary>Forgets what was learnt about the open source. Called whenever a source is opened or closed.</summary>
		private void ResetCaptureState() {
			_dictQuerySupport.Clear();
			_dictQuerySupportCondition.Clear();
		}

		///<summary>A DAT_CAPABILITY message that returns a container (MSG_GET, MSG_GETCURRENT, MSG_GETDEFAULT), with every item read. False
		///when the source does not answer (LastConditionCode says why) or the container cannot be read. The container handle is freed like
		///every handle from the source (ITwainMemory).</summary>
		internal bool TryGetCapabilityValues(ushort capability,ushort msg,out TwainCapabilityValues values) {
			values=null;
			if(State<4 || _memory==null) {
				return false;
			}
			IntPtr pCapability=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_CAPABILITY)));
			IntPtr hContainer=IntPtr.Zero;
			try {
				TW_CAPABILITY twCapability=new TW_CAPABILITY { Cap=capability,ConType=Tw.TWON_DONTCARE16,hContainer=IntPtr.Zero };
				Marshal.StructureToPtr(twCapability,pCapability,false);
				if(SourceEntry(Tw.DG_CONTROL,Tw.DAT_CAPABILITY,msg,pCapability)!=Tw.TWRC_SUCCESS) {
					return false;
				}
				twCapability=(TW_CAPABILITY)Marshal.PtrToStructure(pCapability,typeof(TW_CAPABILITY));
				hContainer=twCapability.hContainer;
				if(hContainer==IntPtr.Zero) {
					return false;
				}
				IntPtr pContainer=_memory.Lock(hContainer);
				if(pContainer==IntPtr.Zero) {
					return false;
				}
				try {
					values=ReadContainer(pContainer,twCapability.ConType,_memory.GetSize(hContainer));
					return values!=null;
				}
				finally {
					_memory.Unlock(hContainer);
				}
			}
			catch(Exception ex) {
				Log("Capability "+ToHex(capability)+" of \""+SourceName+"\" could not be read: "+ex.Message);
				values=null;
				return false;
			}
			finally {
				Marshal.FreeHGlobal(pCapability);
				if(hContainer!=IntPtr.Zero) {
					_memory.Free(hContainer);
				}
			}
		}

		///<summary>Reads a container. sizeBlock bounds an enumeration's or array's item list when the allocator knows the block size (0:
		///unknown); a list is never read past 1024 items either way. Null for a container type this library does not read.</summary>
		internal static TwainCapabilityValues ReadContainer(IntPtr pContainer,ushort conType,long sizeBlock) {
			const int maxItems=1024;
			TwainCapabilityValues values=new TwainCapabilityValues { ConType=conType };
			values.ItemType=(ushort)Marshal.ReadInt16(pContainer,0);
			int itemSize=TwContainer.ItemSize(values.ItemType);
			bool isNumeric=itemSize>=1 && itemSize<=4;
			switch(conType) {
				case Tw.TWON_ONEVALUE:
					if(isNumeric) {
						values.ListItems.Add(ReadItem(pContainer,TwContainer.OneValueItemOffset,values.ItemType,itemSize));
					}
					return values;
				case Tw.TWON_RANGE:
					values.RangeMin=Marshal.ReadInt32(pContainer,TwContainer.RangeMinValueOffset);
					values.RangeMax=Marshal.ReadInt32(pContainer,TwContainer.RangeMaxValueOffset);
					values.RangeStep=Marshal.ReadInt32(pContainer,TwContainer.RangeStepSizeOffset);
					values.RangeDefault=Marshal.ReadInt32(pContainer,TwContainer.RangeDefaultValueOffset);
					values.RangeCurrent=Marshal.ReadInt32(pContainer,TwContainer.RangeCurrentValueOffset);
					return values;
				case Tw.TWON_ENUMERATION:
				case Tw.TWON_ARRAY:
					bool isEnumeration=conType==Tw.TWON_ENUMERATION;
					int numItems=Marshal.ReadInt32(pContainer,isEnumeration ? TwContainer.EnumNumItemsOffset : TwContainer.ArrayNumItemsOffset);
					int offsetList=isEnumeration ? TwContainer.EnumItemListOffset : TwContainer.ArrayItemListOffset;
					if(isEnumeration) {
						values.CurrentIndex=Marshal.ReadInt32(pContainer,TwContainer.EnumCurrentIndexOffset);
						values.DefaultIndex=Marshal.ReadInt32(pContainer,TwContainer.EnumDefaultIndexOffset);
					}
					if(!isNumeric || numItems<=0) {
						return values;
					}
					numItems=Math.Min(numItems,maxItems);
					if(sizeBlock>0) {
						numItems=(int)Math.Min(numItems,Math.Max(0,(sizeBlock-offsetList)/itemSize));
					}
					for(int i=0;i<numItems;i++) {
						values.ListItems.Add(ReadItem(pContainer,offsetList+i*itemSize,values.ItemType,itemSize));
					}
					return values;
				default:
					return null;
			}
		}

		///<summary>MSG_GET, then MSG_GETCURRENT for a source that answers only that, read as one value: what the capture session reads of
		///the fixed capabilities (CAP_INDICATORS, ICAP_PIXELFLAVOR, ICAP_BITORDER and the others), which it never sets.</summary>
		internal bool TryGetCapabilityValue(ushort capability,out int value) {
			return TryGetCapability(capability,Tw.MSG_GET,out value) || TryGetCapabilityCurrent(capability,out value);
		}

		///<summary>MSG_QUERYSUPPORT, asked once per open source (_dictQuerySupport). Null when the source did not answer; conditionCode is then
		///its condition code (TWCC_CAPUNSUPPORTED for a capability it does not have), otherwise TWCC_SUCCESS.</summary>
		internal int? GetQuerySupport(ushort capability,out ushort conditionCode) {
			conditionCode=Tw.TWCC_SUCCESS;
			int? support;
			if(_dictQuerySupport.TryGetValue(capability,out support)) {
				if(support==null) {
					_dictQuerySupportCondition.TryGetValue(capability,out conditionCode);
				}
				return support;
			}
			int value;
			if(TryQuerySupport(capability,out value)) {
				support=value;
			}
			else {
				support=null;
				conditionCode=LastConditionCode;
				_dictQuerySupportCondition[capability]=conditionCode;
			}
			if(State>=4) {
				_dictQuerySupport[capability]=support;
			}
			return support;
		}

		///<summary>A custom TWTY_BOOL capability named in the device profile (design 7.1 CAP-3 and 9.3): MSG_QUERYSUPPORT first, then MSG_SET in
		///state 4. TWCC_CAPUNSUPPORTED (from a source that does not have it, such as a twain-contract 1.0 build) is logged and the feature counts
		///as absent. A source that does not answer MSG_QUERYSUPPORT (older than TWAIN 1.6) is left to MSG_SET. key names the capture.ini key,
		///for the log. Only TWRC_SUCCESS counts as set.</summary>
		internal TwainCustomCapOutcome SetCustomCapability(ushort capability,bool value,string key) {
			string what=key+" capability "+ToHex(capability)+" ("+(value ? "TRUE" : "FALSE")+")";
			if(State!=4) {
				Log("Not set: "+what+" for \""+SourceName+"\": the source is not in state 4.");
				return TwainCustomCapOutcome.NotSent;
			}
			ushort conditionQuery;
			int? support=GetQuerySupport(capability,out conditionQuery);
			if(support==null && conditionQuery==Tw.TWCC_CAPUNSUPPORTED) {
				Log("\""+SourceName+"\" does not support the "+what+" (TWCC_CAPUNSUPPORTED); the feature counts as absent.");
				return TwainCustomCapOutcome.Unsupported;
			}
			if(support!=null && (support.Value & Tw.TWQC_SET)==0) {
				Log("\""+SourceName+"\" does not allow setting the "+what+" (MSG_QUERYSUPPORT "+ToHex(support.Value)+"); the feature counts as absent.");
				return TwainCustomCapOutcome.NotSettable;
			}
			ushort? rc=SendCapabilitySet(capability,Tw.TWTY_BOOL,value ? 1 : 0);
			if(rc==null) {
				Log("Not set: "+what+" for \""+SourceName+"\": out of memory.");
				return TwainCustomCapOutcome.NotSent;
			}
			if(rc==Tw.TWRC_SUCCESS) {
				return TwainCustomCapOutcome.Set;
			}
			if(rc==Tw.TWRC_FAILURE && LastConditionCode==Tw.TWCC_CAPUNSUPPORTED) {
				Log("\""+SourceName+"\" does not support the "+what+" (TWCC_CAPUNSUPPORTED to MSG_SET); the feature counts as absent.");
				return TwainCustomCapOutcome.Unsupported;
			}
			Log("\""+SourceName+"\" did not accept the "+what+" ("+(rc==Tw.TWRC_FAILURE ? "condition code "+ToHex(LastConditionCode)
				: "return code "+rc)+"); the feature counts as absent.");
			return TwainCustomCapOutcome.Refused;
		}

		///<summary>The TW_IDENTITY of the open source, or null below state 4.</summary>
		internal TW_IDENTITY? GetOpenSourceIdentity() {
			if(State<4 || _pSourceIdentity==IntPtr.Zero) {
				return null;
			}
			return ReadIdentity(_pSourceIdentity);
		}

		///<summary>The library name of the loaded DSM, or "".</summary>
		internal string DsmLibraryName => _dsm==null ? "" : (_dsm.LibraryName??"");
		#endregion Capabilities
	}
}
