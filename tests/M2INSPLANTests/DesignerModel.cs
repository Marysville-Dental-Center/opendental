// Reads a Windows Forms designer file (FormInsPlan.designer.cs) into a simple model: declared fields and their types, each control's bounds,
// visibility, text and parent, the form's client size and the event wiring. Only the statement forms the designer writes are understood.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ForkMigrationTests;

namespace M2INSPLANTests {
	///<summary>One control (or component) of the designer file.</summary>
	public class DesignerControl {
		public string Name;
		///<summary>The declared type, full name (OpenPmsDental.UI.Button, System.Windows.Forms.Label, ...).</summary>
		public string TypeName;
		///<summary>"this" for controls added to the form itself, null when no Controls.Add puts it anywhere.</summary>
		public string ParentName;
		public Rectangle Bounds;
		public bool HasBounds;
		///<summary>False only when the designer sets Visible = false.</summary>
		public bool IsVisibleInDesigner=true;
		public string Text;
		public int TabIndex=-1;
		public bool IsAutoScroll;
		public float FontSize=8.25f;
		public bool IsFontBold;
		public string CheckAlign;
		///<summary>A button that shows an image or icon next to its text.</summary>
		public bool HasImage;
	}

	public class DesignerModel {
		public const string FORM="this";
		public Dictionary<string,DesignerControl> DictControls=new Dictionary<string,DesignerControl>();
		public Size ClientSize;
		///<summary>Control, event and handler, as "control.Event:handler".</summary>
		public List<string> ListEvents=new List<string>();
		///<summary>Raw designer text, for searches.</summary>
		public string Source;

		private static readonly Regex _regexDecl=new Regex(@"^\t\tprivate ([\w\.]+) (\w+);",RegexOptions.Multiline);
		private static readonly Regex _regexProp=new Regex(@"^\t\t\tthis\.(\w+)\.(\w+) = (.*);$");
		private static readonly Regex _regexAdd=new Regex(@"^\t\t\tthis\.(?:(\w+)\.)?Controls\.Add\(this\.(\w+)\);$");
		private static readonly Regex _regexEvent=new Regex(@"^\t\t\tthis\.(\w+)\.(\w+) \+= new [\w\.]+\(this\.(\w+)\);$");
		private static readonly Regex _regexPoint=new Regex(@"^new System\.Drawing\.Point\((-?\d+), (-?\d+)\)$");
		private static readonly Regex _regexSize=new Regex(@"^new System\.Drawing\.Size\((\d+), (\d+)\)$");
		private static readonly Regex _regexFont=new Regex(@"^new System\.Drawing\.Font\(""[^""]+"", ([\d\.]+)F(.*)\)$");

		public static DesignerModel LoadFormInsPlan() {
			string path=TestDb.FindRepoFile(Path.Combine("OpenPmsDental","Forms","FormInsPlan.designer.cs"));
			return Parse(File.ReadAllText(path,Encoding.UTF8));
		}

