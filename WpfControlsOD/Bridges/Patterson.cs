using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeBase;
using OpenPmsDentalBusiness;
using System.Windows.Forms;



namespace OpenPmsDental.Bridges {
	///<summary>Provides bridging functionality to Patterson Imaging.</summary>
	class Patterson {

		///<summary>Default Constructor</summary>
		public Patterson() { 
		}

		///<summary>Open Dental wrote the patient and provider for Patterson Imaging and started it through its closed VBbridges.dll, which this
		///program does not ship (removed 2026-11-01). Patterson publishes no description of that hand-off (the ini file's sections and keys), so this
		///says the action is not available instead of guessing. The field limits Open Dental used are kept in Tidy and LTidy for when the format is
		///known.</summary>
		public static void SendData(Program ProgramCur,Patient pat) {
			if(pat==null) {
				return;
			}
			MsgBox.Show("Patterson",BridgeNotAvailable.Patterson);
		}

		///<summary>Will only return the beginning amount of characters based on maximum value.</summary>
		private static string Tidy(string str,int maxL) {
			str.Trim();
			if(str.Length>maxL) {
				str=str.Substring(0,maxL);
			}
			return str.Trim();
		}

		///<summary>Will only return the last amount of characters based on maximum value.</summary>
		private static string LTidy(string str,int maxL) {
		  if(str.Length>maxL) {
		    str=str.Substring(str.Length-maxL);
		  }
		  return str.Trim();
		}

		/////<summary>Appended with whitespace and returns FIRST maxL characters. Accepts null to return string full of spaces.</summary>
		//private static string Tidy(string str,int maxL) {
		//  if(str==null){
		//    str="";
		//  }
		//  str=str.Trim().PadRight(maxL,' ');
		//  return str.Substring(0,maxL);
		//}

		/////<summary>PREpended with whitespace and returns LAST maxL characters. Accepts null to return string full of spaces.</summary>
		//private static string LTidy(string str,int maxL) {
		//  if(str==null) {
		//    str="";
		//  }
		//  str=str.Trim().PadLeft(maxL,' ');
		//  return str.Substring(str.Length-maxL,maxL);
		//}


	}
}
