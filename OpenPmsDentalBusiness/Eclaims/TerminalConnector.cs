using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeBase;

namespace OpenPmsDentalBusiness.Eclaims {
	///<summary>The dial-up modem terminal used by the BCBS Georgia clearinghouse. It talks to the modem through the framework's
	///System.IO.Ports.SerialPort; it used the RS232.dll serial library (unknown author and licence) before, as a thin wrapper with the same port
	///settings: COM1 until a port is opened, 9600 baud, 8 data bits, 1 stop bit, no parity, 1.5 second timeouts, DTR and RTS on.</summary>
	public class TerminalConnector:ITerminalConnector {

		public delegate void TextReceivedEventHandler(object sender,TextReceivedEventArgs e);
		///<summary>Event is fired when the terminal receives text from the host. Raised on the serial port's thread, as before.</summary>
		public event TextReceivedEventHandler TextReceivedEvent=null;
		///<summary>The object used to communicate to the host.</summary>
		private SerialPort _serialPort;
		///<summary>The character array of received bytes. Filled on the serial port's thread, so every access holds _lockRx.</summary>
		private StringBuilder RxBuff=new StringBuilder();
		private readonly object _lockRx=new object();
		///<summary>Each byte is one character and back (ISO-8859-1), as the received bytes have always been turned into characters.</summary>
		private static readonly Encoding _encodingBytes=Encoding.GetEncoding(28591);
		///<summary>CTRL-A. Start Of Header?</summary>
		private const char SOH=(char)1;
		///<summary>CTRL-D. End Of Transmission</summary>
		private const char EOT=(char)4;
		///<summary>CTRL-F. Positive ACKnowledgement</summary>
		private const char ACK=(char)6;
		///<summary>CTRL-U. Negative AcKnowledgement</summary>
		private const char NAK=(char)21;//
																		///<summary>CTRL-X. CANcel</summary>
		private const char CAN=(char)24;

		public TerminalConnector() {
			_serialPort=new SerialPort();
			_serialPort.PortName="COM1";
			_serialPort.BaudRate=9600;
			_serialPort.DataBits=8;
			_serialPort.StopBits=StopBits.One;
			_serialPort.Parity=Parity.None;
			_serialPort.ReadTimeout=1500;
			_serialPort.WriteTimeout=1500;
			_serialPort.Encoding=_encodingBytes;
			_serialPort.DataReceived+=new SerialDataReceivedEventHandler(serialPort_DataReceived);
		}

		///<summary>The serial port and its settings. For tests and diagnostics.</summary>
		public SerialPort GetSerialPort() {
			return _serialPort;
		}

		///<summary>Raised by the serial port when bytes arrive.</summary>
		private void serialPort_DataReceived(object sender,SerialDataReceivedEventArgs e) {
			byte[] arrayBytes;
			try {
				int count=_serialPort.BytesToRead;
				if(count<=0) {
					return;
				}
				arrayBytes=new byte[count];
				count=_serialPort.Read(arrayBytes,0,count);
				Array.Resize(ref arrayBytes,count);
			}
			catch(Exception ex) {
				ex.DoNothing();//The port was closed while reading.
				return;
			}
			ReceiveBytes(arrayBytes);
		}

		///<summary>Handles bytes received from the host: adds them to RxBuff, then raises TextReceivedEvent with the special codes shown as text.</summary>
		public void ReceiveBytes(byte[] arrayBytes) {
			StringBuilder strBuilder=new StringBuilder();
			lock(_lockRx) {
				//loop through each new char and handle it.
				for(int i=0;i<arrayBytes.Length;i++) {
					RxBuff.Append((char)arrayBytes[i]);
					if(IsSpecialCode((char)arrayBytes[i])) {
						strBuilder.Append(DisplaySpecialCode((char)arrayBytes[i]));
					}
					else {
						strBuilder.Append((char)arrayBytes[i]);
					}
				}
			}
			if(TextReceivedEvent != null) {
				TextReceivedEvent(this,new TextReceivedEventArgs(strBuilder.ToString()));
			}
		}

		///<summary>What has been received since RxBuff was last cleared.</summary>
		private string GetRxBuff() {
			lock(_lockRx) {
				return RxBuff.ToString();
			}
		}

