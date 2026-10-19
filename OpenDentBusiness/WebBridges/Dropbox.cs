// Dropbox A to Z image storage was removed (docs/specs/vendor-direct/dropbox.md, owner decision 2026-10-18). Local and network-share storage
// stay the default, and SFTP stays available. What is left here keeps a database that still names Dropbox working safely.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDentBusiness {
	///<summary>Dropbox A to Z storage is no longer supported. The "Dropbox" program row, its two programproperty rows and
	///DataStorageType.DropboxAtoZ stay for schema compatibility (ground rule 2). Nothing reads the stored token, which was issued to another
	///company's Dropbox app, and nothing contacts Dropbox.</summary>
	public class Dropbox {
		///<summary>Shown at startup, in Setup > Data Paths, and stored in the error of every CloudStorage task while the database still names
		///Dropbox as the A to Z storage.</summary>
		public const string StorageRemovedMessage="This database stores the A to Z images folder in Dropbox, which this program no longer supports. "
			+"Images and documents in the A to Z folder can't be opened or saved. An administrator must copy the A to Z folder to a local, "
			+"network or SFTP location and choose it in Setup > Data Paths.";

		///<summary>The names of the program property rows that the Dropbox program row still has. Kept so the rows stay recognizable.</summary>
		public class PropertyDescs {
			public static string AtoZPath="Dropbox AtoZ Path";
			///<summary>Legacy row. Never read or sent.</summary>
			public static string AccessToken="Dropbox API Token";
		}

		///<summary>True if the database still names Dropbox as the A to Z storage.</summary>
		public static bool IsStorageSelected {
			get { return PrefC.AtoZfolderUsed==DataStorageType.DropboxAtoZ; }
		}

		///<summary>The A to Z path that was stored for Dropbox, so the paths CloudStorage builds keep their old shape. Blank if the row is missing.</summary>
		public static string AtoZPath {
			get { return ProgramProperties.GetPropVal(Programs.GetProgramNum(ProgramName.Dropbox),Dropbox.PropertyDescs.AtoZPath); }
		}
	}
}
