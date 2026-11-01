// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ImagingCapture.Dicom;

namespace ImagingCapture.Templates {
	///<summary>The standard X-ray mount templates (design 9.4 "Standard mount templates", option a).</summary>
	public enum StandardTemplateKind {
		///<summary>FMX with horizontal bitewings: 4 bitewings, 8 posterior and 6 anterior periapicals.</summary>
		FmxHorizontalBitewings,
		///<summary>FMX with vertical bitewings.</summary>
		FmxVerticalBitewings,
		///<summary>4 horizontal bitewings.</summary>
		FourBitewingsHorizontal,
		///<summary>4 vertical bitewings.</summary>
		FourBitewingsVertical,
	}

	///<summary>The intraoral sensor a slot is taken with, by the film-size convention: size 1 for anterior periapicals, size 2 for bitewings and
	///posterior periapicals. The fork knows no sensor's dimensions; the size only says which slots share a sensor.</summary>
	public enum SensorSize {
		Size1,
		Size2,
	}

	///<summary>One image slot of a standard template, as Open Dental's generator lays it out (FormMountDefGenerate).</summary>
	public sealed class StandardTemplateSlot {
		public StandardTemplateSlot(int itemOrder,int xpos,int ypos,int width,int height,int rotateOnAcquire,string toothNumbers) {
			ItemOrder=itemOrder;
			Xpos=xpos;
			Ypos=ypos;
			Width=width;
			Height=height;
			RotateOnAcquire=rotateOnAcquire;
			ToothNumbers=toothNumbers;
			SensorSize=GetSensorSize(toothNumbers);
		}

		///<summary>The exposure order (1-based).</summary>
		public int ItemOrder { get; }
		public int Xpos { get; }
		public int Ypos { get; }
		public int Width { get; }
		public int Height { get; }
		///<summary>Degrees clockwise: 0, 90, 180 or 270. A starting value; the clinic confirms it on the phantom (design 7.10).</summary>
		public int RotateOnAcquire { get; }
		///<summary>Universal tooth numbers, comma-separated: "2,3,30,31".</summary>
		public string ToothNumbers { get; }
		///<summary>Size 1 when every tooth of the slot is anterior (a periapical of the canines and incisors), else size 2.</summary>
		public SensorSize SensorSize { get; }

		private static SensorSize GetSensorSize(string toothNumbers) {
			List<ToothInfo> listTeeth;
			string error;
			if(Dicom.ToothNumbers.TryParseList(toothNumbers,out listTeeth,out error) && listTeeth.Count>0 && listTeeth.All(x => x.IsAnterior)) {
				return SensorSize.Size1;
			}
			return SensorSize.Size2;
		}
	}

	///<summary>The text item of a template (the patient, PatNum, date and provider fields), as Open Dental's generator adds it to an FMX.</summary>
	public sealed class StandardTemplateText {
		public const string TextShowingDefault="Patient: [NameLF]\r\nPatNum: [PatNum]\r\nDate: [MountDate]\r\nProvider: [MountProv]\r\n";
		public const float FontSizeDefault=80;

		public StandardTemplateText(int xpos,int ypos,int width,int height) {
			Xpos=xpos;
			Ypos=ypos;
			Width=width;
			Height=height;
		}

		public int Xpos { get; }
		public int Ypos { get; }
		public int Width { get; }
		public int Height { get; }
		public string TextShowing => TextShowingDefault;
		public float FontSize => FontSizeDefault;
	}

	///<summary>One standard template: the mount definition's size and the slots, in ItemOrder.</summary>
	public sealed class StandardTemplate {
		internal StandardTemplate(StandardTemplateKind kind,string description,int width,int height,List<StandardTemplateSlot> listSlots,
			StandardTemplateText text)
		{
			Kind=kind;
			Description=description;
			Width=width;
			Height=height;
			ListSlots=new ReadOnlyCollection<StandardTemplateSlot>(listSlots.OrderBy(x => x.ItemOrder).ToList());
			Text=text;
			string scaleNote;
			ScaleValue=GetScaleValue(ListSlots,out scaleNote);
			ScaleNote=scaleNote;
		}

