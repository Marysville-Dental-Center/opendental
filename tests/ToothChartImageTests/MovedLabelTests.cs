// The free-text move of package TC-2: the wrapper's TextMoved reaches the relay's TextMoved, and ToothChartRelay.MoveTextLabel gives a label a
// point that survives the round trip through toothinitial.DrawText (SetText writes the workstation's number format, GetTextPoint reads only
// digits and a dot) under en-US and de-DE. No engine raises TextMoved yet (TC-5); the tests raise it as an engine will.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using SparksToothChart;

namespace ToothChartImageTests {
	[TestClass]
	public class MovedLabelTests {
		[TestMethod]
		public void MoveTextLabel_RoundTrip_EnUs() {
			RoundTrip("en-US");
		}

		[TestMethod]
		public void MoveTextLabel_RoundTrip_DeDe() {
			RoundTrip("de-DE");
		}

		private static void RoundTrip(string cultureName) {
			CultureInfo cultureInfoBefore=Thread.CurrentThread.CurrentCulture;
			try {
				Thread.CurrentThread.CurrentCulture=new CultureInfo(cultureName);
				ToothInitial toothInitial=new ToothInitial { InitialType=ToothInitialType.Text,DrawText="25,40;Synthetic label" };
				Assert.AreEqual(new PointF(25,40),toothInitial.GetTextPoint());
				//A point in between pixels, as the mouse gives it, is rounded to whole numbers.
				ToothChartRelay.MoveTextLabel(toothInitial,new PointF(123.6f,45.4f));
				Assert.AreEqual("124,45;Synthetic label",toothInitial.DrawText,cultureName);
				Assert.AreEqual(new PointF(124,45),toothInitial.GetTextPoint(),cultureName);
				Assert.AreEqual("Synthetic label",toothInitial.GetTextString());
				//Halves round away from zero, and the point stays inside 0..410 and 0..307.
				ToothChartRelay.MoveTextLabel(toothInitial,new PointF(0.5f,306.5f));
				Assert.AreEqual(new PointF(1,307),toothInitial.GetTextPoint(),cultureName);
				ToothChartRelay.MoveTextLabel(toothInitial,new PointF(-12.7f,999.2f));
				Assert.AreEqual("0,307;Synthetic label",toothInitial.DrawText,"A negative value would make GetTextPoint read 0,0.");
				ToothChartRelay.MoveTextLabel(toothInitial,new PointF(410.4f,-0.4f));
				Assert.AreEqual(new PointF(410,0),toothInitial.GetTextPoint(),cultureName);
				ToothChartRelay.MoveTextLabel(toothInitial,new PointF(float.NaN,float.PositiveInfinity));
				Assert.AreEqual("0,307;Synthetic label",toothInitial.DrawText,"Not a number reads as 0; infinity is clamped.");
				Assert.AreEqual(ToothInitialType.Text,toothInitial.InitialType);
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureInfoBefore;
			}
		}

		[TestMethod]
		public void MoveTextLabel_ExplainsWhyWholeNumbers_SetTextWithDecimalsUnderDeDeLosesThePoint() {
			CultureInfo cultureInfoBefore=Thread.CurrentThread.CurrentCulture;
			try {
				Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");
				ToothInitial toothInitial=new ToothInitial { InitialType=ToothInitialType.Text,DrawText="" };
				toothInitial.SetText(new PointF(123.6f,45.4f),"Synthetic label");
				Assert.AreEqual("123,6,45,4;Synthetic label",toothInitial.DrawText,"SetText writes decimal commas under de-DE.");
				Assert.AreEqual(new PointF(0,0),toothInitial.GetTextPoint(),"which GetTextPoint does not read.");
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureInfoBefore;
			}
		}

		[TestMethod]
		public void MoveTextLabel_KeepsTheTextWhole_EvenWithASemicolon() {
			ToothInitial toothInitial=new ToothInitial { InitialType=ToothInitialType.Text,DrawText="10,20;first; second" };
			ToothChartRelay.MoveTextLabel(toothInitial,new PointF(30,40));
			Assert.AreEqual("30,40;first; second",toothInitial.DrawText);
			//A label without a point keeps no text, as GetTextString reads it today.
			toothInitial.DrawText="No point";
			ToothChartRelay.MoveTextLabel(toothInitial,new PointF(30,40));
			Assert.AreEqual("30,40;",toothInitial.DrawText);
		}

		[TestMethod]
		public void TextMoved_IsPassedOnByTheRelay_FromTheWrapperItIsGiven() {
			StaRunner.Run(() => {
				using(ToothChartWrapper toothChartWrapper=new ToothChartWrapper())
				using(ToothChartWrapper toothChartWrapperOther=new ToothChartWrapper()) {
					ToothChartRelay toothChartRelay=new ToothChartRelay();
					int count=0;
					ToothChartTextMovedEventArgs eventArgsSeen=null;
					object senderSeen=null;
					toothChartRelay.TextMoved+=(sender,e) => {
						count++;
						senderSeen=sender;
						eventArgsSeen=e;
					};
					toothChartRelay.SetToothChartWrapper(toothChartWrapperOther);
					toothChartRelay.SetToothChartWrapper(toothChartWrapper);
					RaiseTextMoved(toothChartWrapperOther,7,new PointF(1,2));
					Assert.AreEqual(0,count,"A wrapper the relay no longer holds is not passed on.");
					RaiseTextMoved(toothChartWrapper,42,new PointF(123.6f,45.4f));
					Assert.AreEqual(1,count);
					Assert.AreSame(toothChartRelay,senderSeen,"The relay is the sender of its own event.");
					Assert.AreEqual(42,eventArgsSeen.ToothInitialNum);
					Assert.AreEqual(new PointF(123.6f,45.4f),eventArgsSeen.Location,"The relay passes the point as the engine gave it.");
				}
			});
		}

		///<summary>Raises the wrapper's TextMoved as an engine does (the internal OnTextMoved, which only SparksToothChart calls).</summary>
		private static void RaiseTextMoved(ToothChartWrapper toothChartWrapper,long toothInitialNum,PointF pointF) {
			MethodInfo methodInfo=typeof(ToothChartWrapper).GetMethod("OnTextMoved",BindingFlags.Instance|BindingFlags.NonPublic);
			Assert.IsNotNull(methodInfo,"ToothChartWrapper.OnTextMoved");
			methodInfo.Invoke(toothChartWrapper,new object[] { toothInitialNum,pointF });
		}
	}
}