		public bool IsSpecialCode(char inputChar) {
			if((int)inputChar>=32) {
				return false;
			}
			return true;
			/*
			if(inputChar==SOH
				|| inputChar==EOT
				|| inputChar==ACK
				|| inputChar==NAK
				|| inputChar==CAN)
			{
				return true;
			}
			return false;*/
		}

		///<summary>Test if IsSpecialCode first.  Then, this returns a string representation for display purposes only.</summary>
		public string DisplaySpecialCode(char inputChar) {
			if(inputChar==SOH) {
				return "<SOH>";
			}
			if(inputChar==EOT) {
				return "<EOT>";
			}
			if(inputChar==ACK) {
				return "<ACK>";
			}
			if(inputChar==NAK) {
				return "<NAK>";
			}
			if(inputChar==CAN) {
				return "<CAN>";
			}
			if((int)inputChar==10//Line Feed
				|| (int)inputChar==13)//Carriage Return
			{
				return inputChar.ToString();
			}
			if((int)inputChar<32) {
				return "<"+((int)inputChar).ToString()+">";
			}
			return "";
		}

		public void ClearRxBuff() {
			lock(_lockRx) {
				RxBuff=new StringBuilder();
			}
		}

		public void CloseConnection() {
			_serialPort.Close();
		}

		public void Dial(string phone) {
			PurgeBuffers();
			string str="ATDT"+phone+"\r\n";
			_serialPort.Write(str);
		}

		///<summary>Discards whatever is waiting to be sent or read.</summary>
		private void PurgeBuffers() {
			_serialPort.DiscardOutBuffer();
			_serialPort.DiscardInBuffer();
		}

		///<summary>I don't think this actually works.</summary>
		public void DownloadXmodem(string filePath) {
			//send ACK
			int attempts=0;
			ClearRxBuff();
			while(attempts<5) {
				attempts++;
				Send(ACK);
				try {
					GetOneByte(5000);
					break;
				}
				catch {
					continue;
				}
				//if no response within given time, then try sending another ACK
				//Send up to about 5 ACKs before giving up
			}
			byte packetNumber=0;
			byte[] block;
			while(true) {//response){
				ClearRxBuff();
				//receive a block 132 bytes long
				block=GetBytes(132,30000);
				//1: verify SOH. If not present, send NAK.
				if(block[0]!=(byte)SOH) {
					Send(NAK);
					continue;
				}
				//2: verify packet number incrementing correctly. If wrong, send CAN
				if(block[1]!=packetNumber) {
					Send(CAN);

				}
				//3: verify 1's complement of packet number. If wrong, send CAN

				//4: temporarily store the packet of 128 bytes
				//5: validate the checksum.
				//If checksum correct, then add packet to the file. Send ACK
				//If incorrect, send NAK and prepare to receive this block again.
				//If EOT received instead of SOH, send NAK
				//If receive another EOT, send ACK




			}
			//transfer complete
			//Note: Allowed to to cancel at any time by sending a CAN byte (2 is better)

			/*
			byte response;
			for(int i=0;i<bytes.GetLength(0);i++){
			SendPacket:// (block):
				block=new byte[132];
				//1: SOH byte
				block[0]=(byte)SOH;
				//2: packet Number
				packetNumber=(byte)Math.IEEERemainder((double)i,(double)256);
				block[1]=packetNumber;
				//3: 1's complement of packet number
				block[2]=(byte)(255-packetNumber);
				//4: the packet
				bytes[i].CopyTo(block,3);
				//5: checksum
				block[131]=GetCheckSum(block);
				Send(block);
			GetResponse:*/
		}

		///<summary>Gets a single byte from the RxBuff. Throws exception if no byte received within the given time. Does not clear this byte from the RxBuff in any way.  That's up to the calling code.</summary>
		public byte GetOneByte(double timeoutMS) {
			if(timeoutMS>60000) {
				throw new Exception("Not allowed to wait longer than 60 seconds");
			}
			DateTime startTime=DateTime.Now;
			while(startTime.AddMilliseconds(timeoutMS)>DateTime.Now) {
				string rxBuff=GetRxBuff();
				if(rxBuff.Length>0) {
					return (byte)rxBuff[rxBuff.Length-1];
				}
				Application.DoEvents();
			}
			throw new Exception("Timed out.  No bytes received yet.");
		}

		public void OpenConnection(int modemPort) {
			_serialPort.PortName="COM"+modemPort;
			_serialPort.Open();
			_serialPort.DtrEnable=true;
			_serialPort.RtsEnable=true;
			ClearRxBuff();
		}

