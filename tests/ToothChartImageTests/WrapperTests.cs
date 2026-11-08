// Tests of the small wrapper and relay changes of package TC-0: the graphics device exception test and the relay's perio mode setter.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using SparksToothChart;

namespace ToothChartImageTests {
	[TestClass]
	public class WrapperTests {
		///<summary>SharpDX's exception type, from the assembly SparksToothChart references. This project does not reference SharpDX itself, so the
		///callers' code after TC-2 and this test both work without naming it.</summary>
		private static Type GetSharpDXType(string typeName) {
			AssemblyName assemblyName=typeof(ToothChartWrapper).Assembly.GetReferencedAssemblies().First(x => x.Name=="SharpDX");
			return Assembly.Load(assemblyName).GetType(typeName,true);
		}

		[TestMethod]
		public void IsGraphicsDeviceException_IsTrueForSharpDXExceptionsOnly() {
			Exception exSharpDX=(Exception)Activator.CreateInstance(GetSharpDXType("SharpDX.SharpDXException"));
			Assert.IsTrue(ToothChartWrapper.IsGraphicsDeviceException(exSharpDX));
			Exception exCompilation=(Exception)Activator.CreateInstance(GetSharpDXType("SharpDX.CompilationException"),
				BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new object[] { "x" },null);
			Assert.IsTrue(ToothChartWrapper.IsGraphicsDeviceException(exCompilation),"A subclass is caught too, as catch(SharpDXException) catches it.");
			Assert.IsFalse(ToothChartWrapper.IsGraphicsDeviceException(null));
			Assert.IsFalse(ToothChartWrapper.IsGraphicsDeviceException(new Exception("device")));
			Assert.IsFalse(ToothChartWrapper.IsGraphicsDeviceException(new InvalidOperationException()));
			Assert.IsFalse(ToothChartWrapper.IsGraphicsDeviceException(new NullReferenceException()));
			Assert.IsFalse(ToothChartWrapper.IsGraphicsDeviceException(new DllNotFoundException(ToothChartWrapper.D3DX_DLL)),
				"A missing DirectX runtime is the fallback's matter, not a device error.");
			Assert.IsFalse(ToothChartWrapper.IsGraphicsDeviceException(new Exception("outer",exSharpDX)),
				"Only the thrown type counts, as with catch(SharpDXException).");
		}
	}
}
