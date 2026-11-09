// A second process for the WPF chart's tests (package TC-1): the determinism test's second render, the process-exit test and the cold render times.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CSharp;
using OpenPmsDental;
using SparksToothChart;

namespace ToothChartImageTests {
	///<summary>What a second process did.</summary>
	public class SecondProcessResult {
		public int ExitCode;
		public bool IsTimedOut;
		public string Output;
		public double MillisecondsElapsed;
	}

	///<summary>Runs SecondProcessEntry.Run in a new 32-bit process. The test assembly is a library, so a tiny console program is compiled for each run
	///with the .NET Framework's own C# compiler into the test assembly's folder (so it finds the same assemblies, with the test assembly's binding
	///redirects as its config), started with no window, and deleted afterwards. Its Main is [MTAThread]: the WPF chart needs no STA caller.</summary>
	public static class SecondProcess {
		public static SecondProcessResult Run(int millisecondsTimeout,params string[] arrayArgs) {
			string folder=Path.GetDirectoryName(typeof(SecondProcessEntry).Assembly.Location);
			string name="ToothChartSecondProcess-"+Guid.NewGuid().ToString("N").Substring(0,8);
			string pathExe=Path.Combine(folder,name+".exe");
			string pathConfig=pathExe+".config";
			try {
				Compile(pathExe);
				string pathConfigTests=typeof(SecondProcessEntry).Assembly.Location+".config";
				if(File.Exists(pathConfigTests)) {
					File.Copy(pathConfigTests,pathConfig,true);
				}
				ProcessStartInfo processStartInfo=new ProcessStartInfo(pathExe,string.Join(" ",arrayArgs.Select(x => "\""+x+"\"")));
				processStartInfo.UseShellExecute=false;
				processStartInfo.CreateNoWindow=true;
				processStartInfo.RedirectStandardOutput=true;
				processStartInfo.RedirectStandardError=true;
				processStartInfo.WorkingDirectory=folder;
				SecondProcessResult result=new SecondProcessResult();
				Stopwatch stopwatch=Stopwatch.StartNew();
				using(Process process=Process.Start(processStartInfo)) {
					StringBuilder stringBuilder=new StringBuilder();
					object lockOutput=new object();
					process.OutputDataReceived+=(sender,e) => { if(e.Data!=null) { lock(lockOutput) { stringBuilder.AppendLine(e.Data); } } };
					process.ErrorDataReceived+=(sender,e) => { if(e.Data!=null) { lock(lockOutput) { stringBuilder.AppendLine("stderr: "+e.Data); } } };
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();
					if(!process.WaitForExit(millisecondsTimeout)) {
						result.IsTimedOut=true;
						try {
							process.Kill();//the process this test started, and nothing else
						}
						catch(Exception ex) {
							ex.ToString();
						}
						process.WaitForExit(10000);
					}
					else {
						process.WaitForExit();//the output is read to its end
					}
					result.MillisecondsElapsed=stopwatch.Elapsed.TotalMilliseconds;
					result.ExitCode=result.IsTimedOut?-1:process.ExitCode;
					lock(lockOutput) {
						result.Output=stringBuilder.ToString();
					}
				}
				return result;
			}
			finally {
				DeleteQuietly(pathExe);
				DeleteQuietly(pathConfig);
			}
		}

		private static void Compile(string pathExe) {
			string source="using System;\r\n"
				+"static class Program {\r\n"
				+"	[MTAThread]\r\n"
				+"	static int Main(string[] args) {\r\n"
				+"		return ToothChartImageTests.SecondProcessEntry.Run(args);\r\n"
				+"	}\r\n"
				+"}\r\n";
			using(CSharpCodeProvider cSharpCodeProvider=new CSharpCodeProvider()) {
				CompilerParameters compilerParameters=new CompilerParameters(new[] { "System.dll",typeof(SecondProcessEntry).Assembly.Location });
				compilerParameters.GenerateExecutable=true;
				compilerParameters.GenerateInMemory=false;
				compilerParameters.IncludeDebugInformation=false;
				compilerParameters.OutputAssembly=pathExe;
				compilerParameters.CompilerOptions="/platform:x86";
				CompilerResults compilerResults=cSharpCodeProvider.CompileAssemblyFromSource(compilerParameters,source);
				if(compilerResults.Errors.HasErrors) {
					throw new InvalidOperationException("The second process could not be compiled: "
						+string.Join("; ",compilerResults.Errors.Cast<CompilerError>().Select(x => x.ErrorText)));
				}
			}
		}

		private static void DeleteQuietly(string path) {
			for(int i=0;i<20;i++) {
				try {
					if(File.Exists(path)) {
						File.Delete(path);
					}
					return;
				}
				catch(IOException) {
					System.Threading.Thread.Sleep(100);//the process may still hold its file for a moment after it exits
				}
				catch(UnauthorizedAccessException) {
					System.Threading.Thread.Sleep(100);
				}
			}
		}
	}

	///<summary>The second process's work. No MSTest type is touched here, so the program needs only the chart's assemblies.</summary>
	public static class SecondProcessEntry {
		///<summary>args[0] is the mode:
		///"hash &lt;scene&gt;": renders the scene at 410x307 with the module colours and prints its pixel SHA-256.
		///"exit": renders one image and returns at once, leaving the render thread alive; the process must still end.
		///"times &lt;out file&gt;": measures the render times for O-19 (WpfTimings) and writes the report.</summary>
		public static int Run(string[] args) {
			try {
				string mode=args.Length>0?args[0]:"";
				switch(mode) {
					case "hash":
						Console.WriteLine("SHA256 "+RenderHash(args[1]));
						return 0;
					case "exit":
						using(Bitmap bitmap=RenderOne()) {
							Console.WriteLine("Rendered "+bitmap.Width+"x"+bitmap.Height+"; render thread alive: "
								+SparksToothChart.Wpf3D.ToothChartRenderer.Instance.IsThreadAlive);
						}
						return 0;//no shutdown: the background render thread must not keep the process alive
					case "times":
						string report=WpfTimings.Measure(true).GetReport();
						File.WriteAllText(args[1],report,new UTF8Encoding(false));
						Console.WriteLine(report);
						return 0;
					default:
						Console.WriteLine("Unknown mode "+mode);
						return 2;
				}
			}
			catch(Exception ex) {
				Console.WriteLine("Failed: "+ex);
				return 1;
			}
		}

		public static string RenderHash(string sceneName) {
			ChartScene scene=ChartScenes.Get(sceneName);
			using(ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size)) {
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				using(Bitmap bitmap=WpfCharts.Draw(relay,scene,ChartColorSet.Module)) {
					if(wrapper.Engine!=ToothChartEngine.Wpf3D) {
						throw new InvalidOperationException("The chart fell back to "+wrapper.Engine+".");
					}
					return ChartImageCompare.Sha256(ChartImageCompare.GetPixelsArgb(bitmap));
				}
			}
		}

		private static Bitmap RenderOne() {
			ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
			ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
			return WpfCharts.Draw(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
		}
	}
}
