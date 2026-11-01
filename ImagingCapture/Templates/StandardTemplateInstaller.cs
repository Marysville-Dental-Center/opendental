// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Templates {
	///<summary>The database seam of "Add standard templates".</summary>
	public interface IStandardTemplateStore {
		///<summary>Security.IsAuthorized(permType,suppressMessage:true): the caller shows the refusal itself.</summary>
		bool IsAuthorized(EnumPermType permType);
		///<summary>Every mount definition, read fresh (the ToolButsAndMounts cache refreshed, then MountDefs.GetDeepCopy).</summary>
		List<MountDef> GetMountDefs();
		///<summary>Defs.GetDef(DefCat.ImageCats,defNum) is not null.</summary>
		bool IsCategoryValid(long defNum);
		///<summary>MountDefs.Insert; returns the MountDefNum. Throws on failure.</summary>
		long InsertMountDef(MountDef mountDef);
		///<summary>MountItemDefs.Insert. Throws on failure.</summary>
		long InsertMountItemDef(MountItemDef mountItemDef);
		///<summary>MountDefs.Delete, which deletes its items too. Throws on failure.</summary>
		void DeleteMountDef(long mountDefNum);
		///<summary>SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue). Throws on failure.</summary>
		void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey);
		///<summary>Tells the other computers and this one that the mount definitions changed (DataValid.SetInvalid ToolButsAndMounts).</summary>
		void SignalMountDefsChanged();
	}

	///<summary>IStandardTemplateStore through OpenDentBusiness. Not exercised by the unit tests, which have no database.</summary>
	public sealed class OdStandardTemplateStore:IStandardTemplateStore {
		public bool IsAuthorized(EnumPermType permType) {
			return Security.IsAuthorized(permType,suppressMessage:true);
		}

		public List<MountDef> GetMountDefs() {
			//MountDefs.RefreshCache returns a DataTable, which this project does not reference; this refreshes the same cache.
			OpenPmsDentalBusiness.Cache.Refresh(InvalidType.ToolButsAndMounts);
			return MountDefs.GetDeepCopy();
		}

		public bool IsCategoryValid(long defNum) {
			return Defs.GetDef(DefCat.ImageCats,defNum)!=null;
		}

		public long InsertMountDef(MountDef mountDef) {
			return MountDefs.Insert(mountDef);
		}

		public long InsertMountItemDef(MountItemDef mountItemDef) {
			return MountItemDefs.Insert(mountItemDef);
		}

		public void DeleteMountDef(long mountDefNum) {
			MountDefs.Delete(mountDefNum);
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue);
		}

		public void SignalMountDefsChanged() {
			OpenPmsDental.DataValid.SetInvalid(InvalidType.ToolButsAndMounts);
		}
	}

	///<summary>One template of a plan: added, or left alone because a mount definition with its Description exists.</summary>
	public sealed class StandardTemplatePlanItem {
		public StandardTemplate Template { get; set; }
		///<summary>The mount definition with the same Description (trimmed, ignoring case), or null: then the template is added.</summary>
		public MountDef MountDefExisting { get; set; }

		public bool IsAdded => MountDefExisting==null;
	}

	///<summary>What "Add standard templates" will do: each standard template, added or left alone.</summary>
	public sealed class StandardTemplatePlan {
		public long CategoryDefNum { get; set; }
		public List<StandardTemplatePlanItem> ListItems { get; set; }=new List<StandardTemplatePlanItem>();

		public List<StandardTemplatePlanItem> ListToAdd => ListItems.Where(x => x.IsAdded).ToList();
		public List<StandardTemplatePlanItem> ListLeftAlone => ListItems.Where(x => !x.IsAdded).ToList();
	}

	///<summary>The pure logic of "Add standard templates" (design 9.4 option a): which templates are added (idempotent by Description), the mount
	///definition and items of each, and the security-log text. No database.</summary>
	public static class StandardTemplatePlanner {
		public const string FormatName="standard-mount-template/1";
		public const string LogPrefix="Standard mount template added ("+FormatName+"): ";

		///<summary>The plan for listMountDefsExisting: a template whose Description an existing mount definition has (trimmed, ignoring case) is
		///left alone.</summary>
		public static StandardTemplatePlan Plan(IEnumerable<MountDef> listMountDefsExisting,long categoryDefNum) {
			List<MountDef> listMountDefs=(listMountDefsExisting??Enumerable.Empty<MountDef>()).Where(x => x!=null).ToList();
			StandardTemplatePlan plan=new StandardTemplatePlan { CategoryDefNum=categoryDefNum };
			foreach(StandardTemplate template in StandardMountTemplates.ListTemplates) {
				plan.ListItems.Add(new StandardTemplatePlanItem {
					Template=template,
					MountDefExisting=listMountDefs.FirstOrDefault(x => string.Equals((x.Description??"").Trim(),template.Description,StringComparison.OrdinalIgnoreCase)),
				});
			}
			return plan;
		}

		///<summary>The mount definition of template, as Open Dental's generator sets one up: black background, white drawings and text on black, no
		///flip, AdjModeAfterSeries off (the generator leaves it as a new mount definition has it), DefaultCat the X-ray category picked, and the
		///template's ScaleValue. itemOrder: its place in the mount list.</summary>
		public static MountDef BuildMountDef(StandardTemplate template,long categoryDefNum,int itemOrder) {
			MountDef mountDef=new MountDef();
			mountDef.IsNew=true;
			mountDef.Description=template.Description;
			mountDef.ItemOrder=itemOrder;
			mountDef.Width=template.Width;
			mountDef.Height=template.Height;
			mountDef.ColorBack=Color.Black;
			mountDef.ColorFore=Color.White;
			mountDef.ColorTextBack=Color.Black;
			mountDef.ScaleValue=template.ScaleValue;
			mountDef.DefaultCat=categoryDefNum;
			mountDef.FlipOnAcquire=false;
			mountDef.AdjModeAfterSeries=false;
			return mountDef;
		}

		///<summary>The items of template for mountDefNum: the slots in ItemOrder, then the text item (ItemOrder 0), as the generator inserts them.</summary>
		public static List<MountItemDef> BuildMountItemDefs(StandardTemplate template,long mountDefNum) {
			List<MountItemDef> listMountItemDefs=new List<MountItemDef>();
			foreach(StandardTemplateSlot slot in template.ListSlots) {
				MountItemDef mountItemDef=new MountItemDef();
				mountItemDef.MountDefNum=mountDefNum;
				mountItemDef.ItemOrder=slot.ItemOrder;
				mountItemDef.Xpos=slot.Xpos;
				mountItemDef.Ypos=slot.Ypos;
				mountItemDef.Width=slot.Width;
				mountItemDef.Height=slot.Height;
				mountItemDef.RotateOnAcquire=slot.RotateOnAcquire;
				mountItemDef.ToothNumbers=slot.ToothNumbers;
				mountItemDef.TextShowing="";
				listMountItemDefs.Add(mountItemDef);
			}
			if(template.Text!=null) {
				MountItemDef mountItemDef=new MountItemDef();
				mountItemDef.MountDefNum=mountDefNum;
				mountItemDef.Xpos=template.Text.Xpos;
				mountItemDef.Ypos=template.Text.Ypos;
				mountItemDef.Width=template.Text.Width;
				mountItemDef.Height=template.Text.Height;
				mountItemDef.ToothNumbers="";
				mountItemDef.TextShowing=template.Text.TextShowing;
				mountItemDef.FontSize=template.Text.FontSize;
				listMountItemDefs.Add(mountItemDef);
			}
			return listMountItemDefs;
		}

		///<summary>The Setup security-log text for an added template (design 10.7: the new values).</summary>
		public static string BuildLogText(StandardTemplate template,MountDef mountDef,string categoryName) {
			List<string> listParts=new List<string> {
				"Description="+template.Description,
				"MountDefNum="+mountDef.MountDefNum.ToString(CultureInfo.InvariantCulture),
				"DefaultCat="+mountDef.DefaultCat.ToString(CultureInfo.InvariantCulture)+(string.IsNullOrWhiteSpace(categoryName) ? "" : " ("+categoryName.Trim()+")"),
				"Size="+ToText(mountDef.Width)+"x"+ToText(mountDef.Height),
				"Slots="+ToText(template.ListSlots.Count),
				"SensorOrder="+template.GetSensorOrderText(),
				"ScaleValue="+(template.ScaleValue=="" ? "(none)" : template.ScaleValue),
				"FlipOnAcquire=0",
				"AdjModeAfterSeries=0",
				"SlotList="+string.Join("|",template.ListSlots.Select(x => ToText(x.ItemOrder)+":"+x.ToothNumbers.Replace(",","/")+":"+ToText(x.RotateOnAcquire))),
			};
			return LogPrefix+string.Join("; ",listParts);
		}

		private static string ToText(int number) {
			return number.ToString(CultureInfo.InvariantCulture);
		}
	}

	///<summary>What "Add standard templates" did.</summary>
	public sealed class StandardTemplateResult {
		///<summary>False when nothing was tried (permission, category) or a template could not be added; Error says why.</summary>
		public bool IsDone { get; set; }
		public string Error { get; set; }="";
		public StandardTemplatePlan Plan { get; set; }
		///<summary>The mount definitions added, in order.</summary>
		public List<MountDef> ListAdded { get; set; }=new List<MountDef>();
		public List<string> ListWarnings { get; set; }=new List<string>();
	}

	///<summary>"Add standard templates" (design 9.4 option a): a Setup user adds the standard FMX and 4BW mount definitions
	///(StandardMountTemplates) with the X-ray category they picked as DefaultCat. It is idempotent by Description: a mount definition that
	///already has a template's Description is left alone and reported, so running it again adds nothing. Each added template gets a Setup
	///security-log entry (PatNum 0, FKey=MountDefNum). A template whose items cannot all be inserted is deleted again, so no half template stays.
	///No migration seeds them (design 9.4 says never).</summary>
	public sealed class StandardTemplateInstaller {
		private readonly IStandardTemplateStore _store;

		public StandardTemplateInstaller(IStandardTemplateStore store) {
			_store=store??throw new ArgumentNullException(nameof(store));
		}

		///<summary>The plan for the mount definitions in the database now. Throws when they cannot be read.</summary>
		public StandardTemplatePlan GetPlan(long categoryDefNum) {
			return StandardTemplatePlanner.Plan(_store.GetMountDefs(),categoryDefNum);
		}

		///<summary>Adds the templates that do not exist yet. categoryName is for the log only. Never throws.</summary>
		public StandardTemplateResult Install(long categoryDefNum,string categoryName) {
			StandardTemplateResult result=new StandardTemplateResult();
			try {
				if(!_store.IsAuthorized(EnumPermType.Setup)) {
					result.Error="Not authorized for Setup: no mount template was added.";
					return result;
				}
				if(categoryDefNum<=0 || !_store.IsCategoryValid(categoryDefNum)) {
					result.Error="Choose the X-ray category the templates save into.";
					return result;
				}
				List<MountDef> listMountDefs=_store.GetMountDefs()??new List<MountDef>();
				result.Plan=StandardTemplatePlanner.Plan(listMountDefs,categoryDefNum);
				int itemOrder=listMountDefs.Count;
				foreach(StandardTemplatePlanItem item in result.Plan.ListToAdd) {
					string error;
					MountDef mountDef=Add(item.Template,categoryDefNum,itemOrder,out error);
					if(mountDef==null) {
						result.Error=error;
						break;
					}
					itemOrder++;
					result.ListAdded.Add(mountDef);
					try {
						_store.MakeLogEntry(EnumPermType.Setup,0,StandardTemplatePlanner.BuildLogText(item.Template,mountDef,categoryName),mountDef.MountDefNum);
					}
					catch(Exception ex) {
						result.ListWarnings.Add("The security-log entry for \""+item.Template.Description+"\" could not be written: "+ex.Message);
					}
				}
				result.IsDone=result.Error=="";
			}
			catch(Exception ex) {
				result.Error="The mount templates could not be added: "+ex.Message;
			}
			if(result.ListAdded.Count>0) {
				try {
					_store.SignalMountDefsChanged();
				}
				catch(Exception ex) {
					result.ListWarnings.Add("Other computers could not be told about the new templates: "+ex.Message);
				}
			}
			return result;
		}

		///<summary>Inserts the mount definition and its items; deletes it again when an item fails. Null with error on failure.</summary>
		private MountDef Add(StandardTemplate template,long categoryDefNum,int itemOrder,out string error) {
			error="";
			MountDef mountDef=StandardTemplatePlanner.BuildMountDef(template,categoryDefNum,itemOrder);
			try {
				mountDef.MountDefNum=_store.InsertMountDef(mountDef);
				mountDef.IsNew=false;
			}
			catch(Exception ex) {
				error="\""+template.Description+"\" could not be added: "+ex.Message;
				return null;
			}
			try {
				foreach(MountItemDef mountItemDef in StandardTemplatePlanner.BuildMountItemDefs(template,mountDef.MountDefNum)) {
					_store.InsertMountItemDef(mountItemDef);
				}
			}
			catch(Exception ex) {
				error="\""+template.Description+"\" could not be added: "+ex.Message;
				try {
					_store.DeleteMountDef(mountDef.MountDefNum);
				}
				catch(Exception exDelete) {
					error+=" The incomplete mount definition "+mountDef.MountDefNum.ToString(CultureInfo.InvariantCulture)+" could not be deleted ("
						+exDelete.Message+"); delete it in Setup, Mounts.";
				}
				return null;
			}
			return mountDef;
		}
	}
}
