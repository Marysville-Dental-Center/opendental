// The NTP reply rules of NtpReply.TryParse (docs/specs/ehr-rebuild-plan.md, section 4.3), on synthetic packets built from RFC 5905's field
// layout: a good reply, each rule broken in turn, a kiss-o'-death reply, an all-zero reply, a reply with a different originate time, a short
// reply, and replies on both sides of the 2036 era rollover. Also the request NTPv4 sends. No network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace EhrTests {
	[TestClass]
	public class NtpReplyTests {
		///<summary>The first instant of NTP era 1 (2^32 seconds after 1900-01-01 00:00:00 UTC).</summary>
		private static readonly DateTime _dateTimeEra1=new DateTime(2036,2,7,6,28,16,DateTimeKind.Utc);
		private static readonly DateTime _dateTimeToday=new DateTime(2026,11,8,17,30,0,DateTimeKind.Utc);
		///<summary>Offsets agree to a microsecond: timestamps are read to 100 ns ticks.</summary>
		private const double TOLERANCE_SECONDS=0.000001;

		private static NtpResult Parse(NtpExchange ntpExchange,NtpPacket ntpPacket) {
			return NtpReply.Parse(ntpPacket.ToBytes(),ntpExchange.SentTransmit,ntpExchange.DestinationUtc);
		}

		private static void AssertInvalid(NtpExchange ntpExchange,NtpPacket ntpPacket,string reasonPart,string what) {
			NtpResult ntpResult=Parse(ntpExchange,ntpPacket);
			Assert.AreEqual(EnumNtpResult.Invalid,ntpResult.Status,what+": "+ntpResult.Reason);
			Assert.IsFalse(ntpResult.IsValid,what);
			StringAssert.Contains(ntpResult.Reason,reasonPart,what);
			Assert.IsFalse(NtpReply.TryParse(ntpPacket.ToBytes(),ntpExchange.SentTransmit,ntpExchange.DestinationUtc,out double offset,out string reason),what);
			Assert.AreEqual(0,offset,what+": no offset from an invalid reply.");
			Assert.AreEqual(ntpResult.Reason,reason,what);
		}

		#region The good reply
		[TestMethod]
		public void GoodReply_GivesRfc5905sOffsetAndDelay() {
			foreach(double offsetSeconds in new[] { 30.0,-12.5,0.0,0.4,-3599.75,86400.0 }) {
				NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,offsetSeconds);
				NtpResult ntpResult=Parse(ntpExchange,ntpExchange.MakeReply());
				Assert.AreEqual(EnumNtpResult.Valid,ntpResult.Status,offsetSeconds+": "+ntpResult.Reason);
				Assert.AreEqual(offsetSeconds,ntpResult.OffsetSeconds,TOLERANCE_SECONDS,"theta");
				Assert.AreEqual(2*ntpExchange.OneWaySeconds,ntpResult.DelaySeconds,TOLERANCE_SECONDS,"delta");
				Assert.AreEqual(1,ntpResult.Stratum);
				Assert.AreEqual("",ntpResult.Reason);
				Assert.IsTrue(NtpReply.TryParse(ntpExchange.MakeReply().ToBytes(),ntpExchange.SentTransmit,ntpExchange.DestinationUtc,out double offset,
					out string reason));
				Assert.AreEqual(offsetSeconds,offset,TOLERANCE_SECONDS);
				Assert.AreEqual("",reason);
			}
		}

		[TestMethod]
		public void GoodReply_HandWorkedTimestamps() {
			//T1 = 10 s, T2 = 12 s, T3 = 12.5 s, T4 = 11.5 s after one instant: theta = ((12-10)+(12.5-11.5))/2 = 1.5, delta = (11.5-10)-(12.5-12) = 1.
			//The delay of exactly 1 second is still accepted (0 to 1 second).
			DateTime dateTimeBase=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
			NtpPacket ntpPacket=new NtpPacket();
			ulong sentTransmit=NtpPacket.Timestamp(dateTimeBase.AddSeconds(10));
			ntpPacket.Originate=sentTransmit;
			ntpPacket.Receive=NtpPacket.Timestamp(dateTimeBase.AddSeconds(12));
			ntpPacket.Transmit=NtpPacket.Timestamp(dateTimeBase.AddSeconds(12.5));
			NtpResult ntpResult=NtpReply.Parse(ntpPacket.ToBytes(),sentTransmit,dateTimeBase.AddSeconds(11.5));
			Assert.AreEqual(EnumNtpResult.Valid,ntpResult.Status,ntpResult.Reason);
			Assert.AreEqual(1.5,ntpResult.OffsetSeconds,TOLERANCE_SECONDS);
			Assert.AreEqual(1.0,ntpResult.DelaySeconds,TOLERANCE_SECONDS);
		}

		[TestMethod]
		public void GoodReply_Version3_Stratum15_AndExtensionBytes_AreAccepted() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,2);
			NtpPacket ntpPacket=ntpExchange.MakeReply();
			ntpPacket.Version=3;
			Assert.IsTrue(Parse(ntpExchange,ntpPacket).IsValid,"version 3");
			ntpPacket=ntpExchange.MakeReply();
			ntpPacket.Stratum=15;
			ntpPacket.ReferenceId="À\u0000\u0002\u0001";//A secondary server's reference ID is its source's address (192.0.2.1).
			Assert.IsTrue(Parse(ntpExchange,ntpPacket).IsValid,"stratum 15");
			ntpPacket=ntpExchange.MakeReply();
			ntpPacket.Length=68;//An extension field or a MAC after the header.
			Assert.IsTrue(Parse(ntpExchange,ntpPacket).IsValid,"68 bytes");
			ntpPacket=ntpExchange.MakeReply();
			ntpPacket.LeapIndicator=1;//A leap second is announced: still synchronised.
			Assert.IsTrue(Parse(ntpExchange,ntpPacket).IsValid,"leap indicator 1");
		}

		[TestMethod]
		public void GoodReply_TheDestinationMayBeLocalTime() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,7);
			NtpResult ntpResult=NtpReply.Parse(ntpExchange.MakeReply().ToBytes(),ntpExchange.SentTransmit,ntpExchange.DestinationUtc.ToLocalTime());
			Assert.IsTrue(ntpResult.IsValid,ntpResult.Reason);
			Assert.AreEqual(7,ntpResult.OffsetSeconds,TOLERANCE_SECONDS);
		}
		#endregion The good reply

		#region Each rule broken in turn
		[TestMethod]
		public void ShortReply_IsInvalid() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			NtpPacket ntpPacket=ntpExchange.MakeReply();
			ntpPacket.Length=47;
			AssertInvalid(ntpExchange,ntpPacket,"47 bytes","47 bytes");
			ntpPacket.Length=0;
			AssertInvalid(ntpExchange,ntpPacket,"0 bytes","empty");
			Assert.AreEqual(EnumNtpResult.Invalid,NtpReply.Parse(null,ntpExchange.SentTransmit,ntpExchange.DestinationUtc).Status,"null");
		}

		[TestMethod]
		public void LeapIndicator3_IsInvalid() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			NtpPacket ntpPacket=ntpExchange.MakeReply();
			ntpPacket.LeapIndicator=3;
			AssertInvalid(ntpExchange,ntpPacket,"not synchronised","leap indicator 3");
		}

		[TestMethod]
		public void VersionOtherThan3Or4_IsInvalid() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			foreach(int version in new[] { 0,1,2,5,6,7 }) {
				NtpPacket ntpPacket=ntpExchange.MakeReply();
				ntpPacket.Version=version;
				AssertInvalid(ntpExchange,ntpPacket,"version "+version,"version "+version);
			}
		}

		[TestMethod]
		public void ModeOtherThanServer_IsInvalid() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			//3 is the client's own mode (a reflected request), 5 broadcast, 1 and 2 symmetric, 6 and 7 control and private.
			foreach(int mode in new[] { 0,1,2,3,5,6,7 }) {
				NtpPacket ntpPacket=ntpExchange.MakeReply();
				ntpPacket.Mode=mode;
				AssertInvalid(ntpExchange,ntpPacket,"mode "+mode,"mode "+mode);
			}
		}

		[TestMethod]
		public void StratumAbove15_IsInvalid() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			foreach(int stratum in new[] { 16,17,255 }) {
				NtpPacket ntpPacket=ntpExchange.MakeReply();
				ntpPacket.Stratum=stratum;
				AssertInvalid(ntpExchange,ntpPacket,"stratum "+stratum,"stratum "+stratum);
			}
		}

		[TestMethod]
		public void DifferentOriginateTime_IsInvalid() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			//One bit off in the fraction, one second off, a whole era off (the same seconds and fraction would need the same 64 bits), and zero.
			foreach(ulong originate in new[] { ntpExchange.SentTransmit^1UL,ntpExchange.SentTransmit+(1UL<<32),ntpExchange.SentTransmit-(1UL<<32),0UL }) {
				NtpPacket ntpPacket=ntpExchange.MakeReply();
				ntpPacket.Originate=originate;
				AssertInvalid(ntpExchange,ntpPacket,"originate time","originate "+originate.ToString("X16"));
			}
		}

		[TestMethod]
		public void RequestWithoutTransmitTime_MatchesNoReply() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			NtpPacket ntpPacket=ntpExchange.MakeReply();
			ntpPacket.Originate=0;
			NtpResult ntpResult=NtpReply.Parse(ntpPacket.ToBytes(),0,ntpExchange.DestinationUtc);
			Assert.AreEqual(EnumNtpResult.Invalid,ntpResult.Status);
			StringAssert.Contains(ntpResult.Reason,"no transmit time");
		}

		[TestMethod]
		public void ZeroReceiveOrTransmitTime_IsInvalid() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			NtpPacket ntpPacket=ntpExchange.MakeReply();
			ntpPacket.Receive=0;
			AssertInvalid(ntpExchange,ntpPacket,"is zero","receive 0");
			ntpPacket=ntpExchange.MakeReply();
			ntpPacket.Transmit=0;
			AssertInvalid(ntpExchange,ntpPacket,"is zero","transmit 0");
		}

		[TestMethod]
		public void RoundTripOutside0To1Second_IsInvalid() {
			//Longer than 1 second.
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			ntpExchange.OneWaySeconds=0.6;
			AssertInvalid(ntpExchange,ntpExchange.MakeReply(),"round trip","delay 1.2 s");
			ntpExchange.OneWaySeconds=0.4995;
			Assert.IsTrue(Parse(ntpExchange,ntpExchange.MakeReply()).IsValid,"delay 0.999 s");
			//Negative: the server says it held the request longer than the whole round trip.
			ntpExchange=new NtpExchange(_dateTimeToday,30);
			NtpPacket ntpPacket=ntpExchange.MakeReply();
			ntpPacket.Transmit=NtpPacket.Timestamp(ntpExchange.T2.AddSeconds(0.5));
			AssertInvalid(ntpExchange,ntpPacket,"round trip","negative delay");
		}

		[TestMethod]
		public void RootDistanceOf1SecondOrMore_IsInvalid() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			//Root delay / 2 + root dispersion: 0.5 + 0.5, 0 + 1, 0.75 + 0.6.
			foreach(double[] arrayDistance in new[] { new[] { 1.0,0.5 },new[] { 0.0,1.0 },new[] { 1.5,0.6 } }) {
				NtpPacket ntpPacket=ntpExchange.MakeReply();
				ntpPacket.RootDelaySeconds=arrayDistance[0];
				ntpPacket.RootDispersionSeconds=arrayDistance[1];
				AssertInvalid(ntpExchange,ntpPacket,"root dispersion","distance "+arrayDistance[0]+"/"+arrayDistance[1]);
			}
			NtpPacket ntpPacketBelow=ntpExchange.MakeReply();
			ntpPacketBelow.RootDelaySeconds=0.5;
			ntpPacketBelow.RootDispersionSeconds=0.7;//0.95
			Assert.IsTrue(Parse(ntpExchange,ntpPacketBelow).IsValid,"distance 0.95 s");
		}
		#endregion Each rule broken in turn

		#region Kiss-o'-death and the all-zero reply
		[TestMethod]
		public void KissOfDeath_IsReportedWithItsCode() {
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			foreach(string code in new[] { "RATE","DENY","RSTR" }) {
				NtpPacket ntpPacket=ntpExchange.MakeReply();
				ntpPacket.Stratum=0;
				ntpPacket.LeapIndicator=3;//Kiss-o'-death replies usually say "unsynchronised" too.
				ntpPacket.ReferenceId=code;
				NtpResult ntpResult=Parse(ntpExchange,ntpPacket);
				Assert.AreEqual(EnumNtpResult.KissOfDeath,ntpResult.Status,code);
				Assert.AreEqual(code,ntpResult.KissCode);
				StringAssert.Contains(ntpResult.Reason,code);
				Assert.IsFalse(NtpReply.TryParse(ntpPacket.ToBytes(),ntpExchange.SentTransmit,ntpExchange.DestinationUtc,out double offset,out string reason));
				Assert.AreEqual(0,offset);
				StringAssert.Contains(reason,"kiss-o'-death");
			}
		}

		[TestMethod]
		public void KissOfDeath_WithAnotherOriginateTime_IsOnlyInvalid() {
			//A forged kiss-o'-death from off the path, which cannot know the transmit time, must not stop the checks.
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,30);
			NtpPacket ntpPacket=ntpExchange.MakeReply();
			ntpPacket.Stratum=0;
			ntpPacket.ReferenceId="DENY";
			ntpPacket.Originate=ntpExchange.SentTransmit+1;
			AssertInvalid(ntpExchange,ntpPacket,"originate time","forged kiss-o'-death");
		}

		[TestMethod]
		public void AllZeroReply_IsInvalid() {
			//Open Dental's client read 48 zero bytes as times in 1900 (the originate time too), an offset of about -63 years, and would have set
			//the clock to it.
			NtpExchange ntpExchange=new NtpExchange(_dateTimeToday,0);
			byte[] arrayZero=new byte[48];
			NtpResult ntpResult=NtpReply.Parse(arrayZero,ntpExchange.SentTransmit,ntpExchange.DestinationUtc);
			Assert.AreEqual(EnumNtpResult.Invalid,ntpResult.Status);
			StringAssert.Contains(ntpResult.Reason,"version 0","48 zero bytes fail at the first rule they break: version 0.");
			Assert.IsFalse(NtpReply.TryParse(arrayZero,ntpExchange.SentTransmit,ntpExchange.DestinationUtc,out double offset,out string reason));
			Assert.AreEqual(0,offset);
			Assert.AreNotEqual("",reason);
			//A valid first byte and stratum: the zero originate time does not match the request.
			arrayZero[0]=0x24;//LI 0, version 4, mode 4
			arrayZero[1]=1;
			ntpResult=NtpReply.Parse(arrayZero,ntpExchange.SentTransmit,ntpExchange.DestinationUtc);
			Assert.AreEqual(EnumNtpResult.Invalid,ntpResult.Status);
			StringAssert.Contains(ntpResult.Reason,"originate time is not the transmit time");
			//With the originate time echoed as well (Originate=sentTransmit), the zero receive and transmit times are what fail.
			NtpReply.WriteUInt64(arrayZero,24,ntpExchange.SentTransmit);
			ntpResult=NtpReply.Parse(arrayZero,ntpExchange.SentTransmit,ntpExchange.DestinationUtc);
			Assert.AreEqual(EnumNtpResult.Invalid,ntpResult.Status);
			Assert.AreEqual("The reply's receive or transmit time is zero.",ntpResult.Reason);
		}
		#endregion Kiss-o'-death and the all-zero reply

		#region The 2036 era rollover
		[TestMethod]
		public void Timestamps_HaveRfc5905sKnownValues() {
			Assert.AreEqual(0UL,NtpReply.ToTimestamp(new DateTime(1900,1,1,0,0,0,DateTimeKind.Utc)));
			Assert.AreEqual(2208988800UL<<32,NtpReply.ToTimestamp(new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)),"The Unix epoch.");
			Assert.AreEqual(3155673600UL<<32,NtpReply.ToTimestamp(new DateTime(2000,1,1,0,0,0,DateTimeKind.Utc)));
			Assert.AreEqual(0UL,NtpReply.ToTimestamp(_dateTimeEra1),"Era 1 starts at 0 again.");
			Assert.AreEqual(0xFFFFFFFFUL<<32,NtpReply.ToTimestamp(_dateTimeEra1.AddSeconds(-1)),"The last second of era 0.");
			Assert.AreEqual((1UL<<32)|0x80000000UL,NtpReply.ToTimestamp(_dateTimeEra1.AddSeconds(1.5)),"1.5 seconds into era 1.");
			foreach(DateTime dateTime in new[] { _dateTimeToday,_dateTimeEra1.AddSeconds(-0.25),_dateTimeEra1.AddDays(400),new DateTime(2100,3,1,12,0,0,DateTimeKind.Utc) }) {
				Assert.AreEqual(NtpPacket.Timestamp(dateTime),NtpReply.ToTimestamp(dateTime),dateTime.ToString("o"));
			}
		}

		[TestMethod]
		public void Timestamps_ArePlacedInTheEraNearestTheClock() {
			Assert.AreEqual(_dateTimeEra1,NtpReply.FromTimestamp(0,_dateTimeEra1.AddYears(-10)),"0 near 2026 is the start of era 1, not 1900.");
			Assert.AreEqual(new DateTime(1900,1,1,0,0,0,DateTimeKind.Utc),NtpReply.FromTimestamp(0,new DateTime(1950,1,1,0,0,0,DateTimeKind.Utc)));
			Assert.AreEqual(_dateTimeEra1.AddSeconds(-1),NtpReply.FromTimestamp(0xFFFFFFFFUL<<32,_dateTimeEra1.AddSeconds(30)),
				"The last second of era 0, read just after the rollover.");
			Assert.AreEqual(_dateTimeEra1.AddSeconds(4),NtpReply.FromTimestamp(4UL<<32,_dateTimeEra1.AddSeconds(-30)),
				"Four seconds into era 1, read just before the rollover.");
			foreach(DateTime dateTime in new[] { _dateTimeToday,_dateTimeEra1.AddMilliseconds(250),new DateTime(2100,3,1,12,0,0,DateTimeKind.Utc) }) {
				DateTime dateTimeRead=NtpReply.FromTimestamp(NtpReply.ToTimestamp(dateTime),dateTime.AddDays(-3000));
				Assert.AreEqual(0,(dateTimeRead-dateTime).TotalMilliseconds,0.001,dateTime.ToString("o"));
			}
		}

		[TestMethod]
		public void Rollover_ServerAfterIt_ClientBefore() {
			//The client's clock reads 06:28:06, ten seconds before the rollover; the server, 20 seconds ahead, answers after it.
			NtpExchange ntpExchange=new NtpExchange(_dateTimeEra1.AddSeconds(-10),20);
			Assert.IsTrue(NtpPacket.Timestamp(ntpExchange.T2)<(100UL<<32),"The server's times are in era 1 (small seconds).");
			NtpResult ntpResult=Parse(ntpExchange,ntpExchange.MakeReply());
			Assert.IsTrue(ntpResult.IsValid,ntpResult.Reason);
			Assert.AreEqual(20,ntpResult.OffsetSeconds,TOLERANCE_SECONDS);
		}

		[TestMethod]
		public void Rollover_ServerBeforeIt_ClientAfter() {
			//The client's clock reads 06:28:26, ten seconds after the rollover; the server, 20 seconds behind, still answers in era 0.
			NtpExchange ntpExchange=new NtpExchange(_dateTimeEra1.AddSeconds(10),-20);
			Assert.IsTrue(NtpPacket.Timestamp(ntpExchange.T2)>(0xFFFFFF00UL<<32),"The server's times are at the end of era 0.");
			NtpResult ntpResult=Parse(ntpExchange,ntpExchange.MakeReply());
			Assert.IsTrue(ntpResult.IsValid,ntpResult.Reason);
			Assert.AreEqual(-20,ntpResult.OffsetSeconds,TOLERANCE_SECONDS);
		}

		[TestMethod]
		public void Rollover_DuringTheRoundTrip() {
			//The request leaves in era 0 and the reply arrives in era 1 by both clocks.
			NtpExchange ntpExchange=new NtpExchange(_dateTimeEra1.AddMilliseconds(5),0.25);
			ntpExchange.OneWaySeconds=0.02;
			Assert.IsTrue(ntpExchange.T1<_dateTimeEra1);
			NtpResult ntpResult=Parse(ntpExchange,ntpExchange.MakeReply());
			Assert.IsTrue(ntpResult.IsValid,ntpResult.Reason);
			Assert.AreEqual(0.25,ntpResult.OffsetSeconds,TOLERANCE_SECONDS);
			Assert.AreEqual(0.04,ntpResult.DelaySeconds,TOLERANCE_SECONDS);
		}

		[TestMethod]
		public void LaterEra_Works() {
			NtpExchange ntpExchange=new NtpExchange(new DateTime(2100,3,1,12,0,0,DateTimeKind.Utc),-6);
			NtpResult ntpResult=Parse(ntpExchange,ntpExchange.MakeReply());
			Assert.IsTrue(ntpResult.IsValid,ntpResult.Reason);
			Assert.AreEqual(-6,ntpResult.OffsetSeconds,TOLERANCE_SECONDS);
		}
		#endregion The 2036 era rollover

		#region The request
		[TestMethod]
		public void Request_IsVersion4_ClientMode_WithTheTransmitTime() {
			Assert.AreEqual((byte)0x23,NTPv4.REQUEST_FIRST_BYTE,"LI 0, version 4, mode 3. Open Dental sent 0x1B (version 3).");
			ulong transmit=0x0123456789ABCDEFUL;
			byte[] arrayRequest=NTPv4.MakeRequest(transmit);
			Assert.AreEqual(48,arrayRequest.Length);
			Assert.AreEqual(0x23,arrayRequest[0]);
			Assert.AreEqual(4,(arrayRequest[0]>>3) & 7,"version");
			Assert.AreEqual(3,arrayRequest[0] & 7,"mode");
			CollectionAssert.AreEqual(new byte[] { 0x01,0x23,0x45,0x67,0x89,0xAB,0xCD,0xEF },arrayRequest.Skip(40).ToArray(),"Transmit time, big-endian.");
			Assert.IsTrue(arrayRequest.Skip(1).Take(39).All(x => x==0),"Every other field is zero.");
		}

		[TestMethod]
		public void TransmitTime_IsTheClock_WithRandomLowBits() {
			DateTime dateTimeUtc=_dateTimeToday.AddMilliseconds(123);
			HashSet<ulong> hashSetTransmits=new HashSet<ulong>();
			for(int i=0;i<20;i++) {
				ulong transmit=NTPv4.MakeTransmitTimestamp(dateTimeUtc);
				Assert.AreNotEqual(0UL,transmit);
				//Within a millisecond of the clock: only bits below the clock's resolution change.
				Assert.AreEqual(0,(NtpReply.FromTimestamp(transmit,dateTimeUtc)-dateTimeUtc).TotalMilliseconds,1.0);
				hashSetTransmits.Add(transmit);
			}
			Assert.IsTrue(hashSetTransmits.Count>1,"The low bits are random.");
		}

		[TestMethod]
		public void Address_IsTheFirstIPv4One() {
			IPAddress ipAddressV6=IPAddress.IPv6Loopback;
			IPAddress ipAddressV4=IPAddress.Parse("192.0.2.10");
			IPAddress ipAddressV4Second=IPAddress.Parse("192.0.2.20");
			Assert.AreEqual(ipAddressV4,NTPv4.GetFirstIPv4(new[] { ipAddressV6,ipAddressV4,ipAddressV4Second }),"An IPv6 address first is skipped.");
			Assert.IsNull(NTPv4.GetFirstIPv4(new[] { ipAddressV6 }));
			Assert.IsNull(NTPv4.GetFirstIPv4(new IPAddress[0]));
			Assert.IsNull(NTPv4.GetFirstIPv4(null));
		}

		[TestMethod]
		public void Query_WithoutAServerName_SendsNothing() {
			List<string> listEventLog=new List<string>();
			NTPv4.ActionWriteEventLog=(text) => listEventLog.Add(text);
			try {
				NtpResult ntpResult=new NTPv4().Query("  ");
				Assert.AreEqual(EnumNtpResult.NoReply,ntpResult.Status);
				StringAssert.Contains(ntpResult.Reason,"No time server");
				Assert.AreEqual(1,listEventLog.Count,"One Event Log entry per request, through the seam.");
				StringAssert.Contains(listEventLog[0],"NTPv4 time request to : NoReply.");
				StringAssert.Contains(listEventLog[0],"No time server is set.");
				Assert.AreEqual(double.MaxValue,new NTPv4().getTime(""),"getTime gives double.MaxValue without a valid reply, as before.");
				Assert.AreEqual(2,listEventLog.Count,"getTime goes through Query and writes its entry through the seam too.");
			}
			finally {
				ClockGuard.Install();
			}
		}
		#endregion The request
	}
}
