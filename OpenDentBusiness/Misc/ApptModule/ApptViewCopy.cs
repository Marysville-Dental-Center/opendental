// Package B-APPT (docs/specs/port-backlog.md), item v24_4-appts-copy-apptview: the Copy View button in Setup, Appointment Views.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Reflection;

namespace OpenDentBusiness {
	public partial class ApptViews {
		///<summary>Added to the description of a copied view. Our wording (docs/specs/features-24.4.md).</summary>
		public const string COPY_SUFFIX=" (copy)";
		///<summary>apptview.Description is varchar(255).</summary>
		private const int DESCRIPTION_MAX_LENGTH=255;

		///<summary>"Original (copy)", shortened so it fits the column.</summary>
		public static string GetCopyDescription(string description) {
			Meth.NoCheckMiddleTierRole();
			string descriptionSource=description??"";
			if(descriptionSource.Length+COPY_SUFFIX.Length>DESCRIPTION_MAX_LENGTH) {
				descriptionSource=descriptionSource.Substring(0,DESCRIPTION_MAX_LENGTH-COPY_SUFFIX.Length);
			}
			return descriptionSource+COPY_SUFFIX;
		}

		///<summary>Copies a view and all of its apptviewitem rows (operatories, providers, row and corner elements, appointment and patient field
		///elements, and the IsMobile rows), and returns the copy, or null if the source view no longer exists. Every apptview column is copied
		///except the key, so the copy keeps the clinic, rows per increment, scheduled-provider filters, stacking, scroll start, bubble setting,
		///minimum width, waiting room name format and ShowMirroredAppts. The copy is placed directly after the source in its clinic's list; the
		///views after it move down one. Users' remembered views (userodapptview) are not copied. The caller refreshes the Views cache.</summary>
		public static ApptView CopyView(long apptViewNumSource) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<ApptView>(MethodBase.GetCurrentMethod(),apptViewNumSource);
			}
			ApptView apptViewSource=Crud.ApptViewCrud.SelectOne(apptViewNumSource);
			if(apptViewSource==null) {
				return null;
			}
			string command="UPDATE apptview SET ItemOrder=ItemOrder+1 "
				+"WHERE ClinicNum="+POut.Long(apptViewSource.ClinicNum)+" "
				+"AND ItemOrder>"+POut.Int(apptViewSource.ItemOrder)+" "
				+"AND ApptViewNum!="+POut.Long(apptViewSource.ApptViewNum);
			Db.NonQ(command);
			ApptView apptViewCopy=apptViewSource.Copy();
			apptViewCopy.ApptViewNum=0;
			apptViewCopy.Description=GetCopyDescription(apptViewSource.Description);
			apptViewCopy.ItemOrder=apptViewSource.ItemOrder+1;
			Crud.ApptViewCrud.Insert(apptViewCopy);
			command="SELECT * FROM apptviewitem WHERE ApptViewNum="+POut.Long(apptViewSource.ApptViewNum)+" ORDER BY ApptViewItemNum";
			List<ApptViewItem> listApptViewItems=Crud.ApptViewItemCrud.SelectMany(command);
			for(int i=0;i<listApptViewItems.Count;i++) {
				listApptViewItems[i].ApptViewItemNum=0;
				listApptViewItems[i].ApptViewNum=apptViewCopy.ApptViewNum;
				Crud.ApptViewItemCrud.Insert(listApptViewItems[i]);
			}
			return apptViewCopy;
		}

		///<summary>The Setup security log text for a copied view.</summary>
		public static string GetCopyLogText(string descriptionSource,string descriptionCopy) {
			Meth.NoCheckMiddleTierRole();
			return "Appointment view '"+descriptionSource+"' copied to '"+descriptionCopy+"'";
		}
	}
}
