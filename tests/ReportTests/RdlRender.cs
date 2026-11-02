// Renders RDL report definitions the way the program does (package RDL-SRC, docs/port-notes/RDL-SRC.md): through RdlEngine's RDLParser,
// Report.RunGetData and Report.BuildPages, as RdlViewer.GetReport and GetPages call them, or through the RdlViewer control itself, as
// OpenPmsDental/ReportingForRdl/FormReportForRdl.cs uses it. ODR.Query asks for report parameters in a modal window (ODR.FormParameterInput); the
// render thread fills that window in from its message loop and presses OK, so no one has to.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using fyiReporting.RDL;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ReportTests {
	///<summary>What one render produced.</summary>
	public class RdlRenderResult {
		public int PageCount;
		///<summary>The text of every text item on every page, in page order.</summary>
		public List<string> ListTexts=new List<string>();
		///<summary>Messages the engine logged while parsing (any severity).</summary>
		public List<string> ListParseMessages=new List<string>();
		public int ParseMaxSeverity;
		///<summary>Messages the engine logged while running the queries and building the pages (any severity).</summary>
		public List<string> ListRunMessages=new List<string>();
		public int RunMaxSeverity;
		///<summary>How many times the ODR parameter window opened.</summary>
		public int CountParameterWindows;
		///<summary>The values typed into the parameter window, by its prompt.</summary>
		public Dictionary<string,string> DictParameterPrompts=new Dictionary<string,string>();

		public string Describe() {
			return "pages="+PageCount+", parse severity "+ParseMaxSeverity+": ["+string.Join(" | ",ListParseMessages)+"], run severity "+RunMaxSeverity
				+": ["+string.Join(" | ",ListRunMessages)+"], texts: ["+string.Join(" | ",ListTexts.Take(80))+"]";
		}
	}

	public static class RdlRender {
		///<summary>A render that has not finished by then is stuck in a window nobody closes (for example ODR's MessageBox after a failed query).
		///</summary>
		private const int TIMEOUT_MS=180000;

		///<summary>The report definition as the program embeds it: OpenPmsDental\Resources\fileName (Properties\Resources.resx reads these files as
		///Windows-1252 text).</summary>
		public static string ReadProgramReport(string fileName) {
			string path=ForkMigrationTests.TestDb.FindRepoFile(Path.Combine("OpenPmsDental","Resources",fileName));
			return File.ReadAllText(path,Encoding.GetEncoding(1252));
		}

		///<summary>Parses, runs and builds the pages with the engine API, as RdlViewer does. arrayParameterValues are typed into the ODR parameter
		///window's boxes in order, if it opens.</summary>
		public static RdlRenderResult RenderWithEngine(string rdl,params string[] arrayParameterValues) {
			return RunOnStaThread(result => {
				RDLParser rdlParser=new RDLParser(rdl);
				rdlParser.Folder=AppDomain.CurrentDomain.BaseDirectory;
				Report report=rdlParser.Parse();
				result.ParseMaxSeverity=report.ErrorMaxSeverity;
				result.ListParseMessages.AddRange(ToStrings(report.ErrorItems));
				report.ErrorReset();
				report.RunGetData(new ListDictionary());
				Pages pages=report.BuildPages();
				result.RunMaxSeverity=report.ErrorMaxSeverity;
				result.ListRunMessages.AddRange(ToStrings(report.ErrorItems));
				result.PageCount=pages.PageCount;
				foreach(Page page in pages) {
					foreach(PageItem pageItem in page) {
						if(pageItem is PageText pageText) {
							result.ListTexts.Add(pageText.Text??"");
						}
					}
				}
			},arrayParameterValues);
		}

		///<summary>Loads the report into the RdlViewer control the way FormReportForRdl does (SourceRdl, ShowParameterPanel=false, Rebuild) and
		///returns its page count and the messages the viewer kept (it never shows them with the parameter panel hidden).</summary>
		public static RdlRenderResult RenderWithViewer(string rdl,params string[] arrayParameterValues) {
			return RunOnStaThread(result => {
				using(fyiReporting.RdlViewer.RdlViewer viewer=new fyiReporting.RdlViewer.RdlViewer()) {
					viewer.SourceRdl=rdl;
					viewer.ShowParameterPanel=false;
					viewer.Rebuild();
					result.PageCount=viewer.PageCount;
					Assert.IsNotNull(viewer.Report,"The viewer has no report.");
					FieldInfo fieldInfo=typeof(fyiReporting.RdlViewer.RdlViewer).GetField("_errorMsgs",BindingFlags.Instance|BindingFlags.NonPublic);
					Assert.IsNotNull(fieldInfo,"RdlViewer._errorMsgs");
					result.ListRunMessages.AddRange(ToStrings((IList)fieldInfo.GetValue(viewer)));
				}
			},arrayParameterValues);
		}

		private static List<string> ToStrings(IList list) {
			if(list==null) {
				return new List<string>();
			}
			return list.Cast<object>().Select(x => x?.ToString()??"").ToList();
		}

		///<summary>Runs the render on its own STA thread (WinForms, and ShowDialog's message loop for the parameter window). The current directory is
		///this folder meanwhile, where the program's would be its own folder: RdlEngine loads ODR.dll and MySqlConnector.dll by file name from it.
		///</summary>
		private static RdlRenderResult RunOnStaThread(Action<RdlRenderResult> actionRender,string[] arrayParameterValues) {
			RdlRenderResult result=new RdlRenderResult();
			Exception exception=null;
			Thread thread=new Thread(() => {
				RdlReportDb.UseUsEnglish();
				EventHandler eventHandlerIdle=(sender,e) => FillParameterWindow(result,arrayParameterValues);
				Application.Idle+=eventHandlerIdle;
				try {
					actionRender(result);
				}
				catch(Exception ex) {
					exception=ex;
				}
				finally {
					Application.Idle-=eventHandlerIdle;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground=true;
			string currentDirectory=Environment.CurrentDirectory;
			Environment.CurrentDirectory=AppDomain.CurrentDomain.BaseDirectory;
			try {
				thread.Start();
				if(!thread.Join(TIMEOUT_MS)) {
					Assert.Fail("The report did not finish within "+(TIMEOUT_MS/1000)+" s; a window is probably waiting for input. So far: "+result.Describe());
				}
			}
			finally {
				Environment.CurrentDirectory=currentDirectory;
			}
			if(exception!=null) {
				throw new AssertFailedException("Rendering threw: "+exception,exception);
			}
			return result;
		}

		///<summary>Runs on the render thread whenever its message loop is idle. When ODR's parameter window is up and laid out, types the values into
		///its input boxes in order and presses OK, as a user would.</summary>
		private static void FillParameterWindow(RdlRenderResult result,string[] arrayParameterValues) {
			List<Form> listForms=Application.OpenForms.Cast<Form>().Where(x => x.GetType().FullName=="ODR.FormParameterInput" && x.Visible).ToList();
			foreach(Form form in listForms) {
				if(form.Tag as string=="filled") {
					continue;
				}
				Control panelMain=form.Controls.Find("panelMain",false).FirstOrDefault();
				if(panelMain==null || panelMain.Controls.Count==0) {
					continue;//not laid out yet
				}
				List<Control> listLabels=panelMain.Controls.Cast<Control>().Where(x => x is Label).ToList();
				List<Control> listInputs=panelMain.Controls.Cast<Control>().Where(x => !(x is Label)).ToList();
				for(int i=0;i<listInputs.Count;i++) {
					string value=i<arrayParameterValues.Length ? arrayParameterValues[i] : "";
					listInputs[i].Text=value;
					result.DictParameterPrompts[i<listLabels.Count ? listLabels[i].Text : "#"+i]=value;
				}
				form.Tag="filled";
				result.CountParameterWindows++;
				Button butOK=(Button)form.Controls.Find("butOK",true).First();
				if(butOK.CanSelect) {
					butOK.PerformClick();
				}
				else {//PerformClick does nothing for a button that cannot take the focus yet
					typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(butOK,new object[] { EventArgs.Empty });
				}
			}
		}
	}
}
