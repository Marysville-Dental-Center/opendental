// Vendor-direct integrations: finds the vendor packages' setup and job classes, so no package edits a shared registry file.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CodeBase.VendorDirect {
	///<summary>Creates one instance of every concrete class in the given assemblies that implements T and has a public parameterless constructor.
	///A vendor package adds its class in its own file; nothing else is edited to register it.</summary>
	public static class TypeDiscovery {
		public static List<T> CreateImplementations<T>(params Assembly[] arrayAssemblies) where T:class {
			List<T> listInstances=new List<T>();
			HashSet<Type> setSeen=new HashSet<Type>();
			foreach(Assembly assembly in arrayAssemblies.Where(x => x!=null).Distinct()) {
				foreach(Type type in GetLoadableTypes(assembly)) {
					if(!setSeen.Add(type)) {
						continue;
					}
					if(!type.IsClass || type.IsAbstract || type.ContainsGenericParameters || !typeof(T).IsAssignableFrom(type)) {
						continue;
					}
					if(type.GetConstructor(Type.EmptyTypes)==null) {
						continue;
					}
					listInstances.Add((T)Activator.CreateInstance(type));
				}
			}
			return listInstances.OrderBy(x => x.GetType().FullName,StringComparer.Ordinal).ToList();
		}

		private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
			try {
				return assembly.GetTypes();
			}
			catch(ReflectionTypeLoadException ex) {
				return ex.Types.Where(x => x!=null);
			}
		}
	}
}
