// Tests for the tooth chart's draw mode fallback (package BIN-A, docs/port-notes/BIN-A.md). SparksToothChart is the only chart now. Its DirectX
// mode needs d3dx9_43.dll from Microsoft's legacy DirectX End-User Runtime, which a clean Windows lacks, so a mode that cannot start falls back to
// the next one (DirectX, OpenGL, Simple 2D) without an error dialog and is logged once. The fallback choice is tested through
// ToothChartWrapper.StartFirstWorkingMode with fake starters, so no window or graphics device is needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.SheetFramework;
using SparksToothChart;

namespace BINATests {
	[TestClass]
	public class ToothChartFallbackTests {
		///<summary>Runs StartFirstWorkingMode with a starter that fails for the modes in arrayFailing (throwing for DirectX, as a missing D3DX does,
		///and returning false otherwise). Returns the started mode; listTried and listLog get what happened.</summary>
		private static DrawingMode Run(DrawingMode drawingModeRequested,bool isDirectXRuntimeAvailable,DrawingMode[] arrayFailing,
			List<DrawingMode> listTried,List<string> listLog)
		{
			return ToothChartWrapper.StartFirstWorkingMode(drawingModeRequested,isDirectXRuntimeAvailable,drawingMode => {
				listTried.Add(drawingMode);
				if(Array.IndexOf(arrayFailing,drawingMode)<0) {
					return true;
				}
				if(drawingMode==DrawingMode.DirectX) {
					throw new DllNotFoundException("Unable to load DLL 'd3dx9_43.dll'");
				}
				return false;
			},listLog.Add);
		}

		[TestMethod]
		public void FallbackOrder_IsDirectXThenOpenGLThenSimple2D() {
			Assert.AreEqual(DrawingMode.OpenGL,ToothChartWrapper.GetFallbackDrawMode(DrawingMode.DirectX));
			Assert.AreEqual(DrawingMode.Simple2D,ToothChartWrapper.GetFallbackDrawMode(DrawingMode.OpenGL));
			Assert.AreEqual(DrawingMode.Simple2D,ToothChartWrapper.GetFallbackDrawMode(DrawingMode.Simple2D),"Simple 2D is the last resort.");
		}

		[TestMethod]
		public void MissingDirectXRuntime_SkipsDirectXAndStartsOpenGL() {
			List<DrawingMode> listTried=new List<DrawingMode>();
			List<string> listLog=new List<string>();
			DrawingMode drawingMode=Run(DrawingMode.DirectX,false,new DrawingMode[0],listTried,listLog);
			Assert.AreEqual(DrawingMode.OpenGL,drawingMode);
			CollectionAssert.AreEqual(new[] { DrawingMode.OpenGL },listTried,"DirectX is not even tried without d3dx9_43.dll.");
			Assert.AreEqual(1,listLog.Count);
			StringAssert.Contains(listLog[0],ToothChartWrapper.D3DX_DLL);
			Assert.AreEqual("d3dx9_43.dll",ToothChartWrapper.D3DX_DLL);
		}

		[TestMethod]
		public void MissingDirectXRuntimeAndNoOpenGL_EndsInSimple2D() {
			List<DrawingMode> listTried=new List<DrawingMode>();
			List<string> listLog=new List<string>();
			DrawingMode drawingMode=Run(DrawingMode.DirectX,false,new[] { DrawingMode.OpenGL },listTried,listLog);
			Assert.AreEqual(DrawingMode.Simple2D,drawingMode);
			CollectionAssert.AreEqual(new[] { DrawingMode.OpenGL,DrawingMode.Simple2D },listTried);
			Assert.AreEqual(2,listLog.Count);
		}

		[TestMethod]
		public void DirectXThatThrows_FallsBackToOpenGL() {
			List<DrawingMode> listTried=new List<DrawingMode>();
			List<string> listLog=new List<string>();
			DrawingMode drawingMode=Run(DrawingMode.DirectX,true,new[] { DrawingMode.DirectX },listTried,listLog);
			Assert.AreEqual(DrawingMode.OpenGL,drawingMode);
			CollectionAssert.AreEqual(new[] { DrawingMode.DirectX,DrawingMode.OpenGL },listTried);
			Assert.AreEqual(1,listLog.Count);
			StringAssert.Contains(listLog[0],"DllNotFoundException","The log says why DirectX did not start.");
			StringAssert.Contains(listLog[0],"OpenGL");
		}

		[TestMethod]
		public void DirectXAndOpenGLFailing_EndsInSimple2D() {
			List<DrawingMode> listTried=new List<DrawingMode>();
			List<string> listLog=new List<string>();
			DrawingMode drawingMode=Run(DrawingMode.DirectX,true,new[] { DrawingMode.DirectX,DrawingMode.OpenGL },listTried,listLog);
			Assert.AreEqual(DrawingMode.Simple2D,drawingMode);
			CollectionAssert.AreEqual(new[] { DrawingMode.DirectX,DrawingMode.OpenGL,DrawingMode.Simple2D },listTried);
			Assert.AreEqual(2,listLog.Count);
		}

