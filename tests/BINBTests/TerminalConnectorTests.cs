// The BCBS Georgia dial-up terminal on System.IO.Ports.SerialPort instead of RS232.dll. No modem or serial port is opened: the settings the
// RS232 library had are checked, and received bytes are fed in directly.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness.Eclaims;

namespace BINBTests {
	[TestClass]
	public class TerminalConnectorTests {
		[TestMethod]
		public void SerialPort_HasTheSettingsTheRs232LibraryHad() {
			TerminalConnector terminalConnector=new TerminalConnector();
			SerialPort serialPort=terminalConnector.GetSerialPort();
			Assert.AreEqual("COM1",serialPort.PortName);
			Assert.AreEqual(9600,serialPort.BaudRate);
			Assert.AreEqual(8,serialPort.DataBits);
			Assert.AreEqual(StopBits.One,serialPort.StopBits);
			Assert.AreEqual(Parity.None,serialPort.Parity);
			Assert.AreEqual(1500,serialPort.ReadTimeout);
			Assert.AreEqual(1500,serialPort.WriteTimeout);
			Assert.AreEqual(28591,serialPort.Encoding.CodePage,"One byte per character and back.");
			Assert.IsFalse(serialPort.IsOpen);
		}

		[TestMethod]
		public void ReceivedBytes_FillTheBuffer_AndShowSpecialCodes() {
			TerminalConnector terminalConnector=new TerminalConnector();
			List<string> listTexts=new List<string>();
			terminalConnector.TextReceivedEvent+=(sender,e) => listTexts.Add(e.TextReceived);
			terminalConnector.ReceiveBytes(new byte[] { (byte)'O',(byte)'K',13,10,6,1,4,21,24,7 });
			CollectionAssert.AreEqual(new[] { "OK\r\n<ACK><SOH><EOT><NAK><CAN><7>" },listTexts);
			Assert.AreEqual("OK",terminalConnector.WaitFor("OK",100));
			Assert.AreEqual("OK",terminalConnector.WaitFor("NO CARRIER","OK",100));
			Assert.AreEqual("OK\r\n<ACK><SOH><EOT><NAK><CAN><7>",terminalConnector.Receive(0));
		}

		[TestMethod]
		public void WaitFor_OneExpectedReply_KeepsWaitingUntilItArrives() {
			TerminalConnector terminalConnector=new TerminalConnector();
			terminalConnector.ReceiveBytes(new byte[] { (byte)'A' });
			//The RS232 version passed a null second reply to IndexOf, which throws, so a reply that was not there at the first look was never seen.
			string message=Expect.Throws<Exception>(() => terminalConnector.WaitFor("CONNECT",50)).Message;
			StringAssert.StartsWith(message,"Timed out waiting for CONNECT");
			terminalConnector.ReceiveBytes(new byte[] { (byte)'C',(byte)'O',(byte)'N',(byte)'N',(byte)'E',(byte)'C',(byte)'T' });
			Assert.AreEqual("CONNECT",terminalConnector.WaitFor("CONNECT",50));
		}

		[TestMethod]
		public void Bytes_ComeBackAsReceived() {
			TerminalConnector terminalConnector=new TerminalConnector();
			byte[] arrayBlock=Enumerable.Range(0,256).Select(x => (byte)x).ToArray();
			terminalConnector.ReceiveBytes(arrayBlock);
			CollectionAssert.AreEqual(arrayBlock,terminalConnector.GetBytes(256,100));
			Assert.AreEqual(255,terminalConnector.GetOneByte(100));
			Assert.AreEqual((byte)21,terminalConnector.WaitFor((byte)21,100));
			terminalConnector.ClearRxBuff();
			Expect.Throws<Exception>(() => terminalConnector.GetOneByte(20));
		}

		[TestMethod]
		public void OpenConnection_ToAPortThatDoesNotExist_Throws() {
			TerminalConnector terminalConnector=new TerminalConnector();
			Expect.Throws<Exception>(() => terminalConnector.OpenConnection(239));
			Assert.AreEqual("COM239",terminalConnector.GetSerialPort().PortName);
			terminalConnector.CloseConnection();//Closing a port that is not open is harmless, as before.
		}
	}
}
