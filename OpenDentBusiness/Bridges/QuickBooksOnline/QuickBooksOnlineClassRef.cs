// QuickBooks Online (direct): a class picked for deposits. Replaces the type of the same name in the prebuilt Bridges.dll, re-implemented from
// how this program calls it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness.Qbo {
	///<summary>A QuickBooks Online class. Name is the fully qualified name for classes loaded from QuickBooks Online. Equality is by reference.</summary>
	public class QuickBooksOnlineClassRef {
		///<summary>The name shown and stored. Names containing ',' or '|' can't be stored.</summary>
		public string Name { get; set; }
		///<summary>The QuickBooks Online Id.</summary>
		public string Id { get; set; }
		///<summary>"Parent:Child" from QuickBooks Online, or the stored name for classes read from the program property.</summary>
		public string FullyQualifiedName { get; set; }
		///<summary>Set on the single element of a list returned for a failed request.</summary>
		public Exception RequestException { get; internal set; }

		public QuickBooksOnlineClassRef(string name,string id) {
			Name=name??"";
			Id=id??"";
			FullyQualifiedName=Name;
		}

		///<summary>Parses "Name,Id|Name,Id". A blank string gives an empty list.</summary>
		public static List<QuickBooksOnlineClassRef> StringClassRefsToList(string classRefs) {
			return QboEntityList.Parse(classRefs).Select(x => new QuickBooksOnlineClassRef(x.Key,x.Value)).ToList();
		}

		///<summary>Formats the list as "Name,Id|Name,Id".</summary>
		public static string ListClassRefsToString(List<QuickBooksOnlineClassRef> listClassRefs) {
			if(listClassRefs==null) {
				return "";
			}
			return QboEntityList.Format(listClassRefs.Select(x => new KeyValuePair<string,string>(x.Name,x.Id)));
		}

		internal static List<QuickBooksOnlineClassRef> FromException(Exception ex) {
			QuickBooksOnlineClassRef classRef=new QuickBooksOnlineClassRef("","");
			classRef.RequestException=ex;
			return new List<QuickBooksOnlineClassRef> { classRef };
		}

		public override string ToString() {
			return Name;
		}
	}
}
