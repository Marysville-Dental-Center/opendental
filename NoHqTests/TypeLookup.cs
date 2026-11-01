// Type lookups for the absence tests that keep checking after the namespaces are renamed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NoHqTests {
	///<summary>Finds types by a name relative to the namespace of a type the test names with typeof, never by a namespace written out as text. An
	///absence check written as GetType("OldNamespace.Removed") would still pass after a rename, because nothing has that name any more, so
	///every check here first asserts that a type known to exist resolves the same way.</summary>
	internal static class TypeLookup {
		///<summary>Asserts that typeKnown resolves by its namespace plus its name in its own assembly, then that none of the names resolve. Each name
		///is relative to typeKnown's namespace, for example "Jobs" or "Crud.JobCrud".</summary>
		public static void AssertRemoved(Type typeKnown,IEnumerable<string> relativeNames,string why) {
			Assembly assembly=AssertKnownResolves(typeKnown);
			foreach(string relativeName in relativeNames) {
				string fullName=typeKnown.Namespace+"."+relativeName;
				Assert.IsNull(assembly.GetType(fullName),fullName+" "+why);
			}
		}

		///<summary>Asserts that typeKnown resolves, then that every name (relative to typeKnown's namespace) resolves in its assembly.</summary>
		public static void AssertKept(Type typeKnown,IEnumerable<string> relativeNames,string why) {
			Assembly assembly=AssertKnownResolves(typeKnown);
			foreach(string relativeName in relativeNames) {
				string fullName=typeKnown.Namespace+"."+relativeName;
				Assert.IsNotNull(assembly.GetType(fullName),fullName+" "+why);
			}
		}

		///<summary>The namespace of the program's entry point (ProgramEntry), which the windows of the exe share. Asserts that the entry point's
		///type resolves by that namespace.</summary>
		public static string GetEntryNamespace(Assembly assemblyExe) {
			Assert.IsNotNull(assemblyExe.EntryPoint,assemblyExe.GetName().Name+" has no entry point.");
			Type typeEntry=assemblyExe.EntryPoint.DeclaringType;
			Assert.AreEqual(typeEntry,assemblyExe.GetType(typeEntry.Namespace+"."+typeEntry.Name),"The entry point's type does not resolve by its namespace.");
			return typeEntry.Namespace;
		}

		///<summary>The namespace of the one top-level type with this simple name in the assembly (for an assembly the test cannot reference).</summary>
		public static string GetNamespaceOf(Assembly assembly,string simpleName) {
			Type[] arrayTypes;
			try {
				arrayTypes=assembly.GetTypes();
			}
			catch(ReflectionTypeLoadException ex) {
				arrayTypes=ex.Types.Where(x => x!=null).ToArray();//A type whose dependency is missing here does not matter for the lookup.
			}
			List<Type> listTypes=arrayTypes.Where(x => x.Name==simpleName && !x.IsNested).ToList();
			Assert.AreEqual(1,listTypes.Count,"Expected one type named "+simpleName+" in "+assembly.GetName().Name+".");
			return listTypes[0].Namespace;
		}

		private static Assembly AssertKnownResolves(Type typeKnown) {
			Assembly assembly=typeKnown.Assembly;
			Assert.AreEqual(typeKnown,assembly.GetType(typeKnown.Namespace+"."+typeKnown.Name),
				typeKnown.Name+" does not resolve by its namespace, so the lookups below would check nothing.");
			return assembly;
		}
	}
}
