// Synthetic NTP packets, built from the field layout of RFC 5905 (section 7.3, figure 8). Nothing is sent or received.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace EhrTests {
	///<summary>One NTP header as a server would send it. Every field of RFC 5905's packet header has a property; ToBytes writes them in network byte
	///order. Timestamps are 64-bit NTP values (32 bits of seconds within the era, 32 bits of fraction), built here independently of the code
	///under test (Timestamp).</summary>
	public class NtpPacket {
		///<summary>LI, 2 bits: 0 no warning, 3 unsynchronised.</summary>
		public int LeapIndicator=0;
		///<summary>VN, 3 bits.</summary>
		public int Version=4;
		///<summary>Mode, 3 bits: 3 client, 4 server, 5 broadcast.</summary>
		public int Mode=4;
		///<summary>0 kiss-o'-death, 1 primary, 2-15 secondary, 16 unsynchronised.</summary>
		public int Stratum=1;
		public int Poll=6;
		public int Precision=-20;
		///<summary>Root delay in seconds (NTP short format, 16.16).</summary>
		public double RootDelaySeconds=0.002;
		///<summary>Root dispersion in seconds (NTP short format, 16.16).</summary>
		public double RootDispersionSeconds=0.001;
		///<summary>Four ASCII characters: a primary server's source (GPS), or a kiss code (RATE, DENY).</summary>
		public string ReferenceId="GPS";
		public ulong Reference;
		public ulong Originate;
		public ulong Receive;
		public ulong Transmit;
		///<summary>Bytes to send. More than 48 stands for extension fields or a MAC (zeros here); fewer cut the header short.</summary>
		public int Length=48;

		///<summary>The packet in network byte order.</summary>
		public byte[] ToBytes() {
			byte[] arrayHeader=new byte[48];
			arrayHeader[0]=(byte)(((LeapIndicator & 0x3)<<6) | ((Version & 0x7)<<3) | (Mode & 0x7));
			arrayHeader[1]=(byte)Stratum;
			arrayHeader[2]=(byte)Poll;
			arrayHeader[3]=unchecked((byte)(sbyte)Precision);
			WriteBigEndian(arrayHeader,4,ToShortFormat(RootDelaySeconds),4);
			WriteBigEndian(arrayHeader,8,ToShortFormat(RootDispersionSeconds),4);
			for(int i=0;i<4;i++) {
				arrayHeader[12+i]=i<ReferenceId.Length ? (byte)ReferenceId[i] : (byte)0;
			}
			WriteBigEndian(arrayHeader,16,Reference,8);
			WriteBigEndian(arrayHeader,24,Originate,8);
			WriteBigEndian(arrayHeader,32,Receive,8);
			WriteBigEndian(arrayHeader,40,Transmit,8);
			byte[] arrayPacket=new byte[Length];
			Array.Copy(arrayHeader,arrayPacket,Math.Min(Length,48));
			return arrayPacket;
		}

		///<summary>The NTP timestamp of a UTC time: seconds since 1900-01-01 00:00:00 UTC modulo 2^32 (so era 1, from 2036-02-07 06:28:16 UTC,
		///starts at 0 again) and the fraction of the second in units of 2^-32.</summary>
		public static ulong Timestamp(DateTime dateTimeUtc) {
			decimal secondsSince1900=(decimal)(dateTimeUtc-new DateTime(1900,1,1,0,0,0,DateTimeKind.Utc)).Ticks/TimeSpan.TicksPerSecond;
			decimal secondsWhole=decimal.Floor(secondsSince1900);
			decimal era=4294967296m;
			decimal secondsInEra=secondsWhole-decimal.Floor(secondsWhole/era)*era;
			decimal fraction=decimal.Floor((secondsSince1900-secondsWhole)*era);
			return ((ulong)secondsInEra<<32) | (ulong)fraction;
		}

		private static ulong ToShortFormat(double seconds) {
			return (ulong)Math.Round(seconds*65536.0);
		}

		private static void WriteBigEndian(byte[] arrayData,int index,ulong value,int count) {
			for(int i=count-1;i>=0;i--) {
				arrayData[index+i]=(byte)(value & 0xFF);
				value>>=8;
			}
		}
	}

	///<summary>One exchange: the client sends at T1 by its clock, the server receives at T2 and answers at T3 by its clock, the reply arrives at T4
	///by the client's clock. From the true offset, the one-way time and the server's processing time it builds T1 to T4, so that RFC 5905's
	///offset is exactly OffsetSeconds and its round-trip delay twice OneWaySeconds.</summary>
	public class NtpExchange {
		///<summary>T4, the client's clock when the reply arrives.</summary>
		public DateTime DestinationUtc;
		///<summary>How far the server's clock is ahead of the client's.</summary>
		public double OffsetSeconds;
		public double OneWaySeconds=0.015;
		public double ServerSeconds=0.0002;

		public NtpExchange(DateTime destinationUtc,double offsetSeconds) {
			DestinationUtc=destinationUtc;
			OffsetSeconds=offsetSeconds;
		}

		public DateTime T1 {
			get {
				return DestinationUtc.AddTicks(-ToTicks(2*OneWaySeconds+ServerSeconds));
			}
		}

		public DateTime T2 {
			get {
				return T1.AddTicks(ToTicks(OneWaySeconds+OffsetSeconds));
			}
		}

		public DateTime T3 {
			get {
				return T2.AddTicks(ToTicks(ServerSeconds));
			}
		}

		///<summary>The transmit time of the client's request.</summary>
		public ulong SentTransmit {
			get {
				return NtpPacket.Timestamp(T1);
			}
		}

		///<summary>A good reply to this exchange from a stratum-1 server.</summary>
		public NtpPacket MakeReply() {
			NtpPacket ntpPacket=new NtpPacket();
			ntpPacket.Originate=SentTransmit;
			ntpPacket.Receive=NtpPacket.Timestamp(T2);
			ntpPacket.Transmit=NtpPacket.Timestamp(T3);
			ntpPacket.Reference=NtpPacket.Timestamp(T2.AddSeconds(-16));
			return ntpPacket;
		}

		private static long ToTicks(double seconds) {
			return (long)Math.Round(seconds*TimeSpan.TicksPerSecond);
		}
	}
}
