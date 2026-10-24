using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenDental {
	/// <summary></summary>
	public partial class FormInsBenefits : FormODBase {
		///<summary>Set this externally before opening the form.</summary>
		public InsSub InsSub_;
		///<summary>Set this externally before opening the form. Provide a deep copy of the benefits.</summary>
		public List<Benefit> ListBenefits;
		///<summary>Set this externally before opening the form. 0 indicates calendar year, otherwise 1-12. This forces all benefits to conform to this setting. User can change as a whole.</summary>
		public byte MonthRenew;
		///<summary>Set this externally before opening the form. The subscriber note.</summary>
		public string Note;
		private long _planNum;
		private long _patPlanNum;
		///<summary>A shallow list of all benefits passed into this form along with new benefits added by the user. Some of the benefits in this list will be displayed within the simple view and the rest will be displayed within the grid. This list gets recreated sometimes by starting with the list of benefits showing in the grid and then making new benefit objects out of the current state of the simple view UI.</summary>
		private List<Benefit> _listBenefitsAll;
		///<summary>A shallow, subset list of the benefits that were passed into this form. These benefits are the ones displaying within the grid on this form.</summary>
		private List<Benefit> _listBenefitsGrid;
		private bool _dontAllowSimplified;
		private bool _isResettingQuickText=false;
		///<summary>Benefits of retired (hidden) code groups, Open Dental 25.1. They get no row in any grid of this window, are saved back unchanged,
		///and the estimates keep using them.</summary>
		private List<Benefit> _listBenefitsRetired=new List<Benefit>();
		///<summary>Rows of the Other Benefits grid whose cells do not make a valid benefit yet, with the reasons. Their cells are red and Save is
		///blocked until they are fixed.</summary>
		private Dictionary<OtherBenefitRow,List<BenefitValidationError>> _dictOtherBenefitErrors=new Dictionary<OtherBenefitRow,List<BenefitValidationError>>();
		///<summary>Insurance categories offered in the Category cell of rows that use a category (not a procedure code or a code group).</summary>
		private List<CovCat> _listCovCats;
		private const int COL_PAT=0;
		private const int COL_LEVEL=1;
		private const int COL_TYPE=2;
		private const int COL_CATEGORY=3;
		private const int COL_PERCENT=4;
		private const int COL_AMOUNT=5;
		private const int COL_TIME_PERIOD=6;
		private const int COL_QUANTITY=7;
		private const int COL_QUALIFIER=8;
		private const int COL_TREAT_AREA=9;
		private const int COL_TEETH=10;

		///<summary></summary>
		public FormInsBenefits(long planNum,long patPlanNum) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_patPlanNum=patPlanNum;
			_planNum=planNum;
		}

		private void FormInsBenefits_Load(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.InsPlanEdit,true)) {
				butSave.Enabled=false;
				butAddBenefit.Enabled=false;
				butAddFrequency.Enabled=false;
				butDelete.Enabled=false;
			}
			//Benefits of retired code groups are kept aside: no rows for them, but they are saved back unchanged.
			_listBenefitsRetired=ListBenefits.FindAll(x => !CodeGroups.IsShownInBenefitsWindow(x));
			_listBenefitsAll=ListBenefits.FindAll(x => CodeGroups.IsShownInBenefitsWindow(x));
			_listCovCats=CovCats.GetDeepCopy(true);
			gridBenefits.FuncCellIsEditable=IsOtherBenefitCellEditable;
			if(CovCats.GetForEbenCat(EbenefitCategory.Accident)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.Crowns)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.Diagnostic)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.Endodontics)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.General)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.MaxillofacialProsth)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.OralSurgery)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.Orthodontics)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.Periodontics)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.Prosthodontics)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.Restorative)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.RoutinePreventive)==null
				|| CovCats.GetForEbenCat(EbenefitCategory.DiagnosticXRay)==null
				|| Clinics.IsMedicalPracticeOrClinic(Clinics.ClinicNum)
				)
			{
				_dontAllowSimplified=true;
				checkSimplified.Checked=false;
				gridBenefits.Title=Lan.g(this,"Benefits");
				gridFrequencies.Visible=false;
				checkShowProcCodes.Visible=false;
				butAddFrequency.Visible=false;
				panelSimple.Visible=false;
				groupCategories.Visible=false;
				LayoutManager.MoveLocation(gridBenefits,new Point(gridBenefits.Left,groupYear.Bottom+3));
				LayoutManager.MoveHeight(gridBenefits,butAddBenefit.Top-gridBenefits.Top-5);
				Width=LayoutManager.Scale(796);
			}
			FillCalendarYear();
			FillSimple();
			FillGridAgeLimits();
			FillGridBenefits();
			FillGridFrequencies();
			textSubscNote.Text=Note;
			if(InsSub_==null) {
				textSubscNote.Visible=false;
				labelSubscNote.Visible=false;
			}
			if(Clinics.IsMedicalPracticeOrClinic(Clinics.ClinicNum)) {
				checkSimplified.Checked=false;
				checkSimplified.Visible=false;
			}
		}

		private void checkSimplified_Click(object sender,EventArgs e) {
			if(!IsOtherBenefitsGridValid()) {
				checkSimplified.Checked=!checkSimplified.Checked;
				return;
			}
			if(checkSimplified.Checked) {
				if(_dontAllowSimplified) {
					checkSimplified.Checked=false;
					MsgBox.Show(this,"Not allowed to use simplified view until you fix your Insurance Categories from the setup menu.  At least one of each e-benefit category must be present.");
					return;
				}
				gridBenefits.Title=Lan.g(this,"Other Benefits");
				gridFrequencies.Visible=true;
				checkShowProcCodes.Visible=true;
				butAddFrequency.Visible=true;
				panelSimple.Visible=true;
				groupCategories.Visible=true;
				LayoutManager.MoveLocation(gridBenefits,new Point(gridBenefits.Left,panelSimple.Bottom+4));
				LayoutManager.MoveHeight(gridBenefits,textSubscNote.Top-gridBenefits.Top-5);
				//LayoutManager.MoveLocation(butAddBenefit,new Point(gridBenefits.Right-78,gridBenefits.Bottom+6));
				//LayoutManager.MoveLocation(butDelete,new Point(butAddBenefit.Left,butAddBenefit.Bottom+6));
				Width=LayoutManager.Scale(1175);
				//FillSimple handles all further logic.
				FillSimple();
				FillGridAgeLimits();
				FillGridBenefits();
				FillGridFrequencies();
				return;
			}
			//Not checked
			if(!ConvertFormToBenefits()) {
				checkSimplified.Checked=true;
				return;
			}
			gridBenefits.Title=Lan.g(this,"Benefits");
			gridFrequencies.Visible=false;
			checkShowProcCodes.Visible=false;
			butAddFrequency.Visible=false;
			panelSimple.Visible=false;
			groupCategories.Visible=false;
			LayoutManager.MoveLocation(gridBenefits,new Point(gridBenefits.Left,groupYear.Bottom+3));
			LayoutManager.MoveHeight(gridBenefits,textSubscNote.Top-gridBenefits.Top-5);
			//LayoutManager.MoveLocation(butAddBenefit,new Point(gridBenefits.Right-78,gridBenefits.Bottom+6));
			//LayoutManager.MoveLocation(butDelete,new Point(butAddBenefit.Left,butAddBenefit.Bottom+6));
			Width=LayoutManager.Scale(796);
			FillSimple();
			FillGridBenefits();
		}

		///<summary>This will only be run when the form first opens or if user switches to simple view.  FillGridBenefits should always be run after this.</summary>
		private void FillSimple() {
			if(!panelSimple.Visible) {
				//Show all of the benefits when the simple view is not showing to the user.
				_listBenefitsGrid=new List<Benefit>(_listBenefitsAll);
				return;
			}
			#region Reset UI
			textAnnualMax.Text="";
			textDeductible.Text="";
			textAnnualMaxFam.Text="";
			textDeductibleFam.Text="";
			textOrthoAge.Text="";
			textOrthoMax.Text="";
			textOrthoPercent.Text="";
			textDeductDiag.Text="";
			textDeductXray.Text="";
			textDeductPrevent.Text="";
			textDeductDiagFam.Text="";
			textDeductXrayFam.Text="";
			textDeductPreventFam.Text="";
			textStand1.Text="";
			textStand2.Text="";
			textStand4.Text="";
			textDiagnostic.Text="";
			textXray.Text="";
			textRoutinePrev.Text="";
			textRestorative.Text="";
			textEndo.Text="";
			textPerio.Text="";
			textOralSurg.Text="";
			textCrowns.Text="";
			textProsth.Text="";
			textMaxProsth.Text="";
			textAccident.Text="";
			textWaitRestorative.Text="";
			textWaitProsth.Text="";
			textWaitPerio.Text="";
			textWaitOralSurg.Text="";
			textWaitEndo.Text="";
			textWaitCrowns.Text="";
			#endregion
			_listBenefitsGrid=new List<Benefit>();
			Benefit benefit;
			for(int i=0;i<_listBenefitsAll.Count;i++) {
				benefit=_listBenefitsAll[i];
				#region Annual Max
				//annual max individual
				if(Benefits.IsAnnualMax(benefit,BenefitCoverageLevel.Individual)) {
					textAnnualMax.Text=benefit.MonetaryAmt.ToString("n");
				}
				//annual max family
				else if(Benefits.IsAnnualMax(benefit,BenefitCoverageLevel.Family)) {
					textAnnualMaxFam.Text=benefit.MonetaryAmt.ToString("n");
				}
				#endregion
				#region General Deductibles
				//deductible individual
				else if(Benefits.IsGeneralDeductible(benefit,BenefitCoverageLevel.Individual)) {
					textDeductible.Text=benefit.MonetaryAmt.ToString("n");
				}
				//deductible family
				else if(Benefits.IsGeneralDeductible(benefit,BenefitCoverageLevel.Family)) {
					textDeductibleFam.Text=benefit.MonetaryAmt.ToString("n");
				}
				#endregion
				#region Age Limitations
				else if(Benefits.IsAgeLimit(benefit)) {
					// Will be seen in Age Limits grid.
				}
				#endregion
				#region Frequencies
				else if(checkSimplified.Checked && Benefits.IsFrequencyLimitation(benefit) && benefit.CodeGroupNum!=0) {
					// Will be shown in Frequencies grid
				}
				#endregion
				#region Ortho
				//Ortho Age
				else if(Benefits.IsOrthoAgeBenefit(benefit)) {
					textOrthoAge.Text=benefit.Quantity.ToString();
				}
				//OrthoMax
				else if(Benefits.IsOrthoMaxBenefit(benefit)) {
					textOrthoMax.Text=benefit.MonetaryAmt.ToString("n");
				}
				//OrthoPercent
				else if(Benefits.IsOrthoPercentBenefit(benefit)) {
					textOrthoPercent.Text=benefit.Percent.ToString();
				}
				#endregion
				#region Diagnostic
				#region Deductible - Diagnostic
				//deductible diagnostic individual
				else if(Benefits.IsDiagnosticDeductibleBenefit(benefit,BenefitCoverageLevel.Individual)) {
					textDeductDiag.Text=benefit.MonetaryAmt.ToString("n");
				}
				//deductible diagnostic family
				else if(Benefits.IsDiagnosticDeductibleBenefit(benefit,BenefitCoverageLevel.Family)) {
					textDeductDiagFam.Text=benefit.MonetaryAmt.ToString("n");
				}
				#endregion
				#region Deductible - DiagnosticXRay
				//deductible xray individual
				else if(Benefits.IsXRayDeductibleBenefit(benefit,BenefitCoverageLevel.Individual)) {
					textDeductXray.Text=benefit.MonetaryAmt.ToString("n");
				}
				//deductible xray family
				else if(Benefits.IsXRayDeductibleBenefit(benefit,BenefitCoverageLevel.Family)) {
					textDeductXrayFam.Text=benefit.MonetaryAmt.ToString("n");
				}
				#endregion
				#region Deductible - RoutinePreventive
				//deductible preventive individual
				else if(Benefits.IsPreventativeDeductibleBenefit(benefit,BenefitCoverageLevel.Individual)) {
					textDeductPrevent.Text=benefit.MonetaryAmt.ToString("n");
				}
				//deductible preventive family
				else if(Benefits.IsPreventativeDeductibleBenefit(benefit,BenefitCoverageLevel.Family)) {
					textDeductPreventFam.Text=benefit.MonetaryAmt.ToString("n");
				}
				#endregion
				#endregion
				#region CoInsurance
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.Diagnostic)) {
					textDiagnostic.Text=benefit.Percent.ToString();
				}
				//X-Ray
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.DiagnosticXRay)) {
					textXray.Text=benefit.Percent.ToString();
				}
				//RoutinePreventive
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.RoutinePreventive)) {
					textRoutinePrev.Text=benefit.Percent.ToString();
				}
				//Restorative
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.Restorative)) {
					textRestorative.Text=benefit.Percent.ToString();
				}
				//Endo
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.Endodontics)) {
					textEndo.Text=benefit.Percent.ToString();
				}
				//Perio
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.Periodontics)) {
					textPerio.Text=benefit.Percent.ToString();
				}
				//OralSurg
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.OralSurgery)) {
					textOralSurg.Text=benefit.Percent.ToString();
				}
				//Crowns
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.Crowns)) {
					textCrowns.Text=benefit.Percent.ToString();
				}
				//Prosth
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.Prosthodontics)) {
					textProsth.Text=benefit.Percent.ToString();
				}
				//MaxProsth
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.MaxillofacialProsth)) {
					textMaxProsth.Text=benefit.Percent.ToString();
				}
				//Accident
				else if(Benefits.IsCoInsuranceBenefit(benefit,EbenefitCategory.Accident)) {
					textAccident.Text=benefit.Percent.ToString();
				}
				#endregion
				#region WaitingPeriod
				//Restorative Waiting Period
				else if(Benefits.IsWaitingPeriodBenefit(benefit,EbenefitCategory.Restorative)) {
					if(benefit.QuantityQualifier==BenefitQuantity.Months) {
						textWaitRestorative.Text=benefit.Quantity.ToString();
					}
					else if(benefit.QuantityQualifier==BenefitQuantity.Years) {
						textWaitRestorative.Text=(benefit.Quantity*12).ToString();//Convert to months
					}
				}
				//Endo Waiting Period
				else if(Benefits.IsWaitingPeriodBenefit(benefit,EbenefitCategory.Endodontics)) {
					if(benefit.QuantityQualifier==BenefitQuantity.Months) {
						textWaitEndo.Text=benefit.Quantity.ToString();
					}
					else if(benefit.QuantityQualifier==BenefitQuantity.Years) {
						textWaitEndo.Text=(benefit.Quantity*12).ToString();//Convert to months
					}
				}
				//Perio Waiting Period
				else if(Benefits.IsWaitingPeriodBenefit(benefit,EbenefitCategory.Periodontics)) {
					if(benefit.QuantityQualifier==BenefitQuantity.Months) {
						textWaitPerio.Text=benefit.Quantity.ToString();
					}
					else if(benefit.QuantityQualifier==BenefitQuantity.Years) {
						textWaitPerio.Text=(benefit.Quantity*12).ToString();//Convert to months
					}
				}
				//OralSurg Waiting Period
				else if(Benefits.IsWaitingPeriodBenefit(benefit,EbenefitCategory.OralSurgery)) {
					if(benefit.QuantityQualifier==BenefitQuantity.Months) {
						textWaitOralSurg.Text=benefit.Quantity.ToString();
					}
					else if(benefit.QuantityQualifier==BenefitQuantity.Years) {
						textWaitOralSurg.Text=(benefit.Quantity*12).ToString();//Convert to months
					}
				}
				//Crowns Waiting Period
				else if(Benefits.IsWaitingPeriodBenefit(benefit,EbenefitCategory.Crowns)) {
					if(benefit.QuantityQualifier==BenefitQuantity.Months) {
						textWaitCrowns.Text=benefit.Quantity.ToString();
					}
					else if(benefit.QuantityQualifier==BenefitQuantity.Years) {
						textWaitCrowns.Text=(benefit.Quantity*12).ToString();//Convert to months
					}
				}
				//Prosthodontics Waiting Period
				else if(Benefits.IsWaitingPeriodBenefit(benefit,EbenefitCategory.Prosthodontics)) {
					if(benefit.QuantityQualifier==BenefitQuantity.Months) {
						textWaitProsth.Text=benefit.Quantity.ToString();
					}
					else if(benefit.QuantityQualifier==BenefitQuantity.Years) {
						textWaitProsth.Text=(benefit.Quantity*12).ToString();//Convert to months
					}
				}
				#endregion
				else {//any benefit that didn't get handled above should show in the grid.
					_listBenefitsGrid.Add(benefit);
				}
			}
			if(textDiagnostic.Text !="" 
				&& textDiagnostic.Text==textRoutinePrev.Text
				&& textDiagnostic.Text==textXray.Text)
			{
				textStand1.Text=textDiagnostic.Text;
			}
			if(textRestorative.Text !="" 
				&& textRestorative.Text==textEndo.Text 
				&& textRestorative.Text==textPerio.Text	
				&& textRestorative.Text==textOralSurg.Text)
			{
				textStand2.Text=textRestorative.Text;
			}
			if(textCrowns.Text !="" && textCrowns.Text==textProsth.Text) {
				textStand4.Text=textCrowns.Text;
			}
		}

		///<summary>Clears Quick% textbox associated to textCur if newly entered value in textCur no longer matches the existing Quick% value.</summary>
		private void TryResetQuickText(System.Windows.Forms.TextBox textBox,System.Windows.Forms.TextBox textBoxStand) {
			if(textBox.Text!=textBoxStand.Text) {
				_isResettingQuickText=true;
				textBoxStand.Text="";
				_isResettingQuickText=false;
			}
		}

		private void textDiagnostic_Leave(object sender,EventArgs e) {
			TryResetQuickText((System.Windows.Forms.TextBox)sender,textStand1);
		}

		private void textXray_Leave(object sender,EventArgs e) {
			TryResetQuickText((System.Windows.Forms.TextBox)sender,textStand1);
		}

		private void textRoutinePrev_Leave(object sender,EventArgs e) {
			TryResetQuickText((System.Windows.Forms.TextBox)sender,textStand1);
		}

		private void textRestorative_Leave(object sender,EventArgs e) {
			TryResetQuickText((System.Windows.Forms.TextBox)sender,textStand2);
		}

		private void textEndo_Leave(object sender,EventArgs e) {
			TryResetQuickText((System.Windows.Forms.TextBox)sender,textStand2);
		}

		private void textPerio_Leave(object sender,EventArgs e) {
			TryResetQuickText((System.Windows.Forms.TextBox)sender,textStand2);
		}

		private void textOralSurg_Leave(object sender,EventArgs e) {
			TryResetQuickText((System.Windows.Forms.TextBox)sender,textStand2);
		}

		private void textCrowns_Leave(object sender,EventArgs e) {
			TryResetQuickText((System.Windows.Forms.TextBox)sender,textStand4);
		}

		private void textProsth_Leave(object sender,EventArgs e) {
			TryResetQuickText((System.Windows.Forms.TextBox)sender,textStand4);
		}

		private void textStand1_TextChanged(object sender,EventArgs e) {
			if(_isResettingQuickText) {
				return;
			}
			textDiagnostic.Text=textStand1.Text;
			textXray.Text=textStand1.Text;
			textRoutinePrev.Text=textStand1.Text;
		}

		private void textStand2_TextChanged(object sender,EventArgs e) {
			if(_isResettingQuickText) {
				return;
			}
			textRestorative.Text=textStand2.Text;
			textEndo.Text=textStand2.Text;
			textPerio.Text=textStand2.Text;
			textOralSurg.Text=textStand2.Text;
		}

		private void textStand4_TextChanged(object sender,EventArgs e) {
			if(_isResettingQuickText) {
				return;
			}
			textCrowns.Text=textStand4.Text;
			textProsth.Text=textStand4.Text;
		}

		///<summary>only run once at startup.</summary>
		private void FillCalendarYear() {
			//Convert all of the CalendarYear or ServiceYear benefits over to the correct TimePeriod based on the value of MonthRenew.
			bool isCalendar=(MonthRenew==0);
			List<Benefit> listBenefits;
			BenefitTimePeriod benefitTimePeriod=BenefitTimePeriod.None;
			if(isCalendar) {
				//Convert all ServiceYear benefits over to CalendarYear.
				listBenefits=_listBenefitsAll.FindAll(x => x.TimePeriod==BenefitTimePeriod.ServiceYear);
				benefitTimePeriod=BenefitTimePeriod.CalendarYear;
				checkCalendarYear.Checked=true;
				textMonth.Text="";
				textMonth.Enabled=false;
			}
			else {
				//Convert all CalendarYear benefits over to ServiceYear.
				listBenefits=_listBenefitsAll.FindAll(x => x.TimePeriod==BenefitTimePeriod.CalendarYear);
				benefitTimePeriod=BenefitTimePeriod.ServiceYear;
				checkCalendarYear.Checked=false;
				textMonth.Text=MonthRenew.ToString();
				textMonth.Enabled=true;
			}
			for(int i=0;i<listBenefits.Count;i++) {
				listBenefits[i].TimePeriod=benefitTimePeriod;
			}
		}

		/// <summary> Should be called any time a change to an age limit needs made visible. </summary>
		private void FillGridAgeLimits() {
			gridAgeLimits.BeginUpdate();
			gridAgeLimits.Columns.Clear();
			GridColumn gridColumn = new GridColumn("Code Group",140);
			gridAgeLimits.Columns.Add(gridColumn);
			gridColumn = new GridColumn("Codes", 80);
			gridAgeLimits.Columns.Add(gridColumn);
			gridColumn = new GridColumn("Age",30,HorizontalAlignment.Center);
			gridColumn.IsEditable=true;
			gridAgeLimits.Columns.Add(gridColumn);
			gridAgeLimits.ListGridRows.Clear();
			GridRow gridRow;
			List<CodeGroup> listCodeGroups = CodeGroups.GetDeepCopy();
			for(int i=0;i<listCodeGroups.Count;i++) {
				if(listCodeGroups[i].IsHidden) {
					continue;//Retired code group (Open Dental 25.1): no row.
				}
				//There will not be duplicates like with frequencies.
				Benefit benefit=_listBenefitsAll.Find(x=>x.CodeGroupNum==listCodeGroups[i].CodeGroupNum && Benefits.IsAgeLimit(x));
				if(benefit==null) {
					if(!listCodeGroups[i].ShowInAgeLimit) {
						//Do not provide an age limit row for this codegroup.
						continue;
					}
					benefit = new Benefit();
					benefit.CodeGroupNum=listCodeGroups[i].CodeGroupNum;
					benefit = MakeAgeLimitBenefit(benefit,0);
				}
				gridRow=new GridRow();
				gridRow.Cells.Add(Benefits.GetCategoryString(benefit));// Code Group
				gridRow.Cells.Add(listCodeGroups[i].ProcCodes);
				if(benefit.Quantity > 0) { // Age
					gridRow.Cells.Add(benefit.Quantity.ToString());
				}
				else {
					gridRow.Cells.Add("");
				}
				gridRow.Tag=benefit;
				gridAgeLimits.ListGridRows.Add(gridRow);
			}
			gridAgeLimits.EndUpdate();
		}

		/// <summary> Should be called any time a change to a Frequency needs made visible. </summary>
		private void FillGridFrequencies() {
			List<CodeGroup> listCodeGroups=CodeGroups.GetDeepCopy();//including hidden
			List<Benefit> listBenefits=_listBenefitsAll.FindAll(x=>Benefits.IsFrequencyLimitation(x) && x.CodeGroupNum!=0);
			listBenefits.Sort();
			gridFrequencies.BeginUpdate();
			gridFrequencies.Columns.Clear();
			GridColumn gridColumn=new GridColumn("Pat",35,HorizontalAlignment.Center);
			gridFrequencies.Columns.Add(gridColumn);
			gridColumn=new GridColumn("Code Group",140);
			if(checkShowProcCodes.Checked) {
				gridColumn=new GridColumn("Codes",140);
			}
			gridFrequencies.Columns.Add(gridColumn);
			gridColumn=new GridColumn("#",25,HorizontalAlignment.Center);
			gridColumn.IsEditable=true;
			gridFrequencies.Columns.Add(gridColumn);
			gridColumn=new GridColumn("Frequency",110);
			List<string> listStringsFrequencyOptions = new List<string>() {
				FrequencyOptions.Every_Years.GetDescription(),
				FrequencyOptions._PerBenefitYear.GetDescription(),
				FrequencyOptions.Every_Months.GetDescription(),
				FrequencyOptions._InLast12Months.GetDescription(),
			};
			gridColumn.ListDisplayStrings=listStringsFrequencyOptions;
			gridFrequencies.Columns.Add(gridColumn);
			gridColumn=new GridColumn("Treat Area",100);
			List<string> listStringsTreatmentArea=new List<string>() {
				"Default",
				TreatmentArea.Surf.ToString(),
				TreatmentArea.Tooth.ToString(),
				TreatmentArea.Mouth.ToString(),
				TreatmentArea.Quad.ToString(),
				TreatmentArea.Sextant.ToString(),
				TreatmentArea.Arch.ToString(),
				TreatmentArea.ToothRange.ToString()
			};
			gridColumn.ListDisplayStrings=listStringsTreatmentArea;
			gridFrequencies.Columns.Add(gridColumn);
			gridFrequencies.ListGridRows.Clear();
			GridRow gridRow;
			for(int i=0;i<listCodeGroups.Count;i++) {
				if(listCodeGroups[i].IsHidden) {
					continue;//Retired code group (Open Dental 25.1): no row. Its benefits were set aside when the window opened.
				}
				List<Benefit> listBenefitsGroup=listBenefits.FindAll(x=>x.CodeGroupNum==listCodeGroups[i].CodeGroupNum);
				if(listBenefitsGroup.Count==0) {
					if(!listCodeGroups[i].ShowInFrequency) {
						continue;
					}
					gridRow=new GridRow();
					Benefit benefit=new Benefit();
					benefit.CodeGroupNum=listCodeGroups[i].CodeGroupNum; // Give benefit a CodeGroupNum so the 'Save' button knows how to handle it.
					gridRow.Cells.Add("");
					string textForCell=listCodeGroups[i].GroupName;// Code Group or Proc Codes
					if(checkShowProcCodes.Checked) {
						List<ProcedureCode> listProcedureCodes=CodeGroups.GetProcedureCodes(listCodeGroups[i]);//ranges expanded
						textForCell="";
						for(int c=0;c<listProcedureCodes.Count;c++) {
							if(c!=0) {
								textForCell+="\r\n";
							}
							textForCell+=listProcedureCodes[c].ProcCode + " - " + listProcedureCodes[c].AbbrDesc;
						}
					}
					gridRow.Cells.Add(textForCell);
					gridRow.Cells.Add("");//#
					GridCell gridCell=new GridCell(listStringsFrequencyOptions[0]);//Frequency
					gridCell.ComboSelectedIndex=0;
					gridRow.Cells.Add(gridCell);
					gridCell=new GridCell(listStringsTreatmentArea[0]);//Treatment Area
					gridCell.ComboSelectedIndex=0;
					gridRow.Cells.Add(gridCell);
					gridRow.Tag=benefit;
					gridFrequencies.ListGridRows.Add(gridRow);
					continue;
				}
				for(int k=0;k<listBenefitsGroup.Count;k++){//one or more
					//There could be duplicates for a codegroup for various reasons.
					//For example, patient overrides or different treatment areas.
					gridRow=new GridRow();
					gridRow.Cells.Add(listBenefitsGroup[k].PatPlanNum==0?"":"X");
					string textForCell=Benefits.GetCategoryString(listBenefitsGroup[k]);// Code Group or Proc Codes
					if(checkShowProcCodes.Checked) {
						List<ProcedureCode> listProcedureCodes=CodeGroups.GetProcedureCodes(listCodeGroups[i]);//ranges expanded
						textForCell="";
						for(int c=0;c<listProcedureCodes.Count;c++) {
							if(c!=0) {
								textForCell+="\r\n";
							}
							textForCell+=listProcedureCodes[c].ProcCode + " - " + listProcedureCodes[c].AbbrDesc;
						}
					}
					gridRow.Cells.Add(textForCell);
					gridRow.Cells.Add(listBenefitsGroup[k].Quantity.ToString()); //#
					FrequencyOptions frequencyOptions=listBenefitsGroup[k].GetFrequencyOption();
					GridCell gridCell=new GridCell(listStringsFrequencyOptions[(int)frequencyOptions]); //Frequency
					gridCell.ComboSelectedIndex=(int)frequencyOptions;
					gridRow.Cells.Add(gridCell);
					TreatmentArea treatmentArea=listBenefitsGroup[k].TreatArea;
					gridCell=new GridCell(listStringsTreatmentArea[(int)treatmentArea]);//Treatment Area
					gridCell.ComboSelectedIndex=(int)treatmentArea;
					gridRow.Cells.Add(gridCell);
					gridRow.Tag=listBenefitsGroup[k];
					gridFrequencies.ListGridRows.Add(gridRow);
				}
			}
			gridFrequencies.EndUpdate();
		}

		///<summary>This only fills the grid labeled 'Other Benefits'. It does not get any data from the database.
		///From Open Dental 25.3 its rows are edited in place (double click or Edit still opens Edit Benefit), and every code group flagged Show in
		///Other Benefits that has no row yet gets a blank, pre-filled row; picking a type and values there creates the benefit. Edits are checked
		///when a cell is left, with the rules of Edit Benefit, and saved with the window.</summary>
		private void FillGridBenefits() {
			_listBenefitsGrid.Sort();
			_dictOtherBenefitErrors.Clear();
			gridBenefits.BeginUpdate();
			gridBenefits.Columns.Clear();
			GridColumn col=new GridColumn(Lan.g(this,"Pat"),35,HorizontalAlignment.Center);
			gridBenefits.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Level"),60);
			col.ListDisplayStrings=GetEnumStrings<BenefitCoverageLevel>("enumBenefitCoverageLevel");
			gridBenefits.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Type"),90);
			List<string> listStringsType=new List<string>() { "" };//blank: no type picked yet
			listStringsType.AddRange(GetEnumStrings<InsBenefitType>("enumInsBenefitType"));
			col.ListDisplayStrings=listStringsType;
			gridBenefits.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Category"),95);
			List<string> listStringsCategory=new List<string>() { "" };
			listStringsCategory.AddRange(_listCovCats.Select(x => x.Description));
			col.ListDisplayStrings=listStringsCategory;
			gridBenefits.Columns.Add(col);
			col=new GridColumn("%",35,HorizontalAlignment.Center,isEditable:true);
			gridBenefits.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Amt"),50,HorizontalAlignment.Right,isEditable:true);
			gridBenefits.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Time Period"),85);
			col.ListDisplayStrings=GetEnumStrings<BenefitTimePeriod>("enumBenefitTimePeriod");
			gridBenefits.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Qty"),35,HorizontalAlignment.Center,isEditable:true);
			gridBenefits.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Qualifier"),80);
			col.ListDisplayStrings=GetEnumStrings<BenefitQuantity>("enumBenefitQuantity");
			gridBenefits.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Treat Area"),65);
			col.ListDisplayStrings=GetEnumStrings<TreatmentArea>("enumTreatmentArea");
			gridBenefits.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Teeth"),55,isEditable:true);
			gridBenefits.Columns.Add(col);
			gridBenefits.ListGridRows.Clear();
			for(int i=0;i<_listBenefitsGrid.Count;i++) {
				gridBenefits.ListGridRows.Add(MakeOtherBenefitRow(new OtherBenefitRow(_listBenefitsGrid[i],isInList:true)));
			}
			//Pre-filled rows for code groups flagged Show in Other Benefits (codegroup.ShowInOther), the way the Age Limitations grid lists groups.
			List<CodeGroup> listCodeGroups=CodeGroups.GetForOtherBenefitsGrid();
			for(int i=0;i<listCodeGroups.Count;i++) {
				if(_listBenefitsGrid.Any(x => x.CodeGroupNum==listCodeGroups[i].CodeGroupNum && !Benefits.IsFrequencyLimitation(x))) {
					continue;
				}
				Benefit benefit=new Benefit();
				benefit.CodeGroupNum=listCodeGroups[i].CodeGroupNum;
				benefit.PlanNum=_planNum;
				gridBenefits.ListGridRows.Add(MakeOtherBenefitRow(new OtherBenefitRow(benefit,isInList:false)));
			}
			gridBenefits.EndUpdate();
		}

		///<summary>The display strings of an enum, in enum order, translated. The None member of the "none" enums shows blank.</summary>
		private List<string> GetEnumStrings<T>(string translationName) where T:Enum {
			List<string> listStrings=new List<string>();
			foreach(T value in Enum.GetValues(typeof(T))) {
				if(Convert.ToInt32(value)==0 && value.ToString()=="None") {
					listStrings.Add("");
					continue;
				}
				listStrings.Add(Lan.g(translationName,value.ToString()));
			}
			return listStrings;
		}

		///<summary>One row of the Other Benefits grid. A pre-filled code group row has no type picked (combo index 0 in the Type column).</summary>
		private GridRow MakeOtherBenefitRow(OtherBenefitRow otherBenefitRow) {
			Benefit benefit=otherBenefitRow.Benefit;
			GridRow row=new GridRow();
			row.Cells.Add(benefit.PatPlanNum==0?"":"X");
			row.Cells.Add(MakeComboCell(gridBenefits.Columns[COL_LEVEL],(int)benefit.CoverageLevel));
			if(otherBenefitRow.IsInList) {
				GridCell gridCellType=MakeComboCell(gridBenefits.Columns[COL_TYPE],(int)benefit.BenefitType+1);
				if(benefit.BenefitType==InsBenefitType.CoInsurance && benefit.Percent != -1) {
					gridCellType.Text="%";
				}
				else if(Benefits.IsFrequencyLimitation(benefit)) {
					gridCellType.Text=Lan.g(this,"Frequency");
				}
				row.Cells.Add(gridCellType);
			}
			else {
				row.Cells.Add(MakeComboCell(gridBenefits.Columns[COL_TYPE],0));
			}
			GridCell gridCellCategory=new GridCell(Benefits.GetCategoryString(benefit));
			gridCellCategory.ComboSelectedIndex=_listCovCats.FindIndex(x => x.CovCatNum==benefit.CovCatNum)+1;
			if(benefit.CodeGroupNum!=0 || benefit.CodeNum!=0) {
				gridCellCategory.ComboSelectedIndex=-1;//read-only
			}
			else if(benefit.CovCatNum==0) {
				gridCellCategory.Text="";
				gridCellCategory.ComboSelectedIndex=0;
			}
			row.Cells.Add(gridCellCategory);
			row.Cells.Add(benefit.Percent==-1?"":benefit.Percent.ToString());
			row.Cells.Add(benefit.GetDisplayMonetaryAmt());
			row.Cells.Add(MakeComboCell(gridBenefits.Columns[COL_TIME_PERIOD],(int)benefit.TimePeriod));
			row.Cells.Add(benefit.Quantity>0?benefit.Quantity.ToString():"");
			row.Cells.Add(MakeComboCell(gridBenefits.Columns[COL_QUALIFIER],(int)benefit.QuantityQualifier));
			row.Cells.Add(MakeComboCell(gridBenefits.Columns[COL_TREAT_AREA],(int)benefit.TreatArea));
			row.Cells.Add(Benefits.GetToothRangeDisplay(benefit));
			row.Tag=otherBenefitRow;
			otherBenefitRow.SignatureApplied=GetRowSignature(row);
			return row;
		}

		///<summary>The texts and combo choices of a row, to tell whether a cell was really changed since the row was last applied. Leaving a cell
		///without changing anything must not check (and possibly block) an old benefit that the newer rules would refuse.</summary>
		private string GetRowSignature(GridRow gridRow) {
			return string.Join("\t",gridRow.Cells.Select(x => x.Text+"\v"+x.ComboSelectedIndex));
		}

		private GridCell MakeComboCell(GridColumn gridColumn,int index) {
			GridCell gridCell=new GridCell("");
			if(index>=0 && index<gridColumn.ListDisplayStrings.Count) {
				gridCell.Text=gridColumn.ListDisplayStrings[index];
				gridCell.ComboSelectedIndex=index;
			}
			return gridCell;
		}

		///<summary>Which cells of the Other Benefits grid can be edited in place. The category is read-only on rows with a code group or procedure
		///code, Treat Area only applies to limitations, and Teeth only to exclusions.</summary>
		private bool IsOtherBenefitCellEditable(int col,int row) {
			if(!butSave.Enabled) {
				return false;//no InsPlanEdit permission
			}
			if(row<0 || row>=gridBenefits.ListGridRows.Count || !(gridBenefits.ListGridRows[row].Tag is OtherBenefitRow otherBenefitRow)) {
				return false;
			}
			GridRow gridRow=gridBenefits.ListGridRows[row];
			int typeIndex=gridRow.Cells[COL_TYPE].ComboSelectedIndex-1;
			switch(col) {
				case COL_CATEGORY:
					return otherBenefitRow.Benefit.CodeGroupNum==0 && otherBenefitRow.Benefit.CodeNum==0;
				case COL_TREAT_AREA:
					return typeIndex==(int)InsBenefitType.Limitations || gridRow.Cells[COL_TREAT_AREA].ComboSelectedIndex>0;
				case COL_TEETH:
					return typeIndex==(int)InsBenefitType.Exclusions;
				default:
					return true;
			}
		}

		private void gridBenefits_CellClick(object sender,ODGridClickEventArgs e) {
			if(e.Col!=COL_PAT || e.Row<0 || e.Row>=gridBenefits.ListGridRows.Count || !butSave.Enabled) {
				return;
			}
			if(_patPlanNum==0) {
				return;//No patient: benefits can only be attached to the plan.
			}
			GridCell gridCell=gridBenefits.ListGridRows[e.Row].Cells[COL_PAT];
			gridCell.Text=(gridCell.Text=="X")?"":"X";
			ApplyOtherBenefitRow(e.Row);
		}

		private void gridBenefits_CellLeave(object sender,ODGridClickEventArgs e) {
			ApplyOtherBenefitRow(e.Row);
		}

		private void gridBenefits_CellSelectionCommitted(object sender,ODGridClickEventArgs e) {
			ApplyOtherBenefitRow(e.Row);
		}

		///<summary>Reads one row of the Other Benefits grid back into its benefit after an in-place edit. A valid row updates the benefit (and a
		///pre-filled code group row becomes a new benefit); an invalid row turns the bad cells red; a code group row left completely blank is
		///removed, so its benefit is deleted on save.</summary>
		private void ApplyOtherBenefitRow(int rowIndex) {
			if(rowIndex<0 || rowIndex>=gridBenefits.ListGridRows.Count) {
				return;
			}
			GridRow gridRow=gridBenefits.ListGridRows[rowIndex];
			if(!(gridRow.Tag is OtherBenefitRow otherBenefitRow) || gridRow.Cells.Count<=COL_TEETH) {
				return;
			}
			if(GetRowSignature(gridRow)==otherBenefitRow.SignatureApplied) {
				SetOtherBenefitRowErrors(gridRow,otherBenefitRow,new List<BenefitValidationError>());//unchanged, or changed back
				return;
			}
			OtherBenefitsRowInput input=new OtherBenefitsRowInput();
			input.IsPat=gridRow.Cells[COL_PAT].Text=="X";
			input.CoverageLevel=(BenefitCoverageLevel)Math.Max(0,gridRow.Cells[COL_LEVEL].ComboSelectedIndex);
			input.TypeIndex=gridRow.Cells[COL_TYPE].ComboSelectedIndex-1;
			int indexCategory=gridRow.Cells[COL_CATEGORY].ComboSelectedIndex;
			input.CovCatNum=otherBenefitRow.Benefit.CovCatNum;//kept when the category is not in the list (hidden categories)
			if(indexCategory==0) {
				input.CovCatNum=0;
			}
			else if(indexCategory>0 && indexCategory<=_listCovCats.Count) {
				input.CovCatNum=_listCovCats[indexCategory-1].CovCatNum;
			}
			input.Percent=gridRow.Cells[COL_PERCENT].Text;
			input.Amount=gridRow.Cells[COL_AMOUNT].Text;
			input.TimePeriod=(BenefitTimePeriod)Math.Max(0,gridRow.Cells[COL_TIME_PERIOD].ComboSelectedIndex);
			input.Quantity=gridRow.Cells[COL_QUANTITY].Text;
			input.QuantityQualifier=(BenefitQuantity)Math.Max(0,gridRow.Cells[COL_QUALIFIER].ComboSelectedIndex);
			input.TreatArea=(TreatmentArea)Math.Max(0,gridRow.Cells[COL_TREAT_AREA].ComboSelectedIndex);
			input.Teeth=gridRow.Cells[COL_TEETH].Text;
			if(otherBenefitRow.Benefit.CodeGroupNum!=0 && input.IsBlank()) {
				//A code group row cleared completely: not a benefit (any more).
				if(otherBenefitRow.IsInList) {
					_listBenefitsGrid.Remove(otherBenefitRow.Benefit);
					_listBenefitsAll.Remove(otherBenefitRow.Benefit);
					otherBenefitRow.IsInList=false;
				}
				SetOtherBenefitRowErrors(gridRow,otherBenefitRow,new List<BenefitValidationError>());
				otherBenefitRow.SignatureApplied=GetRowSignature(gridRow);
				return;
			}
			Benefit benefit=Benefits.GetFromOtherBenefitsRow(otherBenefitRow.Benefit,input,_planNum,_patPlanNum,
				(ToothNumberingNomenclature)PrefC.GetInt(PrefName.UseInternationalToothNumbers),out List<BenefitValidationError> listErrors);
			SetOtherBenefitRowErrors(gridRow,otherBenefitRow,listErrors);
			if(benefit==null) {
				return;
			}
			//Update in place: the same object is in _listBenefitsGrid and _listBenefitsAll.
			Benefit benefitCur=otherBenefitRow.Benefit;
			benefitCur.PlanNum=benefit.PlanNum;
			benefitCur.PatPlanNum=benefit.PatPlanNum;
			benefitCur.CovCatNum=benefit.CovCatNum;
			benefitCur.BenefitType=benefit.BenefitType;
			benefitCur.Percent=benefit.Percent;
			benefitCur.MonetaryAmt=benefit.MonetaryAmt;
			benefitCur.TimePeriod=benefit.TimePeriod;
			benefitCur.QuantityQualifier=benefit.QuantityQualifier;
			benefitCur.Quantity=benefit.Quantity;
			benefitCur.CoverageLevel=benefit.CoverageLevel;
			benefitCur.TreatArea=benefit.TreatArea;
			benefitCur.ToothRange=benefit.ToothRange;
			if(!otherBenefitRow.IsInList) {
				_listBenefitsGrid.Add(benefitCur);
				_listBenefitsAll.Add(benefitCur);
				otherBenefitRow.IsInList=true;
			}
			gridRow.Cells[COL_TEETH].Text=Benefits.GetToothRangeDisplay(benefitCur);//tidied, or cleared when not an exclusion
			otherBenefitRow.SignatureApplied=GetRowSignature(gridRow);
			gridBenefits.Invalidate();
		}

		///<summary>Colors the cells named in the errors red (and the others back to normal) and remembers whether the row is invalid.</summary>
		private void SetOtherBenefitRowErrors(GridRow gridRow,OtherBenefitRow otherBenefitRow,List<BenefitValidationError> listErrors) {
			for(int i=0;i<gridRow.Cells.Count;i++) {
				gridRow.Cells[i].ColorBackG=Color.Empty;
			}
			if(listErrors.Count==0) {
				_dictOtherBenefitErrors.Remove(otherBenefitRow);
			}
			else {
				_dictOtherBenefitErrors[otherBenefitRow]=listErrors;
				for(int i=0;i<listErrors.Count;i++) {
					int col=GetOtherBenefitColumn(listErrors[i].Field);
					if(col<gridRow.Cells.Count) {
						gridRow.Cells[col].ColorBackG=Color.LightPink;
					}
				}
			}
			gridBenefits.Invalidate();
		}

		private int GetOtherBenefitColumn(EnumBenefitField enumBenefitField) {
			switch(enumBenefitField) {
				case EnumBenefitField.Pat:
					return COL_PAT;
				case EnumBenefitField.Level:
					return COL_LEVEL;
				case EnumBenefitField.Type:
					return COL_TYPE;
				case EnumBenefitField.Category:
					return COL_CATEGORY;
				case EnumBenefitField.Percent:
					return COL_PERCENT;
				case EnumBenefitField.Amount:
					return COL_AMOUNT;
				case EnumBenefitField.TimePeriod:
					return COL_TIME_PERIOD;
				case EnumBenefitField.Quantity:
					return COL_QUANTITY;
				case EnumBenefitField.Qualifier:
					return COL_QUALIFIER;
				case EnumBenefitField.TreatArea:
					return COL_TREAT_AREA;
				default:
					return COL_TEETH;
			}
		}

		///<summary>False, after telling the user what to fix, while any row of the Other Benefits grid has cells marked red.</summary>
		private bool IsOtherBenefitsGridValid() {
			if(_dictOtherBenefitErrors.Count==0) {
				return true;
			}
			List<string> listMessages=_dictOtherBenefitErrors.Values.SelectMany(x => x.Select(y => y.Message)).Distinct().ToList();
			MessageBox.Show(this,Lan.g(this,"Fix the Other Benefits cells marked in red first:")+"\r\n"+string.Join("\r\n",listMessages));
			return false;
		}

		private void gridFrequencies_CellClick(object sender,ODGridClickEventArgs e) {
			// Nothing to do if they click an editable column.
			if(e.Col!=0) {
				return;
			}
			string cellText=gridFrequencies.ListGridRows[e.Row].Cells[0].Text;
			if(String.IsNullOrEmpty(cellText)) {
				gridFrequencies.ListGridRows[e.Row].Cells[0]=new GridCell("X");
			}
			else {
				gridFrequencies.ListGridRows[e.Row].Cells[0]=new GridCell("");
			}
		}

		private void gridBenefits_CellDoubleClick(object sender,OpenDental.UI.ODGridClickEventArgs e) {
			EditOtherBenefitRow(e.Row);
		}

		private void butEditBenefit_Click(object sender,EventArgs e) {
			int row=gridBenefits.SelectedCell.Y;
			if(row<0 || row>=gridBenefits.ListGridRows.Count) {
				MsgBox.Show(this,"Select a row first.");
				return;
			}
			EditOtherBenefitRow(row);
		}

		///<summary>Opens the full Edit Benefit window for a row of the Other Benefits grid. For a pre-filled code group row, OK creates the benefit.</summary>
		private void EditOtherBenefitRow(int rowIndex) {
			if(rowIndex<0 || rowIndex>=gridBenefits.ListGridRows.Count || !(gridBenefits.ListGridRows[rowIndex].Tag is OtherBenefitRow otherBenefitRow)) {
				return;
			}
			if(!IsOtherBenefitsGridValid()) {
				return;
			}
			Benefit benefit=otherBenefitRow.Benefit;
			if(!otherBenefitRow.IsInList) {
				benefit.IsNew=true;
				benefit.BenefitType=InsBenefitType.Exclusions;
				if(checkCalendarYear.CheckState==CheckState.Checked) {
					benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
				}
				if(checkCalendarYear.CheckState==CheckState.Unchecked) {
					benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
				}
			}
			using FormBenefitEdit formBenefitEdit=new FormBenefitEdit(_patPlanNum,_planNum);
			formBenefitEdit.BenefitCur=benefit;
			formBenefitEdit.ShowDialog();
			if(formBenefitEdit.BenefitCur==null) {//user deleted
				_listBenefitsGrid.Remove(benefit);
				_listBenefitsAll.Remove(benefit);
			}
			else if(!otherBenefitRow.IsInList && formBenefitEdit.DialogResult==DialogResult.OK) {
				_listBenefitsGrid.Add(formBenefitEdit.BenefitCur);
				_listBenefitsAll.Add(formBenefitEdit.BenefitCur);
			}
			if(Benefits.IsAgeLimit(formBenefitEdit.BenefitCur)) {
				FillGridAgeLimits();
			}
			if(Benefits.IsFrequencyLimitation(formBenefitEdit.BenefitCur)) {
				FillGridFrequencies();
			}
			FillGridBenefits();
		}

		///<summary>Returns true if there are a mixture of benefits with calendar and service year time periods.</summary>
		private bool HasInvalidTimePeriods() {
			//Similar code can be found in FillCalendarYear()
			bool isCalendar=(!textMonth.Enabled);
			if(panelSimple.Visible) {
				for(int i=0;i<_listBenefitsAll.Count;i++) {
					if(_listBenefitsAll[i].TimePeriod==BenefitTimePeriod.CalendarYear && !isCalendar) {
						return true;
					}
					if(_listBenefitsAll[i].TimePeriod==BenefitTimePeriod.ServiceYear && isCalendar) {
						return true;
					}
				}
				return false;
			}
			for(int i=0;i<_listBenefitsGrid.Count;i++) {
				if(_listBenefitsGrid[i].TimePeriod==BenefitTimePeriod.CalendarYear && !isCalendar) {
					return true;
				}
				if(_listBenefitsGrid[i].TimePeriod==BenefitTimePeriod.ServiceYear && isCalendar) {
					return true;
				}
			}
			return false;
		}

		private void checkCalendarYear_Click(object sender,EventArgs e) {
			//checkstate will have already changed.
			//right now, change any benefits in the grid.  Upon closing, the ones in simple view will be changed.
			if(checkCalendarYear.CheckState==CheckState.Checked) {//change all to calendarYear
				textMonth.Text="";
				textMonth.Enabled=false;
				for(int i=0;i<_listBenefitsAll.Count;i++) {
					if(_listBenefitsAll[i].TimePeriod==BenefitTimePeriod.ServiceYear) {
						_listBenefitsAll[i].TimePeriod=BenefitTimePeriod.CalendarYear;
					}
				}
				FillGridBenefits();
				return;
			}
			if(checkCalendarYear.CheckState!=CheckState.Unchecked) {
				FillGridBenefits();
				return;
			}
			//change all to serviceYear
			textMonth.Enabled=true;
			for(int i=0;i<_listBenefitsAll.Count;i++) {
				if(_listBenefitsAll[i].TimePeriod==BenefitTimePeriod.CalendarYear) {
					_listBenefitsAll[i].TimePeriod=BenefitTimePeriod.ServiceYear;
				}
			}
			FillGridBenefits();
		}

		private void checkShowProcCodes_Click(object sender,EventArgs e) {
			_listBenefitsAll.RemoveAll(x=>Benefits.IsFrequencyLimitation(x) && x.CodeGroupNum!=0);
			List<Benefit> listBenefitsFreq=gridFrequencies.ListGridRows.Select(x=>(Benefit)x.Tag).ToList();
			for(int i=0;i<listBenefitsFreq.Count;i++) {
				int indexFrequencySelected=gridFrequencies.ListGridRows[i].Cells[3].ComboSelectedIndex;
				int indexTreatAreaSelected=gridFrequencies.ListGridRows[i].Cells[4].ComboSelectedIndex;
				Byte byteProvided=PIn.Byte(gridFrequencies.ListGridRows[i].Cells[2].Text, hasExceptions:false);
				bool isPatOverride=gridFrequencies.ListGridRows[i].Cells[0].Text=="X";
				if(byteProvided>0) {
					Benefit benefit=MakeFrequencyBenefit(listBenefitsFreq[i],indexFrequencySelected,indexTreatAreaSelected,byteProvided,isPatOverride);
					_listBenefitsAll.Add(benefit);
				}
			}
			FillGridFrequencies();
		}

		private void butAddBenefit_Click(object sender,EventArgs e) {
			if(!IsOtherBenefitsGridValid()) {
				return;
			}
			Benefit benefit=new Benefit();
			benefit.IsNew=true;
			benefit.PlanNum=_planNum;
			if(checkCalendarYear.CheckState==CheckState.Checked) {
				benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
			}
			if(checkCalendarYear.CheckState==CheckState.Unchecked) {
				benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
			}
			if(CovCats.GetCount(true) > 0) {
				benefit.CovCatNum=CovCats.GetFirst(true).CovCatNum;
			}
			benefit.BenefitType=InsBenefitType.CoInsurance;
			using FormBenefitEdit formBenefitEdit=new FormBenefitEdit(_patPlanNum,_planNum);
			formBenefitEdit.BenefitCur=benefit;
			formBenefitEdit.ShowDialog();
			if(formBenefitEdit.DialogResult!=DialogResult.OK) {
				FillGridBenefits();
				return;
			}
			_listBenefitsGrid.Add(formBenefitEdit.BenefitCur);
			_listBenefitsAll.Add(formBenefitEdit.BenefitCur);
			if(panelSimple.Visible && ConvertFormToBenefits(isSilent:true)) {
				FillSimple();
			}
			FillGridBenefits();
		}

		private void butClear_Click(object sender,EventArgs e) {
			//The grid selects one cell at a time (in-place editing), so Delete removes the benefit of the selected cell's row.
			List<Benefit> listBenefits=new List<Benefit>();
			int row=gridBenefits.SelectedCell.Y;
			if(row<0 || row>=gridBenefits.ListGridRows.Count) {
				if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete all benefits in list?")) {
					return;
				}
				listBenefits.AddRange(_listBenefitsGrid);
			}
			else if(gridBenefits.ListGridRows[row].Tag is OtherBenefitRow otherBenefitRow && otherBenefitRow.IsInList) {
				listBenefits.Add(otherBenefitRow.Benefit);
			}
			for(int i=0;i<listBenefits.Count;i++) {
				_listBenefitsGrid.Remove(listBenefits[i]);
				_listBenefitsAll.Remove(listBenefits[i]);
			}
			FillGridBenefits();
		}

		private void butAddFrequency_Click(object sender,EventArgs e) {
			if(!ConvertFormToBenefits()) {
				MsgBox.Show("Error generating benefits from the form.");
				return;
			}
			FormBenefitFrequencyEdit formBenefitFrequencyEdit = new FormBenefitFrequencyEdit();
			formBenefitFrequencyEdit.BenefitCur = MakeFrequencyBenefit(new Benefit(),0,0,1,false);
			formBenefitFrequencyEdit.PatPlanNum = _patPlanNum;
			formBenefitFrequencyEdit.BenefitCur.PlanNum=_planNum;//used in next window to determine calendar yr.
			Benefit benefit=gridFrequencies.SelectedTag<Benefit>();
			if(benefit!=null) {
				formBenefitFrequencyEdit.BenefitCur.CodeGroupNum=benefit.CodeGroupNum;
			}
			formBenefitFrequencyEdit.ShowDialog();
			if(formBenefitFrequencyEdit.DialogResult==DialogResult.OK) {
				_listBenefitsAll.Add(formBenefitFrequencyEdit.BenefitCur);
				FillGridFrequencies();
			}
		}

		///<summary>Only called if in simple view.  This takes all the data on the form and converts it to benefit items.  A new benefitListAll is created based on a combination of benefitList and the new items from the form.  This is used when clicking OK from simple view, or when switching from simple view to complex view, or when getting ready to view more frequencies, or when adding a new benefit.</summary>
		private bool ConvertFormToBenefits(bool isSilent=false) {
			#region Validation
			string messageText=Lan.g(this,"field is invalid.\r\n"
				+"Leave the field blank or enter an age to denote coverage through that year before clicking OK.");
			if(!textOrthoAge.IsValid()) {
				MessageBox.Show(this,labelOrthoThroughAge.Text+" "+messageText);
				return false;
			}
			if(!textAnnualMax.IsValid()
				|| !textDeductible.IsValid()
				|| !textAnnualMaxFam.IsValid()
				|| !textDeductibleFam.IsValid()
				|| !textOrthoAge.IsValid()
				|| !textOrthoMax.IsValid()
				|| !textOrthoPercent.IsValid()
				|| !textDeductDiag.IsValid()
				|| !textDeductXray.IsValid()
				|| !textDeductPrevent.IsValid()
				|| !textDeductDiagFam.IsValid()
				|| !textDeductXrayFam.IsValid()
				|| !textDeductPreventFam.IsValid()
				|| !textStand1.IsValid()
				|| !textStand2.IsValid()
				|| !textStand4.IsValid()
				|| !textDiagnostic.IsValid()
				|| !textXray.IsValid()
				|| !textRoutinePrev.IsValid()
				|| !textRestorative.IsValid()
				|| !textEndo.IsValid()
				|| !textPerio.IsValid()
				|| !textOralSurg.IsValid()
				|| !textCrowns.IsValid()
				|| !textProsth.IsValid()
				|| !textMaxProsth.IsValid()
				|| !textAccident.IsValid()
				|| !textMonth.IsValid()
				|| !textWaitRestorative.IsValid()
				|| !textWaitEndo.IsValid()
				|| !textWaitPerio.IsValid()
				|| !textWaitOralSurg.IsValid()
				|| !textWaitCrowns.IsValid()
				|| !textWaitProsth.IsValid())
			{
				if(!isSilent) {
					MsgBox.Show(this,"Please fix data entry errors first.");
				}
				return false;
			}
			if(!checkCalendarYear.Checked && textMonth.Text=="") {
				if(!isSilent) {
					MsgBox.Show(this,"Please enter a starting month for the benefit year.");
				}
				return false;
			}
			#endregion Validation
			//We need to pull from the benefits grid, from the frequencies grid, and from textboxes.
			_listBenefitsAll=new List<Benefit>(_listBenefitsGrid);
			Benefit benefit; 
			#region Annual Max
			//annual max individual
			if(textAnnualMax.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsAnnualMax(x,BenefitCoverageLevel.Individual));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Limitations;
					benefit.CovCatNum=0;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else{
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Individual;
				}
				benefit.MonetaryAmt=PIn.Double(textAnnualMax.Text);
				_listBenefitsAll.Add(benefit);
			}
			//annual max family
			if(textAnnualMaxFam.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsAnnualMax(x, BenefitCoverageLevel.Family));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Limitations;
					benefit.CovCatNum=0;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Family;
				}
				benefit.MonetaryAmt=PIn.Double(textAnnualMaxFam.Text);
				_listBenefitsAll.Add(benefit);
			}
			#endregion Annual Max
			#region Deductible
			//deductible individual
			if(textDeductible.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsGeneralDeductible(x,BenefitCoverageLevel.Individual));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Deductible;
					benefit.CovCatNum=0;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else{
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;					
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Individual;
				}
				benefit.MonetaryAmt=PIn.Double(textDeductible.Text);
				_listBenefitsAll.Add(benefit);
			}
			//deductible family
			if(textDeductibleFam.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsGeneralDeductible(x,BenefitCoverageLevel.Family));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Deductible;
					benefit.CovCatNum=0;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Family;
				}
				benefit.MonetaryAmt=PIn.Double(textDeductibleFam.Text);
				_listBenefitsAll.Add(benefit);
			}
			#endregion
			#region Age Limitation
			List<Benefit> listBenefitsAgeLim=gridAgeLimits.ListGridRows.Select(x=>(Benefit)x.Tag).ToList();
			for(int i=0;i<listBenefitsAgeLim.Count;i++) {
				byte age=PIn.Byte(gridAgeLimits.ListGridRows[i].Cells[2].Text,hasExceptions:false);
				if(age>0) {
					benefit=MakeAgeLimitBenefit(listBenefitsAgeLim[i],age);
					_listBenefitsAll.Add(benefit);
				}
			}
			#endregion Age Limitation
			#region Frequencies
			// Go through each of the entries in the grid and add the non-zero frequencies to the list of benefits.
			List<Benefit> listBenefitsFreq=gridFrequencies.ListGridRows.Select(x=>(Benefit)x.Tag).ToList();
			for(int i=0;i<listBenefitsFreq.Count;i++) {
				int indexFrequencySelected=gridFrequencies.ListGridRows[i].Cells[3].ComboSelectedIndex;
				int indexTreatAreaSelected=gridFrequencies.ListGridRows[i].Cells[4].ComboSelectedIndex;
				Byte byteProvided=PIn.Byte(gridFrequencies.ListGridRows[i].Cells[2].Text,hasExceptions:false);
				bool isPatOverride=!String.IsNullOrEmpty(gridFrequencies.ListGridRows[i].Cells[0].Text);
				if(byteProvided>0) {
					benefit=MakeFrequencyBenefit(listBenefitsFreq[i],indexFrequencySelected,indexTreatAreaSelected,byteProvided,isPatOverride);
					_listBenefitsAll.Add(benefit);
				}
			}
			#endregion Frequencies
			#region Ortho
			//ortho age
			if(textOrthoAge.Text!="") {
				benefit=ListBenefits.Find(x => Benefits.IsOrthoAgeBenefit(x));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Limitations;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Orthodontics).CovCatNum;
					benefit.PlanNum=_planNum;
					benefit.QuantityQualifier=BenefitQuantity.AgeLimit;
				}
				benefit.Quantity=PIn.Byte(textOrthoAge.Text);
				_listBenefitsAll.Add(benefit);
			}
			//ortho max
			if(textOrthoMax.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsOrthoMaxBenefit(x));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Limitations;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Orthodontics).CovCatNum;
					benefit.PlanNum=_planNum;
					benefit.TimePeriod=BenefitTimePeriod.Lifetime;
				}
				benefit.MonetaryAmt=PIn.Double(textOrthoMax.Text);
				_listBenefitsAll.Add(benefit);
			}
			//ortho percent
			if(textOrthoPercent.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsOrthoPercentBenefit(x));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Orthodontics).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textOrthoPercent.Text);
				_listBenefitsAll.Add(benefit);
			}
			#endregion Ortho
			#region Deductible
			//deductible diagnostic individual
			if(textDeductDiag.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsDiagnosticDeductibleBenefit(x,BenefitCoverageLevel.Individual));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Deductible;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Diagnostic).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Individual;
				}
				benefit.MonetaryAmt=PIn.Double(textDeductDiag.Text);
				_listBenefitsAll.Add(benefit);
			}
			//deductible diagnostic family
			if(textDeductDiagFam.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsDiagnosticDeductibleBenefit(x,BenefitCoverageLevel.Family));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Deductible;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Diagnostic).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Family;
				}
				benefit.MonetaryAmt=PIn.Double(textDeductDiagFam.Text);
				_listBenefitsAll.Add(benefit);
			}
			//deductible xray individual
			if(textDeductXray.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsXRayDeductibleBenefit(x,BenefitCoverageLevel.Individual));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Deductible;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.DiagnosticXRay).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Individual;
				}
				benefit.MonetaryAmt=PIn.Double(textDeductXray.Text);
				_listBenefitsAll.Add(benefit);
			}
			//deductible xray family
			if(textDeductXrayFam.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsXRayDeductibleBenefit(x,BenefitCoverageLevel.Family));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Deductible;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.DiagnosticXRay).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Family;
				}
				benefit.MonetaryAmt=PIn.Double(textDeductXrayFam.Text);
				_listBenefitsAll.Add(benefit);
			}
			//deductible preventive individual
			if(textDeductPrevent.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsPreventativeDeductibleBenefit(x,BenefitCoverageLevel.Individual));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Deductible;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.RoutinePreventive).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Individual;
				}
				benefit.MonetaryAmt=PIn.Double(textDeductPrevent.Text);
				_listBenefitsAll.Add(benefit);
			}
			//deductible preventive family
			if(textDeductPreventFam.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsPreventativeDeductibleBenefit(x,BenefitCoverageLevel.Family));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.Deductible;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.RoutinePreventive).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
					benefit.CoverageLevel=BenefitCoverageLevel.Family;
				}
				benefit.MonetaryAmt=PIn.Double(textDeductPreventFam.Text);
				_listBenefitsAll.Add(benefit);
			}
			//Diagnostic
			if(textDiagnostic.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.Diagnostic));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Diagnostic).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textDiagnostic.Text);
				_listBenefitsAll.Add(benefit);
			}
			//X-Ray
			if(textXray.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.DiagnosticXRay));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.DiagnosticXRay).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textXray.Text);
				_listBenefitsAll.Add(benefit);
			}
			//RoutinePreventive
			if(textRoutinePrev.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.RoutinePreventive));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.RoutinePreventive).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textRoutinePrev.Text);
				_listBenefitsAll.Add(benefit);
			}
			//Restorative
			if(textRestorative.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.Restorative));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Restorative).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textRestorative.Text);
				_listBenefitsAll.Add(benefit);
			}
			//Endo
			if(textEndo.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.Endodontics));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Endodontics).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textEndo.Text);
				_listBenefitsAll.Add(benefit);
			}
			//Perio
			if(textPerio.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.Periodontics));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Periodontics).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textPerio.Text);
				_listBenefitsAll.Add(benefit);
			}
			//OralSurg
			if(textOralSurg.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.OralSurgery));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.OralSurgery).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textOralSurg.Text);
				_listBenefitsAll.Add(benefit);
			}
			//Crowns
			if(textCrowns.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.Crowns));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Crowns).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textCrowns.Text);
				_listBenefitsAll.Add(benefit);
			}
			//Prosth
			if(textProsth.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.Prosthodontics));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Prosthodontics).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textProsth.Text);
				_listBenefitsAll.Add(benefit);
			}
			//MaxProsth
			if(textMaxProsth.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.MaxillofacialProsth));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.MaxillofacialProsth).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textMaxProsth.Text);
				_listBenefitsAll.Add(benefit);
			}
			//Accident
			if(textAccident.Text !="") {
				benefit=ListBenefits.Find(x => Benefits.IsCoInsuranceBenefit(x,EbenefitCategory.Accident));
				if(benefit==null) {
					benefit=new Benefit();
					benefit.CodeNum=0;
					benefit.CodeGroupNum=0;
					benefit.BenefitType=InsBenefitType.CoInsurance;
					benefit.CovCatNum=CovCats.GetForEbenCat(EbenefitCategory.Accident).CovCatNum;
					benefit.PlanNum=_planNum;
					if(checkCalendarYear.Checked) {
						benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
					}
					else {
						benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
					}
				}
				benefit.Percent=PIn.Int(textAccident.Text);
				_listBenefitsAll.Add(benefit);
			}
			#endregion Deductible
			#region Waiting Period
			//Waiting Periods
			//Restorative
			MakeWaitingPeriodBenefitIfNeeded(textWaitRestorative,EbenefitCategory.Restorative,_listBenefitsAll);
			//Endo
			MakeWaitingPeriodBenefitIfNeeded(textWaitEndo,EbenefitCategory.Endodontics,_listBenefitsAll);
			//Perio
			MakeWaitingPeriodBenefitIfNeeded(textWaitPerio,EbenefitCategory.Periodontics,_listBenefitsAll);
			//OralSurg
			MakeWaitingPeriodBenefitIfNeeded(textWaitOralSurg,EbenefitCategory.OralSurgery,_listBenefitsAll);
			//Crowns
			MakeWaitingPeriodBenefitIfNeeded(textWaitCrowns,EbenefitCategory.Crowns,_listBenefitsAll);
			//Prosth
			MakeWaitingPeriodBenefitIfNeeded(textWaitProsth,EbenefitCategory.Prosthodontics,_listBenefitsAll);
			#endregion Waiting Period
			return true;
		}

		///<summary>Copies the passed in benefit. If existing benefit then will just update during sync.</summary>
		private Benefit MakeAgeLimitBenefit(Benefit benefit, byte quantity) {
			Benefit benefitNew=benefit.Copy();
			benefitNew.BenefitType=InsBenefitType.Limitations;
			benefitNew.MonetaryAmt=-1;
			benefitNew.PatPlanNum=0;
			benefitNew.Percent=-1;
			benefitNew.QuantityQualifier=BenefitQuantity.AgeLimit;
			benefitNew.CoverageLevel=BenefitCoverageLevel.None;
			benefitNew.CodeNum=0;
			benefitNew.CovCatNum=0;
			benefitNew.IsNew=true;
			benefitNew.PlanNum=_planNum;
			benefitNew.Quantity=quantity;
			benefitNew.IsNew=false;
			return benefitNew;
		}

		///<summary>Copies the passed in benefit. If existing benefit then will just update during sync.</summary>
		private Benefit MakeFrequencyBenefit(Benefit benefit, int indexComboFrequency, int indexComboTreatArea, byte quantity, bool isPatOverride) {
			Benefit benefitNew=benefit.Copy();
			benefitNew.BenefitType=InsBenefitType.Limitations;
			benefitNew.MonetaryAmt=-1;
			benefitNew.Percent=-1;
			benefitNew.CodeNum=0;
			benefitNew.CovCatNum=0;
			benefitNew.IsNew=true;
			benefitNew.PlanNum=_planNum;
			benefitNew.PatPlanNum=0;
			if(isPatOverride) {
				benefitNew.PatPlanNum=_patPlanNum;
				benefitNew.PlanNum=0;
			} 
			benefitNew.SetFrequencyOption((FrequencyOptions)indexComboFrequency,checkCalendarYear.Checked);
			benefitNew.TreatArea=(TreatmentArea)indexComboTreatArea;
			benefitNew.Quantity=quantity;
			benefitNew.IsNew=false;
			return benefitNew;
		}

		/// <summary>Only used to gather data from the textboxes.</summary>
		private void MakeWaitingPeriodBenefitIfNeeded(ValidNum validNum,EbenefitCategory ebenefitCategory,List<Benefit> listBenefits) {
			if(string.IsNullOrWhiteSpace(validNum.Text)) {
				return;
			}
			Benefit benefit=ListBenefits.Find(x => Benefits.IsWaitingPeriodBenefit(x,ebenefitCategory));
			if(benefit==null) {
				benefit=new Benefit();
				benefit.CodeNum=0;
				benefit.CodeGroupNum=0;
				benefit.BenefitType=InsBenefitType.WaitingPeriod;
				benefit.CovCatNum=CovCats.GetForEbenCat(ebenefitCategory).CovCatNum;
				benefit.PlanNum=_planNum;
				benefit.QuantityQualifier=BenefitQuantity.Months;
				if(checkCalendarYear.Checked) {
					benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
				}
				else {
					benefit.TimePeriod=BenefitTimePeriod.ServiceYear;
				}
			}
			benefit.Quantity=PIn.Byte(validNum.Text);
			listBenefits.Add(benefit);
		}

		private void butSave_Click(object sender, System.EventArgs e) {
			if(!checkCalendarYear.Checked && textMonth.Text=="") {
				MsgBox.Show(this,"Please enter a starting month for the benefit year.");
				return;
			}
			if(!IsOtherBenefitsGridValid()) {
				return;
			}
			//Benefits of retired code groups follow the plan's benefit year like every other benefit, then are saved back with the rest.
			for(int i=0;i<_listBenefitsRetired.Count;i++) {
				if(checkCalendarYear.Checked && _listBenefitsRetired[i].TimePeriod==BenefitTimePeriod.ServiceYear) {
					_listBenefitsRetired[i].TimePeriod=BenefitTimePeriod.CalendarYear;
				}
				if(!checkCalendarYear.Checked && _listBenefitsRetired[i].TimePeriod==BenefitTimePeriod.CalendarYear) {
					_listBenefitsRetired[i].TimePeriod=BenefitTimePeriod.ServiceYear;
				}
			}
			if(panelSimple.Visible) {
				List<string> listCodeGroupErrors=new List<string>();
				if(listCodeGroupErrors.Count > 0) {
					string error=Lan.g(this,"The following fields have invalid or missing Fixed Code Group(s):")
						+"\r\n  "+string.Join("\r\n  ",listCodeGroupErrors)
						+"\r\n"+Lan.g(this,"Go to Setup | Code Groups to fix or create the Fixed Code Group(s).")
						+"\r\n"+Lan.g(this,"Uncheck Simplified View to save changes anyway.");
					MsgBox.Show(error);
					return;
				}
				if(!ConvertFormToBenefits()) {
					return;
				}
				if(HasInvalidTimePeriods()) {
					MsgBox.Show(this,"A mixture of calendar and service year time periods are not allowed.");
					return;
				}
				_listBenefitsAll.AddRange(_listBenefitsRetired);
				//OriginalBenList.Clear();
				//for(int i=0;i<benefitListAll.Count;i++) {
				//	OriginalBenList.Add(benefitListAll[i]);
				//}
				//We can't just clear the list.  Then, we wouldn't be able to test it for most efficient db queries.
				for(int i=ListBenefits.Count-1;i>=0;i--) {//loop through the old list, backwards.
					bool matchFound=false;
					for(int j=0;j<_listBenefitsAll.Count;j++) {
						if(_listBenefitsAll[j].IsSimilar(ListBenefits[i])) {
							matchFound=true;
						}
					}
					if(!matchFound) {//If no match is found in the new list
						//delete the entry from the old list.  That will cause a deletion from the db later.
						ListBenefits.RemoveAt(i);
					}
				}
				for(int j=0;j<_listBenefitsAll.Count;j++) {//loop through the new list.
					bool matchFound=false;
					for(int i=0;i<ListBenefits.Count;i++) {
						if(_listBenefitsAll[j].IsSimilar(ListBenefits[i])) {
							matchFound=true;
						}
					}
					if(!matchFound) {//If no match is found in the old list
						//add the entry to the old list.  This will cause an insert because BenefitNum will be 0.
						ListBenefits.Add(_listBenefitsAll[j]);
					}
				}
			}
			else {//not simple view.  Will optimize this later for speed.  Should be easier.
				if(HasInvalidTimePeriods()) {
					MsgBox.Show(this,"A mixture of calendar and service year time periods are not allowed.");
					return;
				}
				ListBenefits.Clear();
				for(int i=0;i<_listBenefitsGrid.Count;i++) {
					ListBenefits.Add(_listBenefitsGrid[i]);
				}
				ListBenefits.AddRange(_listBenefitsRetired);
			}
			if(checkCalendarYear.Checked) {
				MonthRenew=0;
			}
			else {
				MonthRenew=PIn.Byte(textMonth.Text);
			}
			Note=textSubscNote.Text;
			DialogResult=DialogResult.OK;
		}

		///<summary>The tag of a row of the Other Benefits grid: the benefit it shows, and whether that benefit is in the window's lists yet. A
		///pre-filled code group row holds a new benefit that joins the lists once the user enters valid values.</summary>
		private class OtherBenefitRow {
			public Benefit Benefit;
			public bool IsInList;
			///<summary>The row's cells as they were when the benefit last matched them (see GetRowSignature).</summary>
			public string SignatureApplied="";

			public OtherBenefitRow(Benefit benefit,bool isInList) {
				Benefit=benefit;
				IsInList=isInList;
			}
		}
	}
}