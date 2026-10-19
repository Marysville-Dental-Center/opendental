using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace OpenDentBusiness {
	///<summary>This table is the link between eServices and procedure codes.
	///Users will not be able to manually add any of these procedure codes as repeating charges.  They are handled by the sign up portal.</summary>
	[Serializable]
	[CrudTable(IsMissingInGeneral=true)]
	public class EServiceCodeLink:TableBase {
		///<summary>Primary key</summary>
		[CrudColumn(IsPriKey=true)]
		public long EServiceCodeLinkNum;
		///<summary>FK to procedurecode.CodeNum</summary>
		public long CodeNum;
		///<summary>Enum:eServiceCode Service which the corresponding procedure code (CodeNum) applies to.</summary>
		public eServiceCode EService;

		[XmlIgnore]
		[CrudColumn(IsNotDbColumn=true)]
		public string ProcCode;

		public EServiceCodeLink Copy() {
			return (EServiceCodeLink)this.MemberwiseClone();
		}

		/* The following queries were ran when we upgraded HQ to v17.1.1
			CREATE TABLE eservicecodelink (
				EServiceCodeLinkNum bigint NOT NULL auto_increment PRIMARY KEY,
				CodeNum bigint NOT NULL,
				EService tinyint NOT NULL,
				INDEX(CodeNum)
				) DEFAULT CHARSET=utf8;


			INSERT INTO eservicecodelink (EService,CodeNum) 
			VALUES(4,699)  -- Mobile Web							ProcCode "027"
					 ,(5,771)  -- Patient Portal					ProcCode "033"
					 ,(6,824)  -- Web Sched Recalls				ProcCode "037"
					 ,(7,823)  -- Web Forms								ProcCode "036"
					 ,(10,888) -- ConfirmationRequest			ProcCode "040"
					 ,(13,889) -- Web Sched New Patient		ProcCode "041"
					 ,(15,892) -- Bundle									ProcCode "042"
					 ,(2,881)  -- Text Message Access			ProcCode "038"
					 ,(16,882) -- Text Message Usaged			ProcCode "039"
					 ,(17,705) -- Software Only						ProcCode "030"
			;
		 */

	}
}
