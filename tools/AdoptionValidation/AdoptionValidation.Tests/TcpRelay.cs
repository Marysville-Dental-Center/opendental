// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AdoptionValidation.Tests {
	///<summary>Stands in for the SSH local forward of runbook step 0.0 ("ssh -N -L 13306:127.0.0.1:3306 admin@fox-db"): listens on a free port of
	///127.0.0.1 and relays every connection to the local MariaDB server, counting them. The tools run with --port set to the relay, as they run on
	///the office's admin PC with --port set to the forward. Loopback only; it never reaches another host.</summary>
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
			Task.Run(AcceptLoop);
		}

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
		}
	}
}