		public StandardTemplateKind Kind { get; }
		///<summary>The mount definition's Description, which "Add standard templates" is idempotent by.</summary>
		public string Description { get; }
		public int Width { get; }
		public int Height { get; }
		public ReadOnlyCollection<StandardTemplateSlot> ListSlots { get; }
		///<summary>The text item, or null.</summary>
		public StandardTemplateText Text { get; }
		///<summary>MountDef.ScaleValue: always "" (design 9.1 allows "50 1 mm" only for mounts whose slots match one sensor size 1:1, and the fork
		///knows no sensor's image size; the clinic sets it per the CAP-0 runbook once the slots are sized to the sensor's measured image).</summary>
		public string ScaleValue { get; }
		///<summary>Why the template has its ScaleValue, one sentence.</summary>
		public string ScaleNote { get; }

		///<summary>The sensor of each slot, in ItemOrder.</summary>
		public List<SensorSize> GetSensorSequence() {
			return ListSlots.Select(x => x.SensorSize).ToList();
		}

		///<summary>How often the operator changes the sensor going through the slots in ItemOrder.</summary>
		public int CountSensorSwaps {
			get {
				List<SensorSize> listSizes=GetSensorSequence();
				int countSwaps=0;
				for(int i=1;i<listSizes.Count;i++) {
					if(listSizes[i]!=listSizes[i-1]) {
						countSwaps++;
					}
				}
				return countSwaps;
			}
		}

		///<summary>"size 2 for slots 1-12, then size 1 for slots 13-18".</summary>
		public string GetSensorOrderText() {
			List<string> listRuns=new List<string>();
			int idxStart=0;
			for(int i=1;i<=ListSlots.Count;i++) {
				if(i<ListSlots.Count && ListSlots[i].SensorSize==ListSlots[idxStart].SensorSize) {
					continue;
				}
				string sizeText=ListSlots[idxStart].SensorSize==SensorSize.Size1 ? "size 1" : "size 2";
				string slotsText=i-1==idxStart ? "slot "+ToText(ListSlots[idxStart].ItemOrder)
					: "slots "+ToText(ListSlots[idxStart].ItemOrder)+"-"+ToText(ListSlots[i-1].ItemOrder);
				listRuns.Add(sizeText+" for "+slotsText);
				idxStart=i;
			}
			return string.Join(", then ",listRuns);
		}

		///<summary>Design 9.1: a mount scale holds only when every image is shown 1:1, so only when every slot is exactly the image of the one sensor
		///it is taken with. Open Dental draws a mount image scaled to fit its slot, and the slots here have the generator's default size, which is
		///no sensor's measured image (and one sensor's image cannot fit both the horizontal and the vertical bitewing slots 1:1). So no template
		///gets a scale: a wrong one would make every measurement wrong. The clinic sets ScaleValue once the slots are sized to its sensor's measured
		///image (the CAP-0 runbook).</summary>
		private static string GetScaleValue(IList<StandardTemplateSlot> listSlots,out string scaleNote) {
			bool isOneSensor=listSlots.Select(x => x.SensorSize).Distinct().Count()==1;
			string advice=" Set the mount's scale ("+StandardMountTemplates.ScaleValueOneToOne+" for 0.02 mm pixels) only after its slots are sized to the "
				+"sensor's measured image, as the clinic runbook describes.";
			if(!isOneSensor) {
				scaleNote="No scale: the anterior slots are taken with the size 1 sensor but sized like the size 2 slots, so their images are scaled to fit "
					+"and one mount scale cannot hold for every slot."+advice;
			}
			else {
				scaleNote="No scale: the slots are "+ToText(StandardMountTemplates.SlotWidth)+" x "+ToText(StandardMountTemplates.SlotHeight)+" pixels, "
					+"Open Dental's generator's default, not a measured sensor image, so a mount scale would measure wrongly."+advice;
			}
			return "";
		}

