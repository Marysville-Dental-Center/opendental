// Reads an assembly's identity and attributes without running it (A3's build stamp, A8's serializer pairing): reflection-only loads in a
// throwaway AppDomain based on the assembly's folder, unloaded afterwards so no file stays locked.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WindowChecksTests {
	public class AssemblyProbe:MarshalByRefObject {
		private string _folder;

		///<summary>Runs func on a probe in a new AppDomain based on the folder and unloads it.</summary>
		public static T Run<T>(string folder,Func<AssemblyProbe,T> func) {
			AppDomain appDomain=AppDomain.CreateDomain("winchk-probe",null,new AppDomainSetup { ApplicationBase=folder });
			try {
				AssemblyProbe assemblyProbe=(AssemblyProbe)appDomain.CreateInstanceFromAndUnwrap(typeof(AssemblyProbe).Assembly.Location,typeof(AssemblyProbe).FullName);
				assemblyProbe.Init(folder);
				return func(assemblyProbe);
			}
			finally {
				AppDomain.Unload(appDomain);
			}
		}

		public void Init(string folder) {
			_folder=folder;
			AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve+=(sender,args) => {
				string path=Path.Combine(_folder,new AssemblyName(args.Name).Name+".dll");
				if(File.Exists(path)) {
					return Assembly.ReflectionOnlyLoadFrom(path);
				}
				return Assembly.ReflectionOnlyLoad(AppDomain.CurrentDomain.ApplyPolicy(args.Name));
			};
		}

		///<summary>The AssemblyMetadata attributes as "key = value" lines.</summary>
		public string[] GetMetadata(string fileName) {
			Assembly assembly=Assembly.ReflectionOnlyLoadFrom(Path.Combine(_folder,fileName));
			return assembly.GetCustomAttributesData().Where(x => x.AttributeType.Name=="AssemblyMetadataAttribute")
				.Select(x => string.Join(" = ",x.ConstructorArguments.Select(y => y.ToString()))).ToArray();
		}

		public string GetMvid(string fileName) {
			return Assembly.ReflectionOnlyLoadFrom(Path.Combine(_folder,fileName)).ManifestModule.ModuleVersionId.ToString();
		}

		///<summary>The XmlSerializerVersionAttribute's named arguments ("ParentAssemblyId = ...; Version = ...").</summary>
		public string GetSerializerVersion(string fileName) {
			Assembly assembly=Assembly.ReflectionOnlyLoadFrom(Path.Combine(_folder,fileName));
			CustomAttributeData customAttributeData=assembly.GetCustomAttributesData().FirstOrDefault(x => x.AttributeType.Name=="XmlSerializerVersionAttribute");
			if(customAttributeData==null) {
				return "";
			}
			return string.Join("; ",customAttributeData.NamedArguments.Select(x => x.MemberName+" = "+x.TypedValue.Value));
		}
	}
}
