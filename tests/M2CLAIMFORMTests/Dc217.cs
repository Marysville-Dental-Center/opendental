// Shared helpers for the Denti-Cal DC217_2019 claim form tests (backlog package M2-CLAIMFORM): the form as shipped, the claim form field list and
// a window-less claim printout.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using ForkMigrationTests;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace M2CLAIMFORMTests {
	public static class Dc217 {
		public const string RelativePath=@"OpenPmsDentalBusiness\Resources\ClaimForms\ClaimFormDenti-Cal DC217_2019.xml";
		///<summary>The service line count of the DC-217.</summary>
		public const int LINE_COUNT=15;
		///<summary>The fields of one service line, left to right: tooth or letter/arch/quad, surface, description, date of service, procedure
		///number, fee, rendering provider NPI.</summary>
		public static readonly string[] ArrayLineFields=new[] { "ToothNumOrArea","Surface","Description","Date","Code","Fee","TreatProvNPI" };

		///<summary>The form as the XML file in the repository holds it.</summary>
		public static ClaimForm FromFile() {
			return ClaimForms.DeserializeClaimForm(TestDb.FindRepoFile(RelativePath),"");
		}

		///<summary>The form as OpenPmsDentalBusiness ships it (embedded resource).</summary>
		public static ClaimForm FromResource() {
			return ClaimForms.GetInternalClaim(ClaimForms.DescriptionDentiCalDC217_2019);
		}

		///<summary>Field, format, position and size of every item, in order, for comparing two copies of the form.</summary>
		public static List<string> Describe(ClaimForm claimForm) {
			return claimForm.Items.Select(x => x.FieldName+"|"+x.ImageFileName+"|"+x.FormatString+"|"+x.XPos+","+x.YPos+","+x.Width+","+x.Height)
				.ToList();
		}

		///<summary>The item of a field that occurs once.</summary>
		public static ClaimFormItem Item(ClaimForm claimForm,string fieldName) {
			return claimForm.Items.Single(x => x.FieldName==fieldName);
		}

		///<summary>The field names the claim form editor offers (FormClaimFormItemEdit), as a US workstation lists them. The window is never
		///created: the list is filled on an uninitialized instance.</summary>
		public static HashSet<string> GetClaimFormFieldNames() {
			FormClaimFormItemEdit form=(FormClaimFormItemEdit)FormatterServices.GetUninitializedObject(typeof(FormClaimFormItemEdit));
			GC.SuppressFinalize(form);
			form.FillFieldNames();
			return new HashSet<string>(form.FieldNames);
		}

		///<summary>Fields that FormClaimPrint right-aligns at XPos (pd2_PrintPage): the fee fields of this form.</summary>
		public static bool IsRightAligned(ClaimFormItem item) {
			return item.FieldName=="TotalFee" || (item.FieldName.StartsWith("P") && item.FieldName.EndsWith("Fee"));
		}
	}

	///<summary>Runs the claim printout (FormClaimPrint) without a window: the same methods pd2_PrintPage calls for each page, on an uninitialized
	///instance, so no control is created and nothing is printed. Reads the strings each item would print.</summary>
	public class ClaimPrintHarness {
		private readonly FormClaimPrint _form;

		public ClaimPrintHarness(ClaimForm claimForm,long claimNum=0,long patNum=0) {
			_form=(FormClaimPrint)FormatterServices.GetUninitializedObject(typeof(FormClaimPrint));
			GC.SuppressFinalize(_form);
			_form.ClaimFormCur=claimForm;
			_form.ClaimNum=claimNum;
			_form.PatNum=patNum;
		}

		///<summary>The number of procedure lines the printout puts on one form.</summary>
		public int ProcLimit() {
			return (int)Invoke("ProcLimitForFormat");
		}

		///<summary>Loads the claim, as the first step of every printed page.</summary>
		public bool FillDisplayStrings() {
			return (bool)Invoke("TryFillDisplayStrings",false);
		}

		///<summary>Fills the procedure lines of the 0-based page and returns what every item prints, by field name (a field that occurs twice keeps
		///its first value). Call FillDisplayStrings first.</summary>
		public Dictionary<string,string> FillPage(int page) {
			int limit=ProcLimit();
			Invoke("FillProcStrings",page*limit,limit);
			string[] arrayDisplay=(string[])typeof(FormClaimPrint).GetField("_stringArrayDisplay",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_form);
			Dictionary<string,string> dict=new Dictionary<string,string>();
			for(int i=0;i<_form.ClaimFormCur.Items.Count;i++) {
				string fieldName=_form.ClaimFormCur.Items[i].FieldName;
				if(!dict.ContainsKey(fieldName)) {
					dict[fieldName]=arrayDisplay[i]??"";
				}
			}
			return dict;
		}

		///<summary>Pages the printout makes for this many procedures, as pd2_PrintPage counts them.</summary>
		public int PageCount(int procCount) {
			return procCount==0 ? 1 : (int)Math.Ceiling((double)procCount/ProcLimit());
		}

		private object Invoke(string methodName,params object[] arrayArgs) {
			MethodInfo methodInfo=typeof(FormClaimPrint).GetMethod(methodName,BindingFlags.Instance|BindingFlags.NonPublic);
			if(methodInfo==null) {
				throw new MissingMethodException(nameof(FormClaimPrint),methodName);
			}
			try {
				return methodInfo.Invoke(_form,arrayArgs);
			}
			catch(TargetInvocationException ex) {
				ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
				return null;//Not reached.
			}
		}
	}
}