		private static string ToText(int number) {
			return number.ToString(CultureInfo.InvariantCulture);
		}
	}

	///<summary>The standard X-ray mount templates of "Add standard templates" (design 9.4 option a, 10.2), the one source of their slots in the
	///fork. The layouts are the ones Open Dental's own generator makes (FormMountDefGenerate, with its default slot size of 1700 x 1300 pixels):
	///positions, sizes, ItemOrder, ToothNumbers and RotateOnAcquire are identical, and IntraOralAnatomyTests pins every slot's ItemOrder,
	///ToothNumbers and RotateOnAcquire against its own table. The generator already orders the slots by sensor size (design 10.2: the sensor is
	///swapped at most twice): the bitewings and posterior periapicals (size 2) first, then the anterior periapicals (size 1), so an FMX needs one
	///swap and a 4BW none. Rotations are starting values that the clinic confirms on the phantom (design 7.10). Like the generator, the templates
	///are black with white drawings, do not flip, and leave AdjModeAfterSeries off; the FMXs get the generator's text item.</summary>
	public static class StandardMountTemplates {
		///<summary>Open Dental's generator's default slot width (a size 2 sensor image, landscape).</summary>
		public const int SlotWidth=1700;
		public const int SlotHeight=1300;
		///<summary>Design 9.1: 50 pixels per mm, 1 decimal, mm.</summary>
		public const string ScaleValueOneToOne="50 1 mm";
		public const string DescriptionFmxHorizontal="FMX horizontal BW";
		public const string DescriptionFmxVertical="FMX vertical BW";
		public const string Description4BwHorizontal="4BW horizontal";
		public const string Description4BwVertical="4BW vertical";

		///<summary>The four templates, in the order they are added.</summary>
		public static readonly ReadOnlyCollection<StandardTemplate> ListTemplates=new ReadOnlyCollection<StandardTemplate>(new List<StandardTemplate> {
			BuildFmx(StandardTemplateKind.FmxHorizontalBitewings),
			BuildFmx(StandardTemplateKind.FmxVerticalBitewings),
			Build4Bw(StandardTemplateKind.FourBitewingsHorizontal),
			Build4Bw(StandardTemplateKind.FourBitewingsVertical),
		});

		public static StandardTemplate Get(StandardTemplateKind kind) {
			return ListTemplates.First(x => x.Kind==kind);
		}

