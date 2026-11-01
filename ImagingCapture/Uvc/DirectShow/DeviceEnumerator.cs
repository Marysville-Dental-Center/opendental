// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImagingCapture.Uvc.Interop;

namespace ImagingCapture.Uvc.DirectShow {
	///<summary>Lists the DirectShow video input category (CLSID_VideoInputDeviceCategory) through the System Device Enumerator.
	///Every device in the category is listed; nothing is filtered or matched by name.</summary>
	internal static class DeviceEnumerator {
		private const string PropertyFriendlyName="FriendlyName";
		private const string PropertyDevicePath="DevicePath";

		///<summary>Every video capture device present. Empty when there are none.</summary>
		public static List<UvcDeviceInfo> GetDevices() {
			List<UvcDeviceInfo> listDevices=new List<UvcDeviceInfo>();
			Enumerate(moniker => {
				string[] arrayValues=DsUtil.ReadPropertyBagStrings(moniker,PropertyFriendlyName,PropertyDevicePath);
				listDevices.Add(new UvcDeviceInfo(arrayValues[0],arrayValues[1],DsUtil.GetDisplayName(moniker)));
				return false;
			});
			return listDevices;
		}

		///<summary>The moniker of the device with the same moniker display name, or else the same device path. Null if it is not
		///present. The caller releases it.</summary>
		public static IMoniker FindMoniker(UvcDeviceInfo device) {
			IMoniker monikerFound=null;
			if(device.MonikerDisplayName!="") {
				Enumerate(moniker => {
					if(!string.Equals(DsUtil.GetDisplayName(moniker),device.MonikerDisplayName,StringComparison.OrdinalIgnoreCase)) {
						return false;
					}
					monikerFound=moniker;
					return true;
				});
			}
			if(monikerFound==null && device.DevicePath!="") {
				Enumerate(moniker => {
					string devicePath=DsUtil.ReadPropertyBagStrings(moniker,PropertyDevicePath)[0];
					if(!string.Equals(devicePath,device.DevicePath,StringComparison.OrdinalIgnoreCase)) {
						return false;
					}
					monikerFound=moniker;
					return true;
				});
			}
			return monikerFound;
		}

		///<summary>Calls funcKeep for each moniker in the category until it returns true. A moniker for which it returns false is
		///released; the one it keeps belongs to the caller.</summary>
		private static void Enumerate(Func<IMoniker,bool> funcKeep) {
			object objDevEnum=null;
			IEnumMoniker enumMoniker=null;
			try {
				objDevEnum=DsUtil.CreateComObject(Ds.CLSID_SystemDeviceEnum);
				ICreateDevEnum createDevEnum=objDevEnum as ICreateDevEnum;
				if(createDevEnum==null) {
					throw new UvcException(UvcErrorKind.NotAvailable,"The System Device Enumerator does not implement ICreateDevEnum.");
				}
				Guid guidCategory=Ds.CLSID_VideoInputDeviceCategory;
				int hr=createDevEnum.CreateClassEnumerator(ref guidCategory,out enumMoniker,0);
				if(hr<0) {
					throw new UvcException(UvcErrors.Classify(hr),"Listing video devices failed ("+UvcErrors.FormatHResult(hr)+").",hr);
				}
				if(hr!=Ds.S_OK || enumMoniker==null) {
					return;//S_FALSE: the category is empty
				}
				IMoniker[] arrayMonikers=new IMoniker[1];
				while(enumMoniker.Next(1,arrayMonikers,IntPtr.Zero)==Ds.S_OK) {
					IMoniker moniker=arrayMonikers[0];
					arrayMonikers[0]=null;
					if(moniker==null) {
						break;
					}
					bool isKept=false;
					try {
						isKept=funcKeep(moniker);
					}
					finally {
						if(!isKept) {
							DsUtil.Release(ref moniker);
						}
					}
					if(isKept) {
						return;
					}
				}
			}
			catch(COMException ex) {
				UvcErrorKind kind=ex.ErrorCode==Ds.REGDB_E_CLASSNOTREG ? UvcErrorKind.NotAvailable : UvcErrorKind.Failed;
				throw new UvcException(kind,"Listing video devices failed ("+UvcErrors.FormatHResult(ex.ErrorCode)+").",ex.ErrorCode);
			}
			finally {
				DsUtil.Release(ref enumMoniker);
				DsUtil.Release(ref objDevEnum);
			}
		}
	}
}
