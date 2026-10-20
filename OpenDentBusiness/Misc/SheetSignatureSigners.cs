using System;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Rules for changing the signing user at a sheet signature box (Open Dental 25.1) and for recording who signed (sheetfield.UserSigned,
	///Open Dental 25.2). The switch belongs to one box while the sheet is open. Security.CurUser never changes.</summary>
	public static class SheetSignatureSigners {
		///<summary>True for the two signature box field types.</summary>
		public static bool IsSigBox(SheetField sheetField) {
			return sheetField!=null && sheetField.FieldType.In(SheetFieldType.SigBox,SheetFieldType.SigBoxPractice);
		}

		///<summary>Whether a signature box gets a Change User button: only boxes that allow electronic signing or are restricted to providers, never in
		///kiosk or terminal mode, and not on treatment plan sheets (those are signed in the Sign Treatment Plan window, which has its own button).</summary>
		public static bool CanChangeUser(SheetField sheetField,SheetTypeEnum sheetType,bool isInTerminal) {
			if(isInTerminal || sheetType==SheetTypeEnum.TreatmentPlan) {
				return false;
			}
			return IsChangeUserBox(sheetField);
		}

		///<summary>A signature box that allows electronic signing or is restricted to providers. Those are the boxes that get a Change User button,
		///in Fill Sheet and in the Sign Treatment Plan window.</summary>
		public static bool IsChangeUserBox(SheetField sheetField) {
			return IsSigBox(sheetField) && (sheetField.CanElectronicallySign || sheetField.IsSigProvRestricted);
		}

		///<summary>Whether this user may sign the box. A provider-restricted box needs a logged on user who is linked to a provider.</summary>
		public static bool CanUserSign(SheetField sheetField,Userod userod) {
			if(sheetField==null || !sheetField.IsSigProvRestricted) {
				return true;
			}
			return userod!=null && userod.UserNum>0 && userod.ProvNum>0;
		}

		///<summary>Whether the E (electronic signature) button shows for this user. Never in kiosk or terminal mode.</summary>
		public static bool CanUserESign(SheetField sheetField,Userod userod,bool isInTerminal) {
			return sheetField!=null && sheetField.CanElectronicallySign && !isInTerminal && userod!=null && userod.UserNum>0;
		}

		///<summary>The value to save in sheetfield.UserSigned. Unchanged signatures keep what they had. A new signature records the user only when
		///the box was signed after a change of user, that user is a provider (ProvNum above 0), and the box allows electronic signing or is restricted to
		///providers. Every other new or cleared signature saves 0.</summary>
		public static long GetUserSignedToSave(SheetField sheetField,bool isSigChanged,bool isSigned,Userod userodSwitched) {
			if(sheetField==null) {
				return 0;
			}
			if(!isSigChanged) {
				return sheetField.UserSigned;
			}
			if(!isSigned || userodSwitched==null || userodSwitched.ProvNum<=0 || !IsSigBox(sheetField)) {
				return 0;
			}
			if(!sheetField.CanElectronicallySign && !sheetField.IsSigProvRestricted) {
				return 0;
			}
			return userodSwitched.UserNum;
		}

		///<summary>The "By: user" caption for a signature box, or "" when UserSigned is 0. getUserName turns a UserNum into a name.</summary>
		public static string GetSignedByText(long userSigned,Func<long,string> getUserName) {
			if(userSigned<=0) {
				return "";
			}
			string name=getUserName(userSigned);
			if(string.IsNullOrEmpty(name)) {
				return "";
			}
			return Lans.g("SheetSignatureSigners","By:")+" "+name;
		}

		///<summary>SheetEdit security log text for a box signed through a change of user. Names the signer and the sheet.</summary>
		public static string GetSignedLogText(Sheet sheet,SheetField sheetField,Userod userodSigner,bool isDomainAuthenticated) {
			string text=Lans.g("SheetSignatureSigners","Sheet with ID")+" "+sheet.SheetNum+" ("+sheet.Description+"): "
				+Lans.g("SheetSignatureSigners","signature box")+" "+(string.IsNullOrEmpty(sheetField.FieldName) ? sheetField.FieldType.ToString() : sheetField.FieldName)
				+" "+Lans.g("SheetSignatureSigners","signed by")+" "+userodSigner.UserName+" "+Lans.g("SheetSignatureSigners","after a change of user");
			if(isDomainAuthenticated) {
				text+=" "+Lans.g("SheetSignatureSigners","(domain authentication)");
			}
			return text;
		}
	}
}