		public static DesignerModel Parse(string source) {
			DesignerModel designerModel=new DesignerModel();
			designerModel.Source=source;
			foreach(Match match in _regexDecl.Matches(source)) {
				DesignerControl designerControl=new DesignerControl();
				designerControl.Name=match.Groups[2].Value;
				designerControl.TypeName=NormalizeType(match.Groups[1].Value);
				designerModel.DictControls[designerControl.Name]=designerControl;
			}
			//Join the designer's continued string literals ("..." +\r\n    "...") into one statement per line.
			List<string> listLines=new List<string>();
			string[] arrayLines=source.Replace("\r\n","\n").Split('\n');
			for(int i=0;i<arrayLines.Length;i++) {
				string line=arrayLines[i];
				while(line.EndsWith("\" +") && i+1<arrayLines.Length) {
					i++;
					line=line.Substring(0,line.Length-3)+arrayLines[i].Trim().Substring(1);//drop the closing quote, the plus and the next opening quote
				}
				listLines.Add(line);
			}
			foreach(string line in listLines) {
				Match matchAdd=_regexAdd.Match(line);
				if(matchAdd.Success) {
					string parent=matchAdd.Groups[1].Success && matchAdd.Groups[1].Value!="" ? matchAdd.Groups[1].Value : FORM;
					designerModel.Get(matchAdd.Groups[2].Value).ParentName=parent;
					continue;
				}
				Match matchEvent=_regexEvent.Match(line);
				if(matchEvent.Success) {
					designerModel.ListEvents.Add(matchEvent.Groups[1].Value+"."+matchEvent.Groups[2].Value+":"+matchEvent.Groups[3].Value);
					continue;
				}
				if(line.StartsWith("\t\t\tthis.ClientSize = ")) {
					Match matchClient=_regexSize.Match(line.Substring("\t\t\tthis.ClientSize = ".Length).TrimEnd(';'));
					designerModel.ClientSize=new Size(int.Parse(matchClient.Groups[1].Value),int.Parse(matchClient.Groups[2].Value));
					continue;
				}
				Match matchProp=_regexProp.Match(line);
				if(!matchProp.Success || !designerModel.DictControls.ContainsKey(matchProp.Groups[1].Value)) {
					continue;
				}
				DesignerControl designerControl=designerModel.DictControls[matchProp.Groups[1].Value];
				string value=matchProp.Groups[3].Value;
				switch(matchProp.Groups[2].Value) {
					case "Location":
						Match matchPoint=_regexPoint.Match(value);
						designerControl.Bounds.Location=new Point(int.Parse(matchPoint.Groups[1].Value),int.Parse(matchPoint.Groups[2].Value));
						designerControl.HasBounds=true;
						break;
					case "Size":
						Match matchSize=_regexSize.Match(value);
						designerControl.Bounds.Size=new Size(int.Parse(matchSize.Groups[1].Value),int.Parse(matchSize.Groups[2].Value));
						break;
					case "Visible":
						designerControl.IsVisibleInDesigner=(value!="false");
						break;
					case "Text":
						designerControl.Text=Unescape(value);
						break;
					case "TabIndex":
						designerControl.TabIndex=int.Parse(value);
						break;
					case "AutoScroll":
						designerControl.IsAutoScroll=(value=="true");
						break;
					case "CheckAlign":
						designerControl.CheckAlign=value.Substring(value.LastIndexOf('.')+1);
						break;
					case "Image":
					case "Icon":
						designerControl.HasImage=true;
						break;
					case "Font":
						Match matchFont=_regexFont.Match(value);
						if(matchFont.Success) {
							designerControl.FontSize=float.Parse(matchFont.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
							designerControl.IsFontBold=matchFont.Groups[2].Value.Contains("FontStyle.Bold");
						}
						break;
				}
			}
			return designerModel;
		}

		public DesignerControl Get(string name) {
			if(!DictControls.TryGetValue(name,out DesignerControl designerControl)) {
				throw new Exception("Not declared in the designer: "+name);
			}
			return designerControl;
		}

		///<summary>The control's ancestors from its parent up to the form (not included). Throws when the chain does not reach the form.</summary>
		public List<DesignerControl> GetAncestors(string name) {
			List<DesignerControl> listAncestors=new List<DesignerControl>();
			string parent=Get(name).ParentName;
			while(parent!=FORM) {
				if(parent==null) {
					throw new Exception(name+" is not on the form: no Controls.Add puts it, or one of its containers, anywhere.");
				}
				DesignerControl designerControlParent=Get(parent);
				if(listAncestors.Contains(designerControlParent)) {
					throw new Exception("Parent loop at "+parent);
				}
				listAncestors.Add(designerControlParent);
				parent=designerControlParent.ParentName;
			}
			return listAncestors;
		}

		///<summary>The size of the area a container offers its children: the form's client size, or the container's own size.</summary>
		public Size GetClientSizeOf(string parentName) {
			if(parentName==FORM) {
				return ClientSize;
			}
			return Get(parentName).Bounds.Size;
		}

		public List<DesignerControl> GetChildren(string parentName) {
			return DictControls.Values.Where(x => x.ParentName==parentName).ToList();
		}

		///<summary>Full type name for the short names the designer declarations use.</summary>
		public static string NormalizeType(string typeName) {
			if(typeName.StartsWith("OpenPmsDental.") || typeName.StartsWith("System.")) {
				return typeName;
			}
			if(typeName.StartsWith("UI.")) {
				return "OpenPmsDental."+typeName;
			}
			switch(typeName) {
				case "Label":
				case "TextBox":
				case "RadioButton":
				case "Panel":
					return "System.Windows.Forms."+typeName;
				case "ValidDate":
				case "ValidPhone":
				case "ValidDouble":
				case "ValidNum":
				case "ODtextBox":
					return "OpenPmsDental."+typeName;
			}
			return typeName;
		}

		private static string Unescape(string value) {
			if(value.Length<2 || value[0]!='"') {
				return value;
			}
			return value.Substring(1,value.Length-2).Replace("\\'","'").Replace("\\\"","\"").Replace("\\n","\n").Replace("\\r","\r").Replace("\\\\","\\");
		}
	}
}
