// Vendor-direct integrations: the startup check of the Dropbox A to Z folder, moved out of FormOpenDental unchanged.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental.VendorDirect {
	///<summary>Owned by the DROPBOX package (docs/specs/vendor-direct/README.md, 3.2). The foundation package moved this block here from
	///FormOpenDental's startup without changing what it does, so that package can change or remove it without editing FormOpenDental.</summary>
	public static class DropboxStartupCheck {
		///<summary>Checks that the Dropbox A to Z folder is reachable. Returns false if not, and sets storageErrMsg to the message to show.</summary>
		public static bool Run(out string storageErrMsg) {
			bool hasConnected=true;
			storageErrMsg="The Dropbox data path for your A to Z Images Folder is not available and this may cause functionality issues or slowness " +
				"as long as it is not available.";
			Program program=Programs.GetCur(ProgramName.Dropbox);
			if(program==null) {//Should never happen.
				hasConnected=false;//If an error occurs, DataStorageType does not connect
			}
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetForProgram(program.ProgramNum);
			ProgramProperty programPropertyPath=listProgramProperties.Find(x => x.PropertyDesc==Dropbox.PropertyDescs.AtoZPath);
			ProgramProperty programPropertyAccessToken=listProgramProperties.Find(x => x.PropertyDesc==Dropbox.PropertyDescs.AccessToken);
			if(programPropertyPath==null || programPropertyAccessToken==null) {
				hasConnected=false;//If an error occurs, DataStorageType does not connect
			}
			else {
				try {
					hasConnected=OpenDentalCloud.Dropbox.FileExists(programPropertyAccessToken.PropertyValue,
						ODFileUtils.CombinePaths(programPropertyPath.PropertyValue,"A",'/'));
				}
				catch(Exception) {
					hasConnected=false;//If an error occurs, DataStorageType does not connect
				}
			}
			return hasConnected;
		}
	}
}
