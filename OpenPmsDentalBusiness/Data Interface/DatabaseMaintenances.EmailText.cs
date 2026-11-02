// Email text clean-up for Database Maintenance (backlog package B-DBM): the EmailmessageInvalidCharactersInBodyText check (Open Dental 25.1, also
// 24.4.42) and the Unicode white space handling of the Raw Emails tool (24.4). Character rules: OpenPmsDentalBusiness/Misc/DbmTextCleaner.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using DataConnectionBase;

namespace OpenPmsDentalBusiness {
	public partial class DatabaseMaintenances {
		///<summary>How many email messages the body text check reads and fixes at a time, so a large emailmessage table is never locked for long.
		///</summary>
		private const int EMAIL_BODY_BATCH_SIZE=100;
		///<summary>How many email messages the break down lists.</summary>
		private const int EMAIL_BODY_BREAKDOWN_MAX=100;

		///<summary>Email messages whose body text holds control characters (other than tab, CR and LF) that break display, printing or re-sending.
		///Fix removes them from emailmessage.BodyText only, in batches by EmailMessageNum. The name is Open Dental's, so the office's
		///databasemaintenance row and preferences for it carry over.</summary>
		[DbmMethodAttr(HasBreakDown=true)]
		public static string EmailmessageInvalidCharactersInBodyText(bool verbose,DbmMode dbmMode) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),verbose,dbmMode);
			}
			string where=DbmTextCleaner.GetSqlHasControlChars("BodyText");
			string log="";
			switch(dbmMode) {
				case DbmMode.Check: {
					long count=PIn.Long(Db.GetCount("SELECT COUNT(*) FROM emailmessage WHERE "+where));
					if(count>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Email messages with invalid characters in the body text:")+" "+count+"\r\n";
						if(count>0) {
							log+="   "+Lans.g("FormDatabaseMaintenance","Double click to see a break down.")+"\r\n";
						}
					}
					break;
				}
				case DbmMode.Breakdown: {
					long count=PIn.Long(Db.GetCount("SELECT COUNT(*) FROM emailmessage WHERE "+where));
					if(count==0 && !verbose) {
						break;
					}
					log+=Lans.g("FormDatabaseMaintenance","Email messages with invalid characters in the body text:")+" "+count+"\r\n";
					DataTable table=Db.GetTable("SELECT EmailMessageNum,PatNum,MsgDateTime FROM emailmessage WHERE "+where
						+" ORDER BY EmailMessageNum LIMIT "+EMAIL_BODY_BREAKDOWN_MAX);
					for(int i=0;i<table.Rows.Count;i++) {
						log+="   "+Lans.g("FormDatabaseMaintenance","EmailMessageNum")+" "+PIn.Long(table.Rows[i]["EmailMessageNum"].ToString())
							+", "+Lans.g("FormDatabaseMaintenance","PatNum")+" "+PIn.Long(table.Rows[i]["PatNum"].ToString())
							+", "+PIn.DateT(table.Rows[i]["MsgDateTime"].ToString()).ToString()+"\r\n";
					}
					if(count>table.Rows.Count) {
						log+="   "+Lans.g("FormDatabaseMaintenance","Only the first")+" "+table.Rows.Count+" "+Lans.g("FormDatabaseMaintenance","are listed.")
							+"\r\n";
					}
					break;
				}
				case DbmMode.Fix: {
					long countFixed=0;
					long emailMessageNumLast=0;
					while(true) {
						DataTable table=Db.GetTable("SELECT EmailMessageNum,BodyText FROM emailmessage "
							+"WHERE EmailMessageNum>"+POut.Long(emailMessageNumLast)+" AND "+where+" "
							+"ORDER BY EmailMessageNum LIMIT "+EMAIL_BODY_BATCH_SIZE);
						for(int i=0;i<table.Rows.Count;i++) {
							long emailMessageNum=PIn.Long(table.Rows[i]["EmailMessageNum"].ToString());
							emailMessageNumLast=emailMessageNum;
							string bodyText=PIn.String(table.Rows[i]["BodyText"].ToString());
							string bodyTextNew=DbmTextCleaner.RemoveEmailBodyInvalidChars(bodyText);
							if(bodyTextNew==bodyText) {
								continue;
							}
							UpdateTextColumn("emailmessage","BodyText","EmailMessageNum",emailMessageNum,bodyTextNew);
							countFixed++;
						}
						if(table.Rows.Count<EMAIL_BODY_BATCH_SIZE) {
							break;
						}
					}
					if(countFixed>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Email messages with invalid characters removed from the body text:")+" "+countFixed+"\r\n";
					}
					break;
				}
			}
			return log;
		}

		///<summary>Writes one text value by primary key through a query parameter, so the value is stored exactly as given. POut.String and the CRUD
		///parameters would also replace some symbols and change line breaks, which a character clean-up must not do. Table, column and key names
		///come from code, never from the user.</summary>
		private static void UpdateTextColumn(string tableName,string columnName,string keyName,long priKey,string value) {
			string command="UPDATE "+tableName+" SET "+columnName+"="+DbHelper.ParamChar+"paramValue WHERE "+keyName+"="+POut.Long(priKey);
			Db.NonQ(command,new OdSqlParameter("paramValue",OdDbType.Text,value));
		}

		///<summary>For the Raw Emails tool (Open Dental 24.4): maps unusual Unicode white space in the subject and body text of the old and the
		///re-processed copy of a message to plain spaces, and removes zero-width characters, before the two are compared and the new one is saved.
		///A message that differs only in that white space is then no longer saved again for nothing. Line breaks are left alone.</summary>
		public static void NormalizeRawEmailWhiteSpace(EmailMessage emailMessageNew,EmailMessage emailMessageOld) {
			Meth.NoCheckMiddleTierRole();
			emailMessageNew.Subject=DbmTextCleaner.NormalizeUnicodeWhiteSpace(emailMessageNew.Subject);
			emailMessageNew.BodyText=DbmTextCleaner.NormalizeUnicodeWhiteSpace(emailMessageNew.BodyText);
			emailMessageOld.Subject=DbmTextCleaner.NormalizeUnicodeWhiteSpace(emailMessageOld.Subject);
			emailMessageOld.BodyText=DbmTextCleaner.NormalizeUnicodeWhiteSpace(emailMessageOld.BodyText);
		}

		///<summary>For the Raw Emails tool: the inbox address that received the message. Unusual Unicode white space around either address is ignored
		///(INFERRED, see docs/port-notes/B-DBM.md), so such a message is processed instead of being counted as an error. Null when none matches.
		///</summary>
		public static EmailAddress FindEmailAddressForRawEmail(List<EmailAddress> listEmailAddresses,string recipientAddress) {
			Meth.NoCheckMiddleTierRole();
			string recipient=DbmTextCleaner.NormalizeUnicodeWhiteSpace(recipientAddress).Trim().ToLower();
			return listEmailAddresses.FirstOrDefault(x => DbmTextCleaner.NormalizeUnicodeWhiteSpace(x.EmailUsername).Trim().ToLower()==recipient);
		}
	}
}