		///<summary>FormMountDefGenerate, list items 0 (FMX horiz BW) and 1 (FMX vert BW).</summary>
		private static StandardTemplate BuildFmx(StandardTemplateKind kind) {
			int w=SlotWidth;
			int h=SlotHeight;
			bool isVertical=kind==StandardTemplateKind.FmxVerticalBitewings;
			List<StandardTemplateSlot> listSlots=new List<StandardTemplateSlot>();
			void Add(int itemOrder,int x,int y,int width,int height,int rotate,string teeth) {
				listSlots.Add(new StandardTemplateSlot(itemOrder,x,y,width,height,rotate,teeth));
			}
			if(isVertical) {
				//Bitewings first:
				int leftBW=(w-h)/2;
				Add(1,leftBW,h,h,w,90,"2,3,30,31");//molar
				Add(2,w+leftBW,h,h,w,90,"4,5,28,29");//pre
				Add(3,3*w+3*h+leftBW,h,h,w,90,"14,15,18,19");//L molar
				Add(4,2*w+3*h+leftBW,h,h,w,90,"12,13,20,21");//L pre
				//PAs
				Add(5,0,0,w,h,0,"2,3");//UR
				Add(6,w,0,w,h,0,"4,5");
				Add(7,3*w+3*h,h+w,w,h,180,"18,19");//LL
				Add(8,2*w+3*h,h+w,w,h,180,"20,21");
				Add(9,0,h+w,w,h,0,"30,31");//LR
				Add(10,w,h+w,w,h,0,"28,29");
				Add(11,3*w+3*h,0,w,h,180,"14,15");//UL
				Add(12,2*w+3*h,0,w,h,180,"12,13");
				//Anterior
				Add(13,2*w,0,h,w,90,"6,7");//max
				Add(14,2*w+h,0,h,w,90,"8,9");
				Add(15,2*w+2*h,0,h,w,90,"10,11");
				Add(16,2*w,2*h,h,w,270,"26,27");//mand
				Add(17,2*w+h,2*h,h,w,270,"24,25");
				Add(18,2*w+2*h,2*h,h,w,270,"22,23");
				return new StandardTemplate(kind,DescriptionFmxVertical,w*4+h*3,2*h+w,listSlots,new StandardTemplateText(2*w+h,w,w,2*h-w));
			}
			//Bitewings first:
			Add(1,0,h,w,h,0,"2,3,30,31");//molar
			Add(2,w,h,w,h,0,"4,5,28,29");//pre
			Add(3,3*w+3*h,h,w,h,180,"14,15,18,19");//L molar
			Add(4,2*w+3*h,h,w,h,180,"12,13,20,21");//L pre
			//PAs
			Add(5,0,0,w,h,0,"2,3");//UR
			Add(6,w,0,w,h,0,"4,5");
			Add(7,3*w+3*h,2*h,w,h,180,"18,19");//LL
			Add(8,2*w+3*h,2*h,w,h,180,"20,21");
			Add(9,0,2*h,w,h,0,"30,31");//LR
			Add(10,w,2*h,w,h,0,"28,29");
			Add(11,3*w+3*h,0,w,h,180,"14,15");//UL
			Add(12,2*w+3*h,0,w,h,180,"12,13");
			//Anterior
			Add(13,2*w,0,h,w,90,"6,7");//max
			Add(14,2*w+h,0,h,w,90,"8,9");
			Add(15,2*w+2*h,0,h,w,90,"10,11");
			Add(16,2*w,3*h-w,h,w,270,"26,27");//mand
			Add(17,2*w+h,3*h-w,h,w,270,"24,25");
			Add(18,2*w+2*h,3*h-w,h,w,270,"22,23");
			return new StandardTemplate(kind,DescriptionFmxHorizontal,w*4+h*3,3*h,listSlots,new StandardTemplateText(2*w+h,w,w,3*h-2*w));
		}

		///<summary>FormMountDefGenerate, list items 2 (4BW horiz) and 3 (4BW vert).</summary>
		private static StandardTemplate Build4Bw(StandardTemplateKind kind) {
			int w=SlotWidth;
			int h=SlotHeight;
			List<StandardTemplateSlot> listSlots=new List<StandardTemplateSlot>();
			if(kind==StandardTemplateKind.FourBitewingsVertical) {
				listSlots.Add(new StandardTemplateSlot(1,0,0,h,w,0,"2,3,30,31"));//molar
				listSlots.Add(new StandardTemplateSlot(2,h,0,h,w,0,"4,5,28,29"));//pre
				listSlots.Add(new StandardTemplateSlot(3,4*h,0,h,w,180,"14,15,18,19"));//L molar
				listSlots.Add(new StandardTemplateSlot(4,3*h,0,h,w,180,"12,13,20,21"));//L pre
				return new StandardTemplate(kind,Description4BwVertical,h*5,w,listSlots,null);
			}
			listSlots.Add(new StandardTemplateSlot(1,0,0,w,h,0,"2,3,30,31"));//molar
			listSlots.Add(new StandardTemplateSlot(2,w,0,w,h,0,"4,5,28,29"));//pre
			listSlots.Add(new StandardTemplateSlot(3,3*w,0,w,h,180,"14,15,18,19"));//L molar
			listSlots.Add(new StandardTemplateSlot(4,2*w,0,w,h,180,"12,13,20,21"));//L pre
			return new StandardTemplate(kind,Description4BwHorizontal,w*4,h,listSlots,null);
		}
	}
}
