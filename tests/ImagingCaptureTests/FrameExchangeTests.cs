// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading;
using ImagingCapture.Uvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	[TestClass]
	public class FrameExchangeTests {
		private static void Fill(UvcFrame frame,byte value) {
			for(int i=0;i<frame.Pixels.Length;i++) {
				frame.Pixels[i]=value;
			}
		}

		[TestMethod]
		public void TakeLatest_NullUntilPublished() {
			FrameExchange frameExchange=new FrameExchange();
			Assert.IsNull(frameExchange.TakeLatest());
			Fill(frameExchange.GetBackBuffer(2,2),7);
			Assert.IsNull(frameExchange.TakeLatest(),"A filled but unpublished buffer is not visible.");
			frameExchange.Publish(1.5);
			UvcFrame frame=frameExchange.TakeLatest();
			Assert.IsNotNull(frame);
			Assert.AreEqual(7,frame.Pixels[0]);
			Assert.AreEqual(1,frame.Sequence);
			Assert.AreEqual(1.5,frame.SampleTime);
			Assert.IsNull(frameExchange.TakeLatest(),"Each frame is taken once.");
		}

		[TestMethod]
		public void Publish_UntakenFrameIsSkippedAndNewestWins() {
			FrameExchange frameExchange=new FrameExchange();
			for(byte value=1;value<=3;value++) {
				Fill(frameExchange.GetBackBuffer(2,2),value);
				frameExchange.Publish(value);
			}
			UvcFrame frame=frameExchange.TakeLatest();
			Assert.AreEqual(3,frame.Pixels[0]);
			Assert.AreEqual(3,frame.Sequence);
			Assert.AreEqual(3,frameExchange.CountPublished);
			Assert.AreEqual(2,frameExchange.CountSkipped);
		}

		[TestMethod]
		public void Writer_NeverGetsTheConsumersFrame() {
			FrameExchange frameExchange=new FrameExchange();
			Fill(frameExchange.GetBackBuffer(2,2),1);
			frameExchange.Publish(0);
			UvcFrame frameHeld=frameExchange.TakeLatest();
			for(int i=0;i<10;i++) {
				UvcFrame frameBack=frameExchange.GetBackBuffer(2,2);
				Assert.AreNotSame(frameHeld,frameBack,"Publish "+i);
				Fill(frameBack,99);
				frameExchange.Publish(0);
			}
			Assert.AreEqual(1,frameHeld.Pixels[0],"The consumer's frame was not written while it held it.");
			UvcFrame frameNew=frameExchange.TakeLatest();
			Assert.AreNotSame(frameHeld,frameNew);
			Assert.AreEqual(99,frameNew.Pixels[0]);
		}

		[TestMethod]
		public void BackBuffers_AreReusedAndReallocatedOnSizeChange() {
			FrameExchange frameExchange=new FrameExchange();
			UvcFrame frameA=frameExchange.GetBackBuffer(4,3);
			Assert.AreSame(frameA,frameExchange.GetBackBuffer(4,3),"Same size reuses the buffer.");
			UvcFrame frameB=frameExchange.GetBackBuffer(8,6);
			Assert.AreNotSame(frameA,frameB);
			Assert.AreEqual(8,frameB.Width);
			Assert.AreEqual(6,frameB.Height);
		}

		[TestMethod]
		public void Publish_WithoutBackBufferThrows() {
			FrameExchange frameExchange=new FrameExchange();
			TestUtil.AssertThrows<InvalidOperationException>(() => frameExchange.Publish(0));
		}

		[TestMethod]
		public void Concurrent_WriterAndConsumerNeverTear() {
			//The writer fills each frame with one value; the consumer checks every frame it takes is uniform and that sequence
			//numbers only increase. A frame written while the consumer holds it would show mixed values.
			FrameExchange frameExchange=new FrameExchange();
			const int countFrames=3000;
			Exception exceptionWriter=null;
			Thread threadWriter=new Thread(() => {
				try {
					for(int i=1;i<=countFrames;i++) {
						UvcFrame frame=frameExchange.GetBackBuffer(64,48);
						Fill(frame,(byte)i);
						frameExchange.Publish(i);
					}
				}
				catch(Exception ex) {
					exceptionWriter=ex;
				}
			});
			threadWriter.Start();
			long sequenceLast=0;
			int countTaken=0;
			bool isWriterDone=false;
			while(true) {
				//Read IsAlive before taking, so the frame taken after the writer finished is the last one published.
				bool isLastPass=isWriterDone;
				isWriterDone=!threadWriter.IsAlive;
				UvcFrame frame=frameExchange.TakeLatest();
				if(frame==null) {
					if(isLastPass) {
						break;
					}
					continue;
				}
				countTaken++;
				Assert.IsTrue(frame.Sequence>sequenceLast,"Sequence goes forward.");
				sequenceLast=frame.Sequence;
				byte value=frame.Pixels[0];
				for(int i=0;i<frame.Pixels.Length;i++) {
					if(frame.Pixels[i]!=value) {
						Assert.Fail("Frame "+frame.Sequence+" was torn at byte "+i+".");
					}
				}
				Assert.AreEqual((byte)frame.Sequence,value,"The frame holds the pixels published with its sequence number.");
			}
			threadWriter.Join();
			Assert.IsNull(exceptionWriter);
			Assert.IsTrue(countTaken>0);
			Assert.AreEqual(countFrames,frameExchange.CountPublished);
			Assert.AreEqual(countFrames,sequenceLast,"The last frame published is taken.");
		}
	}
}
