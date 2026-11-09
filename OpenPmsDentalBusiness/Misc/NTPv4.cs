using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The NTP client of the time check (docs/specs/ehr-rebuild-plan.md, section 4). It sends one version 4 request (RFC 5905) to the first
	///IPv4 address of the server's name and checks the reply with NtpReply. It only measures: nothing here sets the computer's clock.</summary>
	public class NTPv4 {
		///<summary>The NTP port.</summary>
		public const int PORT=123;
		///<summary>How long one request waits for its reply.</summary>
		public const int RECEIVE_TIMEOUT_MS=2000;
		///<summary>The first byte of the request: leap indicator 0, version 4, mode 3 (client), RFC 5905 section 7.3. Open Dental sent 0x1B (version 3)
		///although its comment said version 4. 45 CFR 170.210(g) allows either.</summary>
		public const byte REQUEST_FIRST_BYTE=0x23;
		///<summary>The low bits of the transmit time's fraction that are random (2^20 of 2^32 parts of a second, about 0.24 ms): they lie below the
		///resolution of the computer's clock, and an off-path sender has to guess them as well as the time to forge a reply.</summary>
		private const int RANDOM_FRACTION_BITS=20;
		///<summary>The seam to the Event Log, which tests replace.</summary>
		public static Action<string> ActionWriteEventLog=WriteEventLogNative;

		public NTPv4() {
		}

		///<summary>Sends one request to the server and returns the offset of the server's clock from this computer's clock in milliseconds (positive
		///when this computer's clock is behind). Returns double.MaxValue when there was no reply or the reply broke a rule of NtpReply.TryParse; Query
		///gives the reason. Never throws. Kept for the callers of Open Dental's client; the time check and the manual window use Query.</summary>
		public double getTime(string nistServerUrl) {
			NtpResult ntpResult=Query(nistServerUrl);
			if(!ntpResult.IsValid) {
				return double.MaxValue;
			}
			return ntpResult.OffsetSeconds*1000;
		}

		///<summary>Sends one request to the first IPv4 address of serverName and returns the reply as NtpReply.Parse checked it, or a NoReply result
		///with the reason (no address, timeout, network error). Writes one Event Log entry. Never throws and never sets the clock.</summary>
		public NtpResult Query(string serverName) {
			string server=(serverName??"").Trim();
			NtpResult ntpResult=QueryServer(server);
			ntpResult.Server=server;
			WriteEventLog(ntpResult);
			return ntpResult;
		}

		private NtpResult QueryServer(string server) {
			if(server=="") {
				return NtpResult.NoReply("No time server is set.");
			}
			IPAddress ipAddress;
			try {
				ipAddress=GetFirstIPv4(Dns.GetHostAddresses(server));
			}
			catch(Exception ex) {
				return NtpResult.NoReply("The name "+server+" could not be resolved: "+ex.Message);
			}
			if(ipAddress==null) {
				return NtpResult.NoReply("The name "+server+" has no IPv4 address.");
			}
			try {
				using(Socket socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)) {
					socket.ReceiveTimeout=RECEIVE_TIMEOUT_MS;
					//A connected UDP socket receives datagrams from this address and port only.
					socket.Connect(new IPEndPoint(ipAddress,PORT));
					DateTime dateTimeSendUtc=DateTime.UtcNow;
					ulong transmit=MakeTransmitTimestamp(dateTimeSendUtc);
					//The round trip is timed with the stopwatch, not the coarser clock, and the destination time is the client's own send time
					//(the transmit time it sent) plus that round trip.
					DateTime dateTimeTransmitUtc=NtpReply.FromTimestamp(transmit,dateTimeSendUtc);
					Stopwatch stopwatch=Stopwatch.StartNew();
					socket.Send(MakeRequest(transmit));
					byte[] arrayBuffer=new byte[1024];
					int countReceived=socket.Receive(arrayBuffer);
					stopwatch.Stop();
					byte[] arrayReply=new byte[countReceived];
					Array.Copy(arrayBuffer,arrayReply,countReceived);
					return NtpReply.Parse(arrayReply,transmit,dateTimeTransmitUtc.Add(stopwatch.Elapsed));
				}
			}
			catch(SocketException ex) {
				if(ex.SocketErrorCode==SocketError.TimedOut) {
					return NtpResult.NoReply("No reply from "+server+" within "+(RECEIVE_TIMEOUT_MS/1000)+" seconds.");
				}
				return NtpResult.NoReply("The request to "+server+" failed: "+ex.Message);
			}
			catch(Exception ex) {
				return NtpResult.NoReply("The request to "+server+" failed: "+ex.Message);
			}
		}

		///<summary>The first IPv4 address of the list, or null. An IPv6 address first in the list is skipped (Open Dental's client threw on it).</summary>
		public static IPAddress GetFirstIPv4(IPAddress[] arrayAddresses) {
			if(arrayAddresses==null) {
				return null;
			}
			return arrayAddresses.FirstOrDefault(x => x!=null && x.AddressFamily==AddressFamily.InterNetwork);
		}

		///<summary>The 48-byte request: version 4, mode 3 (client), and the transmit time at bytes 40-47. Every other field is zero.</summary>
		public static byte[] MakeRequest(ulong transmit) {
			byte[] arrayPacket=new byte[NtpReply.LENGTH_MIN];
			arrayPacket[0]=REQUEST_FIRST_BYTE;
			NtpReply.WriteUInt64(arrayPacket,40,transmit);
			return arrayPacket;
		}

		///<summary>The transmit time for a request sent at dateTimeUtc, with the low RANDOM_FRACTION_BITS bits of the fraction random.</summary>
		public static ulong MakeTransmitTimestamp(DateTime dateTimeUtc) {
			byte[] arrayRandom=new byte[4];
			using(RandomNumberGenerator randomNumberGenerator=RandomNumberGenerator.Create()) {
				randomNumberGenerator.GetBytes(arrayRandom);
			}
			ulong mask=(1UL<<RANDOM_FRACTION_BITS)-1;
			ulong random=BitConverter.ToUInt32(arrayRandom,0) & mask;
			ulong transmit=(NtpReply.ToTimestamp(dateTimeUtc) & ~mask) | random;
			if(transmit==0) {//Never send a zero transmit time: no reply could be matched to it.
				transmit=1;
			}
			return transmit;
		}

		///<summary>One Event Log entry per request, through ProgramNames.EventLogSource, as Open Dental's client wrote. A computer where the source is
		///not registered (registering needs administrator rights, so only the per-machine install does it) gets no entry, and the check goes on.</summary>
		private static void WriteEventLog(NtpResult ntpResult) {
			string messageText="NTPv4 time request to "+ntpResult.Server+": "+ntpResult.Status+".";
			if(ntpResult.IsValid) {
				messageText+="\nOffset:  "+ntpResult.OffsetSeconds.ToString("0.000",CultureInfo.InvariantCulture)+" seconds"
					+"\nRound trip:  "+ntpResult.DelaySeconds.ToString("0.000",CultureInfo.InvariantCulture)+" seconds"
					+"\nStratum:  "+ntpResult.Stratum;
			}
			else {
				messageText+="\n"+ntpResult.Reason;
			}
			ActionWriteEventLog(messageText);
		}

		///<summary>Writes through ProgramNames.EventLogSource. Reached only through ActionWriteEventLog.</summary>
		private static void WriteEventLogNative(string messageText) {
			try {
				EventLog.WriteEntry(CodeBase.ProgramNames.EventLogSource,messageText,EventLogEntryType.Information);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}
	}

	///<summary>The rules for an NTP reply (RFC 5905; docs/specs/ehr-rebuild-plan.md, section 4.3). Pure functions: they read only their arguments.
	///A reply is valid when it has at least 48 bytes, leap indicator not 3, version 3 or 4, mode 4 (server), stratum 1 to 15, an originate time
	///equal bit for bit to the transmit time the client sent, receive and transmit times that are not zero, a round trip from 0 to 1 second and a
	///root delay / 2 + root dispersion below 1 second (MAXDIST). Stratum 0 is a kiss-o'-death reply. Each timestamp is placed in the NTP era nearest
	///the client's clock, so the rollover of February 2036 works, and the offset uses the client's own send time, never the copy in the reply.</summary>
	public class NtpReply {
		///<summary>The length of an NTP header without extension fields or a MAC.</summary>
		public const int LENGTH_MIN=48;
		///<summary>The largest round-trip delay accepted, in seconds.</summary>
		public const double DELAY_MAX_SECONDS=1;
		///<summary>RFC 5905's MAXDIST: the root delay / 2 + root dispersion must be below it, in seconds.</summary>
		public const double DISTANCE_MAX_SECONDS=1;
		///<summary>The first instant of NTP era 0, which every era-0 timestamp counts from.</summary>
		public static readonly DateTime EpochUtc=new DateTime(1900,1,1,0,0,0,DateTimeKind.Utc);
		///<summary>Seconds in one NTP era (2^32).</summary>
		private const long ERA_SECONDS=1L<<32;

		///<summary>Checks a reply under the rules of this class. reply is what was received (any length); sentTransmit is the transmit time the client
		///put in its request; destinationUtc is the client's clock when the reply arrived. Returns true for a valid reply, with offset the seconds
		///the server's clock is ahead of the client's (RFC 5905's theta; negative when the client is ahead). Otherwise offset is 0 and reason says
		///which rule failed.</summary>
		public static bool TryParse(byte[] reply,ulong sentTransmit,DateTime destinationUtc,out double offset,out string reason) {
			NtpResult ntpResult=Parse(reply,sentTransmit,destinationUtc);
			offset=ntpResult.IsValid ? ntpResult.OffsetSeconds : 0;
			reason=ntpResult.Reason;
			return ntpResult.IsValid;
		}

		///<summary>Checks a reply as TryParse does and returns the whole result: Valid, Invalid, or KissOfDeath with its code. A kiss-o'-death reply
		///counts only when its originate time matches, so a forged one from off the path cannot stop the checks.</summary>
		public static NtpResult Parse(byte[] reply,ulong sentTransmit,DateTime destinationUtc) {
			if(reply==null || reply.Length<LENGTH_MIN) {
				return NtpResult.Invalid("The reply has "+(reply==null ? 0 : reply.Length)+" bytes; an NTP reply has at least "+LENGTH_MIN+".");
			}
			int leapIndicator=reply[0]>>6;
			int version=(reply[0]>>3) & 0x07;
			int mode=reply[0] & 0x07;
			if(version!=3 && version!=4) {
				return NtpResult.Invalid("The reply is NTP version "+version+"; versions 3 and 4 are accepted.");
			}
			if(mode!=4) {
				return NtpResult.Invalid("The reply has mode "+mode+"; a server's reply has mode 4.");
			}
			if(sentTransmit==0) {
				return NtpResult.Invalid("The request had no transmit time, so no reply can be matched to it.");
			}
			ulong originate=ReadUInt64(reply,24);
			if(originate!=sentTransmit) {
				return NtpResult.Invalid("The reply's originate time is not the transmit time of this request (a reply to another request, or a forged one).");
			}
			int stratum=reply[1];
			if(stratum==0) {
				string kissCode=GetKissCode(reply);
				NtpResult ntpResultKiss=new NtpResult();
				ntpResultKiss.Status=EnumNtpResult.KissOfDeath;
				ntpResultKiss.KissCode=kissCode;
				ntpResultKiss.Reason="The server sent a kiss-o'-death reply with the code '"+kissCode+"' (RFC 5905, section 7.4).";
				return ntpResultKiss;
			}
			if(leapIndicator==3) {
				return NtpResult.Invalid("The server's clock is not synchronised (leap indicator 3).");
			}
			if(stratum>15) {
				return NtpResult.Invalid("The reply has stratum "+stratum+"; strata 1 to 15 are accepted.");
			}
			ulong receive=ReadUInt64(reply,32);
			ulong transmit=ReadUInt64(reply,40);
			if(receive==0 || transmit==0) {
				return NtpResult.Invalid("The reply's receive or transmit time is zero.");
			}
			double rootDelay=ShortToSeconds(ReadUInt32(reply,4));
			double rootDispersion=ShortToSeconds(ReadUInt32(reply,8));
			DateTime dateTimeDestination=ToUtc(destinationUtc);
			DateTime t1;
			DateTime t2;
			DateTime t3;
			try {
				t1=FromTimestamp(sentTransmit,dateTimeDestination);//The client's own send time.
				t2=FromTimestamp(receive,dateTimeDestination);
				t3=FromTimestamp(transmit,dateTimeDestination);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return NtpResult.Invalid("A timestamp of the reply is outside the dates this program can hold.");
			}
			long ticksDelay=(dateTimeDestination-t1).Ticks-(t3-t2).Ticks;
			long ticksOffset=((t2-t1).Ticks+(t3-dateTimeDestination).Ticks)/2;
			double delay=(double)ticksDelay/TimeSpan.TicksPerSecond;
			if(delay<0 || delay>DELAY_MAX_SECONDS) {
				return NtpResult.Invalid("The round trip took "+delay.ToString("0.000",CultureInfo.InvariantCulture)+" seconds; 0 to "
					+DELAY_MAX_SECONDS.ToString(CultureInfo.InvariantCulture)+" are accepted.");
			}
			double distance=rootDelay/2+rootDispersion;
			if(distance>=DISTANCE_MAX_SECONDS) {
				return NtpResult.Invalid("The server's root delay / 2 + root dispersion is "+distance.ToString("0.000",CultureInfo.InvariantCulture)
					+" seconds; it must be below "+DISTANCE_MAX_SECONDS.ToString(CultureInfo.InvariantCulture)+".");
			}
			NtpResult ntpResult=new NtpResult();
			ntpResult.Status=EnumNtpResult.Valid;
			ntpResult.OffsetSeconds=(double)ticksOffset/TimeSpan.TicksPerSecond;
			ntpResult.DelaySeconds=delay;
			ntpResult.Stratum=stratum;
			return ntpResult;
		}

		///<summary>The 64-bit NTP timestamp of a time (32 bits of seconds within its era, 32 bits of fraction). The era itself is not in a timestamp:
		///FromTimestamp finds it again from the clock. A local time is converted to UTC first; an unspecified one counts as UTC.</summary>
		public static ulong ToTimestamp(DateTime dateTime) {
			long ticks=(ToUtc(dateTime)-EpochUtc).Ticks;
			long seconds=FloorDiv(ticks,TimeSpan.TicksPerSecond);
			long ticksRemainder=ticks-seconds*TimeSpan.TicksPerSecond;
			ulong secondsInEra=(ulong)(seconds-FloorDiv(seconds,ERA_SECONDS)*ERA_SECONDS);
			ulong fraction=(ulong)((ticksRemainder<<32)/TimeSpan.TicksPerSecond);
			return (secondsInEra<<32) | fraction;
		}

		///<summary>The UTC time of a 64-bit NTP timestamp, placed in the NTP era nearest dateTimeNearUtc (the client's clock), so a timestamp after
		///the rollover of 2036-02-07 06:28:16 UTC reads as 2036 and not 1900. Throws for a time DateTime cannot hold.</summary>
		public static DateTime FromTimestamp(ulong timestamp,DateTime dateTimeNearUtc) {
			long secondsInEra=(long)(timestamp>>32);
			long ticksFraction=(long)(((timestamp & 0xFFFFFFFFUL)*(ulong)TimeSpan.TicksPerSecond)>>32);
			long secondsNear=FloorDiv((ToUtc(dateTimeNearUtc)-EpochUtc).Ticks,TimeSpan.TicksPerSecond);
			//The era that puts the timestamp within half an era (about 68 years) of the clock.
			long era=FloorDiv(secondsNear-secondsInEra+ERA_SECONDS/2,ERA_SECONDS);
			long seconds=secondsInEra+era*ERA_SECONDS;
			return EpochUtc.AddTicks(checked(seconds*TimeSpan.TicksPerSecond)+ticksFraction);
		}

		///<summary>An NTP short value (16 bits of seconds, 16 bits of fraction: root delay and root dispersion) in seconds.</summary>
		public static double ShortToSeconds(uint value) {
			return value/65536.0;
		}

		///<summary>Reads a big-endian 64-bit value (network byte order).</summary>
		public static ulong ReadUInt64(byte[] arrayData,int index) {
			ulong value=0;
			for(int i=0;i<8;i++) {
				value=(value<<8) | arrayData[index+i];
			}
			return value;
		}

		///<summary>Reads a big-endian 32-bit value (network byte order).</summary>
		public static uint ReadUInt32(byte[] arrayData,int index) {
			uint value=0;
			for(int i=0;i<4;i++) {
				value=(value<<8) | arrayData[index+i];
			}
			return value;
		}

		///<summary>Writes a 64-bit value big-endian (network byte order).</summary>
		public static void WriteUInt64(byte[] arrayData,int index,ulong value) {
			for(int i=7;i>=0;i--) {
				arrayData[index+i]=(byte)(value & 0xFF);
				value>>=8;
			}
		}

		///<summary>The kiss code of a stratum-0 reply: the reference ID as four ASCII letters (RFC 5905, section 7.4), printable characters only.</summary>
		private static string GetKissCode(byte[] reply) {
			StringBuilder stringBuilder=new StringBuilder();
			for(int i=12;i<16;i++) {
				char c=(char)reply[i];
				if(c>=' ' && c<='~') {
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString().Trim();
		}

		private static DateTime ToUtc(DateTime dateTime) {
			if(dateTime.Kind==DateTimeKind.Local) {
				return dateTime.ToUniversalTime();
			}
			return DateTime.SpecifyKind(dateTime,DateTimeKind.Utc);
		}

		private static long FloorDiv(long dividend,long divisor) {
			long quotient=dividend/divisor;
			if((dividend%divisor!=0) && ((dividend<0)!=(divisor<0))) {
				quotient--;
			}
			return quotient;
		}
	}

	///<summary>The result of one NTP request, as NtpReply checked it.</summary>
	public class NtpResult {
		public EnumNtpResult Status;
		///<summary>The server's name, as the request used it.</summary>
		public string Server="";
		///<summary>Seconds the server's clock is ahead of this computer's (RFC 5905's theta): positive when this computer's clock is behind. Only set
		///for a valid reply.</summary>
		public double OffsetSeconds;
		///<summary>The round-trip delay in seconds. Only set for a valid reply.</summary>
		public double DelaySeconds;
		///<summary>The server's stratum. Only set for a valid reply.</summary>
		public int Stratum;
		///<summary>The kiss code of a kiss-o'-death reply, for example RATE or DENY.</summary>
		public string KissCode="";
		///<summary>Why the reply is not valid. Empty for a valid reply.</summary>
		public string Reason="";

		public bool IsValid {
			get {
				return Status==EnumNtpResult.Valid;
			}
		}

		public static NtpResult Invalid(string reason) {
			NtpResult ntpResult=new NtpResult();
			ntpResult.Status=EnumNtpResult.Invalid;
			ntpResult.Reason=reason;
			return ntpResult;
		}

		public static NtpResult NoReply(string reason) {
			NtpResult ntpResult=new NtpResult();
			ntpResult.Status=EnumNtpResult.NoReply;
			ntpResult.Reason=reason;
			return ntpResult;
		}
	}

	///<summary>What came back from one NTP request.</summary>
	public enum EnumNtpResult {
		///<summary>A reply that passed every rule of NtpReply.</summary>
		Valid,
		///<summary>A reply that broke a rule of NtpReply.</summary>
		Invalid,
		///<summary>A matching reply with stratum 0: the server asks the client to stop (RFC 5905, section 7.4).</summary>
		KissOfDeath,
		///<summary>No reply: no address, a timeout or a network error.</summary>
		NoReply,
	}
}
