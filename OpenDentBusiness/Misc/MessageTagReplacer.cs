using System;
using System.Text;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Replaces the tags in message templates that the office sends itself: manual confirmations and statement emails. These used to be
	///replaced by the tag replacers of Open Dental's hosted automated messaging (AutoComm), which was removed together with the eConnector.</summary>
	public class MessageTagReplacer {
		public const string MONTHLY_CARD_TAG="[monthlyCardsOnFile]";
		public const string NAME_PREF_TAG="[namePref]";
		public const string PATNUM_TAG="[PatNum]";
		public const string CURMONTH_TAG="[currentMonth]";
		///<summary>Tag is replaced by the current balance on the associated statement.</summary>
		public const string STATEMENT_BALANCE_TAG="[StatementBalance]";
		///<summary>Tag is replaced by the current insurance estimate on the associated statement.</summary>
		public const string STATEMENT_INS_EST_TAG="[StatementInsuranceEst]";
		public const string NAMEFL_NOPREF_TAG="[nameFLnoPref]";
		public const string NAMEF_NOPREF_TAG="[nameFnoPref]";
		///<summary>These tags were replaced by links to Open Dental's hosted patient portal and payment portal, which were removed. A template that
		///still has them gets them replaced with blank text.</summary>
		private static readonly string[] _arrayRemovedUrlTags=new string[] { "[StatementURL]","[StatementShortURL]","[MsgToPayURL]" };

		///<summary>Replaces the patient, provider, office and practice tags. A null clinic uses the practice name and phone.</summary>
		public static string ReplaceCommonTags(string template,string nameF,string namePreferredOrFirst,long provNum,Clinic clinic,bool isEmailBody) {
			if(string.IsNullOrEmpty(template)) {
				return template;
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(template);
			ReplaceCommonTags(stringBuilder,nameF,namePreferredOrFirst,provNum,clinic,isEmailBody);
			return stringBuilder.ToString();
		}

		///<summary>Replaces the tags of a statement email or text with the information from the patient and statement passed in.</summary>
		public static string ReplaceTagsForStatement(string template,Patient patient,Statement statement,bool isEmail=false) {
			if(string.IsNullOrEmpty(template)) {
				return template;
			}
			Clinic clinic=Clinics.GetClinic(patient.ClinicNum)??Clinics.GetPracticeAsClinicZero();
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(template);
			ReplaceCommonTags(stringBuilder,patient.FName,patient.GetNameFirstOrPreferred(),patient.PriProv,clinic,isEmail);
			if(stringBuilder.ToString().Contains(MONTHLY_CARD_TAG)) {
				ReplaceTags.ReplaceOneTag(stringBuilder,MONTHLY_CARD_TAG,CreditCards.GetMonthlyCardsOnFile(patient.PatNum),isEmail);
			}
			ReplaceTags.ReplaceOneTag(stringBuilder,NAME_PREF_TAG,patient.Preferred,isEmail);
			ReplaceTags.ReplaceOneTag(stringBuilder,PATNUM_TAG,patient.PatNum.ToString(),isEmail);
			ReplaceTags.ReplaceOneTag(stringBuilder,CURMONTH_TAG,DateTime_.Now.ToString("MMMM"),isEmail);
			Statement statementDb=Statements.GetStatement(statement.StatementNum);//Retrieve the statement from the db to replace balance totals.
			if(statementDb!=null) {
				ReplaceTags.ReplaceOneTag(stringBuilder,STATEMENT_BALANCE_TAG,statementDb.BalTotal.ToString("0.00"),isEmail);
				ReplaceTags.ReplaceOneTag(stringBuilder,STATEMENT_INS_EST_TAG,statementDb.InsEst.ToString("0.00"),isEmail);
			}
			for(int i=0;i<_arrayRemovedUrlTags.Length;i++) {
				ReplaceTags.ReplaceOneTag(stringBuilder,_arrayRemovedUrlTags[i],"",isEmail);
			}
			if(isEmail) {
				ReplaceTags.ReplaceOneTag(stringBuilder,NAMEFL_NOPREF_TAG,patient.GetNameFLnoPref(),isEmail);
				ReplaceTags.ReplaceOneTag(stringBuilder,NAMEF_NOPREF_TAG,patient.FName,isEmail);
			}
			return stringBuilder.ToString();
		}

		private static void ReplaceCommonTags(StringBuilder stringBuilder,string nameF,string namePreferredOrFirst,long provNum,Clinic clinic,bool isEmailBody) {
			ReplaceTags.ReplaceOneTag(stringBuilder,"[NameF]",nameF,isEmailBody);
			ReplaceTags.ReplaceOneTag(stringBuilder,"[NameFL]",nameF,isEmailBody);
			ReplaceTags.ReplaceOneTag(stringBuilder,"[NamePreferredOrFirst]",namePreferredOrFirst,isEmailBody);
			ReplaceTags.ReplaceOneTag(stringBuilder,"[ClinicName]",Clinics.GetOfficeName(clinic),isEmailBody);
			ReplaceTags.ReplaceOneTag(stringBuilder,"[ClinicPhone]",Clinics.GetOfficePhone(clinic),isEmailBody);
			ReplaceTags.ReplaceOneTag(stringBuilder,"[OfficePhone]",Clinics.GetOfficePhone(clinic),isEmailBody);
			ReplaceTags.ReplaceOneTag(stringBuilder,"[OfficeName]",Clinics.GetOfficeName(clinic),isEmailBody);
			ReplaceTags.ReplaceOneTag(stringBuilder,"[PracticeName]",PrefC.GetString(PrefName.PracticeTitle),isEmailBody);
			ReplaceTags.ReplaceOneTag(stringBuilder,"[PracticePhone]",Clinics.GetOfficePhone(null),isEmailBody);//passing in null will grab us the practice phone and format the phone number
			ReplaceTags.ReplaceOneTag(stringBuilder,"[ProvName]",Providers.GetFormalName(provNum),isEmailBody);
			ReplaceTags.ReplaceOneTag(stringBuilder,"[ProvAbbr]",Providers.GetAbbr(provNum),isEmailBody);
			long clinicNum=0;
			if(clinic!=null) {
				clinicNum=clinic.ClinicNum;
			}
			ReplaceTags.ReplaceOneTag(stringBuilder,"[EmailDisclaimer]",EmailMessages.GetEmailDisclaimer(clinicNum),isEmailBody);
		}
	}
}