		public void Pause(double ms) {
			if(ms>20000) {
				throw new Exception("Not allowed to pause longer than 20 seconds");
			}
			DateTime startTime=DateTime.Now;
			while(startTime.AddMilliseconds(ms)>DateTime.Now) {
				Application.DoEvents();
			}
		}

		///<summary>Receives all text within the given timespan.</summary>
		public string Receive(double timeoutMS) {
			if(timeoutMS>20000) {
				throw new Exception("Not allowed to wait longer than 20 seconds");
			}
			DateTime startTime=DateTime.Now;
			while(startTime.AddMilliseconds(timeoutMS)>DateTime.Now) {
				Application.DoEvents();
			}
			return CharsToString(GetRxBuff());
		}

		public void Send(string str) {
			PurgeBuffers();
			str+="\r\n";
			_serialPort.Write(str);
		}

		public void Send(byte[] block) {
			PurgeBuffers();
			_serialPort.Write(block,0,block.Length);
		}

		public void Send(char singleChar) {
			PurgeBuffers();
			_serialPort.Write(new byte[] { (byte)singleChar },0,1);
		}

		public void ShowForm() {
			//We don't have anything to display.
		}

		public string Sout(string intputStr,int maxL,int minL) {
			string retStr=intputStr.ToUpper();
			//Debug.Write(retStr+",");
			retStr=Regex.Replace(retStr,//replaces characters in this input string
																	//Allowed: !"&'()+,-./;?=(space)
				"[^\\w!\"&'\\(\\)\\+,-\\./;\\?= ]",//[](any single char)^(that is not)\w(A-Z or 0-9) or one of the above chars.
				"");
			//retStr=Regex.Replace(retStr,"[_]","");//replaces _
			if(maxL!=-1) {
				if(retStr.Length>maxL) {
					retStr=retStr.Substring(0,maxL);
				}
			}
			if(minL!=-1) {
				if(retStr.Length<minL) {
					retStr=retStr.PadRight(minL,' ');
				}
			}
			//Debug.WriteLine(retStr);
			return retStr;
		}

		///<summary>Transmits a file using Xmodem protocol.</summary>
		public void UploadXmodem(string filePath) {
			//divide file into 128 byte packets
			byte[][] bytes;
			using(FileStream fs=File.Open(filePath,FileMode.Open,FileAccess.Read)){
				int numberPackets=(int)Math.Ceiling((double)fs.Length/128);
				bytes=new byte[numberPackets][];
				byte[] buffer;//this will usually be 128 bytes long, except for the last loop
				for(int i=0;i<numberPackets;i++){
					buffer=new byte[128];
					fs.Read(buffer,0,128);
					bytes[i]=new byte[128];
					buffer.CopyTo(bytes[i],0);
				}
			}
			//GetPacketNumber. If greater than 255, repeatedly subtract 256
			//1's complement = 255-packet#
			//checksum=sum of all bytes. If greater than 255, repeatedly subtract 256
			//Actual send:
			//wait for NAK:
			WaitFor((byte)NAK,50000);
			//if wait longer than given time, then throw timout exception
			byte[] block;
			byte packetNumber;
			byte response;
			for(int i=0;i<bytes.GetLength(0);i++){
			SendPacket:// (block):
				block=new byte[132];
				//1: SOH byte
				block[0]=(byte)SOH;
				//2: packet Number
				packetNumber=(byte)Math.IEEERemainder((double)i,(double)256);
				block[1]=packetNumber;
				//3: 1's complement of packet number
				block[2]=(byte)(255-packetNumber);
				//4: the packet
				bytes[i].CopyTo(block,3);
				//5: checksum
				block[131]=GetCheckSum(block);
				Send(block);
			GetResponse:
				ClearRxBuff();
				response=GetOneByte(40000);
				if(response==(byte)CAN){
					throw new Exception("Transfer cancelled by receiver");
				}
				else if(response==(byte)NAK){
					goto SendPacket;//resend
				}
				else if(response!=(byte)ACK){//if anything other than ACK received
					goto GetResponse;//get another byte
				}
				//Note: the gotos will not result in any infinite loops, because even in the worst case
				//scenario, the sender will give up and quit sending responses, resulting in a timeout.
			}
			//Once all blocks sent, sent EOT
			Send(EOT);
			//If receive NAK, send another EOT
			WaitFor((byte)NAK,40000);
			Send(EOT);
			//If receive ACK, then done.
			WaitFor((byte)ACK,40000);
			//Note.  Allowed to send a CAN byte (2 is better) between blocks to cancel upload
		}

