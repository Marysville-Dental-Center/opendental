// HTML dunning and billing e-mails (Open Dental 26.1, item v26_1-manage-dunning-html; package M2-DUN in docs/specs/port-backlog.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Statement e-mails whose body is HTML. The body of a statement e-mail comes from the dunning message's e-mail override when one
	///applies, otherwise from Billing Defaults, and each of the two carries its own EmailType: Regular (plain text, the behavior before 26.1),
	///Html (the e-mail markup used by HTML e-mail templates, placed inside the e-mail master template) or RawHtml (a whole HTML document, used as
	///written). The type travels with the body. The HTML e-mail is built the way the e-mail window builds one from an HTML template: BodyText keeps
	///the markup, HtmlText holds the HTML that is sent, and HtmlType says which.</summary>
	public class BillingEmailHtml {
		///<summary>The e-mail type of the Billing Defaults e-mail body (preference BillingEmailType). Regular when the preference is missing or holds a
		///number that is not an EmailType.</summary>
		public static EmailType GetBillingDefaultsEmailType() {
			Meth.NoCheckMiddleTierRole();
			if(!PrefC.ContainsKey(nameof(PrefName.BillingEmailType))) {
				return EmailType.Regular;
			}
			return ToEmailType(PrefC.GetInt(PrefName.BillingEmailType));
		}

		///<summary>The e-mail type of the Billing Defaults body that a patient of this clinic gets: the clinic's own type while clinics are on and the
		///clinic has its own Billing Defaults (Open Dental 26.2, P262-BILLING), otherwise GetBillingDefaultsEmailType().</summary>
		public static EmailType GetBillingDefaultsEmailType(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			long clinicNumSettings=BillingDefaultsByClinic.GetSettingsClinicNum(clinicNum);
			if(clinicNumSettings<=0) {
				return GetBillingDefaultsEmailType();
			}
			return BillingDefaultsByClinic.GetEmailType(clinicNumSettings);
		}

		///<summary>Regular for any number that is not an EmailType, so an unexpected stored value falls back to the plain-text behavior.</summary>
		public static EmailType ToEmailType(int value) {
			Meth.NoCheckMiddleTierRole();
			if(Enum.IsDefined(typeof(EmailType),value)) {
				return (EmailType)value;
			}
			return EmailType.Regular;
		}

		///<summary>The e-mail type of the body that Statements.GetEmailMessageForStatement uses for this statement. A statement without its own
		///e-mail body uses the Billing Defaults body, so it gets the Billing Defaults type. A statement with its own body got it from a dunning
		///message when it was created (Billing Options or automated billing), so it gets the type of that dunning message.</summary>
		public static EmailType GetEmailTypeForStatement(Statement statement,Patient patient) {
			Meth.NoCheckMiddleTierRole();
			long clinicNum=0;
			if(patient!=null) {
				clinicNum=patient.ClinicNum;
			}
			if(statement==null || string.IsNullOrEmpty(statement.EmailBody)) {
				return GetBillingDefaultsEmailType(clinicNum);
			}
			return FindDunningEmailType(statement.EmailSubject,statement.EmailBody,clinicNum,Dunnings.Refresh(null));
		}

		///<summary>The statement table has no e-mail type column (Open Dental 26.1 added one only to dunning), so the type of a statement's own body
		///is found again from the dunning message it was copied from: the dunning whose e-mail body is the same text. When several match, the one
		///with the same subject is preferred, then the one for the patient's clinic, then the one for all clinics; a remaining tie goes to the last
		///in the list, as billing picks the last matching dunning. Regular when none matches, for example when the dunning message was changed or
		///deleted after the statement was made.</summary>
		public static EmailType FindDunningEmailType(string emailSubject,string emailBody,long clinicNum,List<Dunning> listDunnings) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(emailBody) || listDunnings==null) {
				return EmailType.Regular;
			}
			Dunning dunningBest=null;
			int scoreBest=-1;
			for(int i=0;i<listDunnings.Count;i++) {
				Dunning dunning=listDunnings[i];
				if(dunning==null || (dunning.EmailBody??"")!=emailBody) {
					continue;
				}
				int score=0;
				if((dunning.EmailSubject??"")==(emailSubject??"")) {
					score+=4;
				}
				if(dunning.ClinicNum==clinicNum) {
					score+=2;
				}
				else if(dunning.ClinicNum==-2) {//All clinics
					score+=1;
				}
				if(score>=scoreBest) {
					scoreBest=score;
					dunningBest=dunning;
				}
			}
			if(dunningBest==null) {
				return EmailType.Regular;
			}
			return ToEmailType((int)dunningBest.EmailType);
		}

		///<summary>Makes the e-mail an HTML e-mail of the given type. BodyText must already hold the body with its tags replaced for an e-mail (the
		///replacement values escaped with the markup escapes "&amp;&lt;" and "&amp;&gt;"). Regular leaves the e-mail as it is. Html translates the
		///markup and places it in the e-mail master template. RawHtml uses the body as written. A markup body that cannot be translated (it is
		///checked when it is saved, so this should not happen) is sent as plain text, as before 26.1, rather than not at all.</summary>
		public static void ApplyEmailType(EmailMessage emailMessage,EmailType emailType) {
			Meth.NoCheckMiddleTierRole();
			if(emailMessage==null) {
				return;
			}
			string bodyText=emailMessage.BodyText??"";
			if(emailType==EmailType.Html) {
				string htmlText;
				try {
					htmlText=MarkupEdit.TranslateToXhtml(bodyText,isPreviewOnly:false,hasWikiPageTitles:false,isEmail:true);
				}
				catch(Exception ex) {
					ex.DoNothing();
					return;
				}
				emailMessage.HtmlText=htmlText;
				emailMessage.HtmlType=EmailType.Html;
				emailMessage.AreImagesDownloaded=true;
				return;
			}
			if(emailType==EmailType.RawHtml) {
				emailMessage.BodyText=ConvertMarkupEscapesToHtml(bodyText);
				emailMessage.HtmlText=emailMessage.BodyText;
				emailMessage.HtmlType=EmailType.RawHtml;
				emailMessage.AreImagesDownloaded=true;
			}
		}

		///<summary>Tag replacement for e-mails escapes "&lt;" and "&gt;" in the replacement values as "&amp;&lt;" and "&amp;&gt;", which the markup
		///translation turns into HTML entities. A raw HTML body is not translated, so the escapes are turned into the entities here.</summary>
		public static string ConvertMarkupEscapesToHtml(string text) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(text)) {
				return text;
			}
			return text.Replace("&<","&lt;").Replace("&>","&gt;");
		}

		///<summary>Adds the sender's autograph to an HTML statement e-mail. Returns false, changing nothing, for a plain-text e-mail, which billing
		///handles as before. Html: the autograph is added to the markup (a plain-text autograph with its "&lt;" and "&gt;" escaped) and the e-mail is
		///translated again; if the result cannot be translated, the e-mail goes without the autograph. RawHtml: the autograph is added as HTML before
		///the closing body tag, or at the end when there is none.</summary>
		public static bool TryInsertAutograph(EmailMessage emailMessage,EmailAutograph emailAutograph) {
			Meth.NoCheckMiddleTierRole();
			if(emailMessage==null || !EmailMessages.IsHtmlEmail(emailMessage.HtmlType)) {
				return false;
			}
			if(emailAutograph==null || string.IsNullOrWhiteSpace(emailAutograph.AutographText)) {
				return true;
			}
			bool isAutographHtml=EmailAutographs.IsAutographHTML(emailAutograph.AutographText);
			if(emailMessage.HtmlType==EmailType.Html) {
				EmailAutograph emailAutographMarkup=emailAutograph.Copy();
				if(!isAutographHtml) {
					emailAutographMarkup.AutographText=emailAutograph.AutographText.Replace("<","&<").Replace(">","&>");
				}
				string bodyText=EmailMessages.InsertAutograph(emailMessage.BodyText??"",emailAutographMarkup);
				try {
					emailMessage.HtmlText=MarkupEdit.TranslateToXhtml(bodyText,isPreviewOnly:false,hasWikiPageTitles:false,isEmail:true);
					emailMessage.BodyText=bodyText;
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
				return true;
			}
			string autographHtml=GetAutographHtml(emailAutograph.AutographText,isAutographHtml);
			emailMessage.BodyText=InsertBeforeClosingBody(emailMessage.BodyText??"","<br>\r\n"+autographHtml);
			emailMessage.HtmlText=emailMessage.BodyText;
			return true;
		}

		///<summary>The autograph as an HTML fragment for a raw HTML e-mail. An HTML autograph (HTML tags or image links) is translated like the markup
		///of an HTML e-mail, without the master template; plain text is encoded and keeps its line breaks.</summary>
		public static string GetAutographHtml(string autographText,bool isAutographHtml) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(autographText)) {
				return "";
			}
			if(isAutographHtml) {
				try {
					string html=MarkupEdit.TranslateToXhtml(autographText,isPreviewOnly:false,hasWikiPageTitles:false,isEmail:true,canAggregate:false);
					return Regex.Replace(html,@"^\s*<body>|</body>\s*$","").Trim();
				}
				catch(Exception ex) {
					ex.DoNothing();
					return autographText;
				}
			}
			string encoded=WebUtility.HtmlEncode(autographText);
			return encoded.Replace("\r\n","\n").Replace("\r","\n").Replace("\n","<br>\r\n");
		}

		///<summary>Inserts the fragment before the last closing body tag (any case), or appends it when the document has none.</summary>
		public static string InsertBeforeClosingBody(string html,string fragment) {
			Meth.NoCheckMiddleTierRole();
			html=html??"";
			int index=html.LastIndexOf("</body",StringComparison.OrdinalIgnoreCase);
			if(index<0) {
				return html+fragment;
			}
			return html.Substring(0,index)+fragment+"\r\n"+html.Substring(index);
		}

		///<summary>Blank when the body can be sent as the given type; otherwise why not. Only an Html body is checked: it must be valid e-mail markup,
		///which is what the HTML editor checks before it saves. Raw HTML is the user's own responsibility, as in the HTML editor.</summary>
		public static string GetBodyError(string bodyText,EmailType emailType) {
			Meth.NoCheckMiddleTierRole();
			if(emailType!=EmailType.Html || string.IsNullOrEmpty(bodyText)) {
				return "";
			}
			try {
				MarkupEdit.TranslateToXhtml(bodyText,isPreviewOnly:false,hasWikiPageTitles:false,isEmail:true,canAggregate:false);
			}
			catch(Exception ex) {
				return ex.Message;
			}
			return "";
		}
	}
}