		[TestMethod]
		public void WorkingModes_StartAsRequested_WithoutLogging() {
			foreach(DrawingMode drawingModeRequested in new[] { DrawingMode.DirectX,DrawingMode.OpenGL,DrawingMode.Simple2D }) {
				List<DrawingMode> listTried=new List<DrawingMode>();
				List<string> listLog=new List<string>();
				Assert.AreEqual(drawingModeRequested,Run(drawingModeRequested,true,new DrawingMode[0],listTried,listLog));
				CollectionAssert.AreEqual(new[] { drawingModeRequested },listTried);
				Assert.AreEqual(0,listLog.Count);
			}
		}

		[TestMethod]
		public void OpenGLRequested_IsNotAffectedByAMissingDirectXRuntime() {
			List<DrawingMode> listTried=new List<DrawingMode>();
			List<string> listLog=new List<string>();
			Assert.AreEqual(DrawingMode.OpenGL,Run(DrawingMode.OpenGL,false,new DrawingMode[0],listTried,listLog));
			Assert.AreEqual(0,listLog.Count);
		}

		[TestMethod]
		public void Simple2DFailure_IsNotSwallowed() {
			//Simple 2D is the last resort, so its failure surfaces as before instead of leaving a chart with no control.
			try {
				ToothChartWrapper.StartFirstWorkingMode(DrawingMode.Simple2D,true,drawingMode => throw new InvalidOperationException("2D failed"),null);
				Assert.Fail("Expected the Simple 2D failure to surface.");
			}
			catch(InvalidOperationException ex) {
				Assert.AreEqual("2D failed",ex.Message);
			}
		}

		[TestMethod]
		public void DirectXRuntimeCheck_FindsTheInstalledFile() {
			bool isAvailable=ToothChartWrapper.IsDirectXRuntimeAvailable();
			Assert.AreEqual(isAvailable,ToothChartWrapper.IsDirectXRuntimeAvailable(),"Checked once per process.");
			//This test process is 32-bit, so Windows loads 32-bit system files from the SysWOW64 folder (System32 on 32-bit Windows).
			string pathSystem=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),ToothChartWrapper.D3DX_DLL);
			if(File.Exists(pathSystem)) {
				Assert.IsTrue(isAvailable,pathSystem+" is installed, so DirectX can start.");
			}
		}

		[TestMethod]
		public void Wrapper_UsesTheFallbackWithoutADialog() {
			string wrapper=RepoFiles.Read(@"SparksToothChart\ToothChartWrapper.cs");
			StringAssert.Contains(wrapper,"drawMode=StartFirstWorkingMode(value,IsDirectXRuntimeAvailable(),TryStartDrawMode,LogFallbackOnce);");
			StringAssert.DoesNotMatch(wrapper,new Regex(@"MessageBox\.|MsgBox\."),"A fallback never shows a dialog.");
			//Callers that test DirectX must treat any other mode as failure, because the chart may now fall back to OpenGL instead of 2D.
			string graphics=RepoFiles.Read(@"OpenDental\Forms\FormGraphics.cs");
			StringAssert.Contains(graphics,"if(toothChartWrapperTest.DrawMode!=DrawingMode.DirectX) {");
			StringAssert.Contains(graphics,"if(!ToothChartWrapper.IsDirectXRuntimeAvailable()) {");
			//A computer with no working DirectX format is set to the next mode, OpenGL, rather than straight to Simple 2D.
			StringAssert.Contains(RepoFiles.Read(@"OpenDental\Main Modules\FormOpenDental.cs"),
				"ComputerPrefs.LocalComputer.GraphicsSimple=SparksToothChart.ToothChartWrapper.GetFallbackDrawMode(DrawingMode.DirectX);");
		}

		[TestMethod]
		public void BusinessLayerChartImage_IsAPlainImageOfTheRightSize() {
			//The business layer's tooth chart image was drawn by Sparks3D. It is now a plain image that says so, at the size callers expect.
			using Image image=ToothChartHelper.GetImageNotAvailable(500,370,Color.White,Color.Black);
			Assert.AreEqual(new Size(500,370),image.Size);
			Assert.AreEqual(Color.White.ToArgb(),((Bitmap)image).GetPixel(0,0).ToArgb());
			using Image imagePerio=ToothChartHelper.GetToothChartImageProcess(null,new List<Procedure>(),new List<ToothInitial>(),
				OpenDentBusiness.ToothNumberingNomenclature.Universal,new List<PerioMeasure>(),new Dictionary<PrefName,Color>(),new List<Def>(),
				new List<Procedure>(),new List<ProcedureCode>(),new List<ProcedureCode>(),false,false,true,500,370,false,false);
			Assert.AreEqual(new Size(649,696),imagePerio.Size,"The perio image keeps the graphical perio chart's size.");
		}
	}
}