		private byte GetCheckSum(byte[] input){
			byte retVal=0;
			for(int i=0;i<input.Length;i++){
				retVal+=input[i];
			}
			return (byte)Math.IEEERemainder((double)retVal,(double)256);
		}

		///<summary>Throws an exception if expected text not received within timeout period. Returns the response received.</summary>
		public string WaitFor(string expectedText,double timeoutMS) {
			return WaitFor(expectedText,null,timeoutMS);
		}

		///<summary>Throws an exception if expected text not received within timeout period. Returns the response received out of the given expectedReplies.
		///expectedText2 may be null when there is only one expected reply.</summary>
		public string WaitFor(string expectedText1,string expectedText2,double timeoutMS) {
			if(timeoutMS>60000) {
				throw new Exception("Not allowed to wait longer than 60 seconds");
			}
			DateTime startTime=DateTime.Now;
			while(startTime.AddMilliseconds(timeoutMS)>DateTime.Now) {
				string rxBuff=GetRxBuff();
				if(rxBuff.IndexOf(expectedText1)!=-1) {
					return expectedText1;
				}
				//A null second reply used to reach IndexOf(null), which throws, so a single expected reply only ever matched on the first look.
				if(expectedText2!=null && rxBuff.IndexOf(expectedText2)!=-1) {
					return expectedText2;
				}
				Application.DoEvents();
			}
			if(expectedText2==null) {
				throw new Exception("Timed out waiting for "+expectedText1
				+". Actual text received so far: '"+CharsToString(GetRxBuff())+"'");
			}
			throw new Exception("Timed out waiting for "+expectedText1
				+" or "+expectedText2
				+". Actual text received so far: '"+CharsToString(GetRxBuff())+"'");
		}

		///<summary>Throws an exception if expected text not received within timeout period. Returns the response received.</summary>
		public byte WaitFor(byte expectedByte,double timeoutMS) {
			if(timeoutMS>60000) {
				throw new Exception("Not allowed to wait longer than 60 seconds");
			}
			DateTime startTime=DateTime.Now;
			while(startTime.AddMilliseconds(timeoutMS)>DateTime.Now) {
				if(GetRxBuff().IndexOf((char)expectedByte)!=-1) {
					return expectedByte;
				}
				Application.DoEvents();
			}
			throw new Exception("Timed out waiting for byte "+expectedByte.ToString()
				+". Actual text received so far: '"+CharsToString(GetRxBuff())+"'");
		}

		///<summary>Converts the received characters to a display string. Any of the 5 special chars are transformed into meaningful display strings.</summary>
		private string CharsToString(string inputChars) {
			StringBuilder strBuilder=new StringBuilder();
			for(int i=0;i<inputChars.Length;i++) {
				if(IsSpecialCode(inputChars[i])) {
					strBuilder.Append(DisplaySpecialCode(inputChars[i]));
				}
				else {
					strBuilder.Append(inputChars[i]);
				}
			}
			return strBuilder.ToString();
		}

		///<summary>Gets a precise number of bytes from RxBuff. Throws an error if not received in time.  Also, make sure to clear RxBuff before and after using this function.</summary>
		public byte[] GetBytes(int numberOfBytes,double timeoutMS) {
			if(timeoutMS>60000){
				throw new Exception("Not allowed to wait longer than 60 seconds");
			}
			DateTime startTime=DateTime.Now;
			while(startTime.AddMilliseconds(timeoutMS) > DateTime.Now) {
				string rxBuff=GetRxBuff();
				if(rxBuff.Length >= numberOfBytes) {
					byte[] retVal=new byte[numberOfBytes];
					for(int i=0;i<numberOfBytes;i++) {
						retVal[i]=(byte)rxBuff[i];
					}
					return retVal;
				}
				Application.DoEvents();
			}
			throw new Exception("Timed out.  "+numberOfBytes.ToString()+" bytes not received yet.");
		}
	}

	public class TextReceivedEventArgs {
		public string TextReceived;

		public TextReceivedEventArgs(string textReceived) {
			TextReceived=textReceived;
		}
	}
}
