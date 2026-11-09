// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AdoptionValidation.Tests {
	///<summary>Stands in for the SSH local forward of runbook step 0.0 ("ssh -N -L 13306:127.0.0.1:3306 admin@db-server"): listens on a free port of
	///127.0.0.1 and relays every connection to the local MariaDB server, counting them. The tools run with --port set to the relay, as they run on
	///the office's admin PC with --port set to the forward. Loopback only; it never reaches another host. A relay to the local test server
	///(port 3306) adds its port to OPMS_DB_TLS_RELAY_PORTS while it runs, so the tools reach the server through it with TLS and its CA
	///(DataConnectionBase.DevDbTls, SyntheticData's LocalTls), as they do on port 3306; a forward to another server never gets the test CA.</summary>
	public sealed class TcpRelay : IDisposable {
		private readonly TcpListener _listener;
		private readonly int _portTarget;
		private readonly CancellationTokenSource _cancel=new CancellationTokenSource();
		private int _countAccepted;

		public int Port { get; }

		public int CountAccepted {
			get { return Volatile.Read(ref _countAccepted); }
		}

		///<summary>portListen 0 picks a free port. The developers' dry run of the runbook uses 13306, the port of the runbook's forward.</summary>
		public TcpRelay(int portTarget,int portListen=0) {
			_portTarget=portTarget;
			_listener=new TcpListener(IPAddress.Loopback,portListen);
			_listener.Start();
			Port=((IPEndPoint)_listener.LocalEndpoint).Port;
			if(_portTarget==DataConnectionBase.DevDbTls.PORT) {
				SetRelayPort(Port,isAdded:true);
			}
			Task.Run(AcceptLoop);
		}

		///<summary>Adds the port to OPMS_DB_TLS_RELAY_PORTS of this process (and so of the programs it starts), or removes it.</summary>
		private static void SetRelayPort(int port,bool isAdded) {
			lock(_lockRelayPorts) {
				string value=Environment.GetEnvironmentVariable(DataConnectionBase.DevDbTls.RELAY_PORTS_VARIABLE)??"";
				System.Collections.Generic.List<string> listPorts=new System.Collections.Generic.List<string>(
					value.Split(new[] { ',' },StringSplitOptions.RemoveEmptyEntries));
				string portText=port.ToString(System.Globalization.CultureInfo.InvariantCulture);
				listPorts.RemoveAll(x => x.Trim()==portText);
				if(isAdded) {
					listPorts.Add(portText);
				}
				Environment.SetEnvironmentVariable(DataConnectionBase.DevDbTls.RELAY_PORTS_VARIABLE,listPorts.Count==0 ? null : string.Join(",",listPorts));
			}
		}

		private static readonly object _lockRelayPorts=new object();

		private async Task AcceptLoop() {
			while(!_cancel.IsCancellationRequested) {
				TcpClient client;
				try {
					client=await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
				}
				catch(Exception) {
					return;//Stopped.
				}
				Interlocked.Increment(ref _countAccepted);
				_=Task.Run(() => Relay(client));
			}
		}

		private async Task Relay(TcpClient client) {
			using(client)
			using(TcpClient server=new TcpClient()) {
				try {
					await server.ConnectAsync(IPAddress.Loopback,_portTarget).ConfigureAwait(false);
					NetworkStream streamClient=client.GetStream();
					NetworkStream streamServer=server.GetStream();
					Task taskUp=streamClient.CopyToAsync(streamServer,81920,_cancel.Token).ContinueWith(x => server.Client.Shutdown(SocketShutdown.Send));
					Task taskDown=streamServer.CopyToAsync(streamClient,81920,_cancel.Token).ContinueWith(x => client.Client.Shutdown(SocketShutdown.Send));
					await Task.WhenAll(taskUp,taskDown).ConfigureAwait(false);
				}
				catch(Exception) {
					//A connection closed by either side ends here.
				}
			}
		}

		public void Dispose() {
			_cancel.Cancel();
			_listener.Stop();
			if(_portTarget==DataConnectionBase.DevDbTls.PORT) {
				SetRelayPort(Port,isAdded:false);
			}
		}
	}
}
