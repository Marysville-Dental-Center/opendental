// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ImagingCapture.Uvc.DirectShow {
	///<summary>A dedicated multithreaded-apartment thread that owns one camera's COM objects. Every call into those objects runs
	///here, so they are never used from an apartment that did not create them: no cross-apartment marshaling, and no dependence
	///on the caller's thread (the UI's single-threaded apartment, or a timer's thread-pool thread). The thread also waits on one
	///extra handle (the filter graph's event handle) and runs a handler on it when it is signaled.</summary>
	internal sealed class ComWorker:IDisposable {
		private readonly Thread _thread;
		private readonly object _lock=new object();
		private readonly Queue<WorkItem> _queueWork=new Queue<WorkItem>();
		private readonly AutoResetEvent _eventWork=new AutoResetEvent(false);
		private bool _isStopRequested;
		//Worker thread only.
		private WaitHandle _waitHandleSignal;
		private Action _actionSignal;

		public ComWorker(string threadName) {
			_thread=new Thread(Run);
			_thread.Name=threadName;
			_thread.IsBackground=true;
			_thread.SetApartmentState(ApartmentState.MTA);
			_thread.Start();
		}

		public bool IsWorkerThread {
			get {
				return Thread.CurrentThread==_thread;
			}
		}

		///<summary>Worker thread only. Also waits on waitHandle and runs action on this thread while it is signaled. The action
		///must make the handle unsignaled (drain what it signals). Null for both stops waiting on it.</summary>
		public void SetSignalHandler(WaitHandle waitHandle,Action action) {
			if(!IsWorkerThread) {
				throw new InvalidOperationException("SetSignalHandler must run on the worker thread.");
			}
			_waitHandleSignal=waitHandle;
			_actionSignal=action;
		}

		///<summary>Runs action on the worker thread and waits for it. Exceptions are rethrown here with their stack. Runs inline
		///when called on the worker thread. Throws ObjectDisposedException after Dispose.</summary>
		public void Invoke(Action action) {
			if(IsWorkerThread) {
				action();
				return;
			}
			WorkItem workItem=new WorkItem(action);
			lock(_lock) {
				if(_isStopRequested) {
					throw new ObjectDisposedException(nameof(ComWorker));
				}
				_queueWork.Enqueue(workItem);
			}
			_eventWork.Set();
			//Not disposed afterwards: the worker may still be inside Set when Wait returns.
			workItem.EventDone.Wait();
			if(workItem.ExceptionDispatchInfo!=null) {
				workItem.ExceptionDispatchInfo.Throw();
			}
		}

		public T Invoke<T>(Func<T> func) {
			T result=default(T);
			Invoke(() => { result=func(); });
			return result;
		}

		///<summary>Lets queued work finish, then ends the thread. Waits for it unless called on the worker thread itself.</summary>
		public void Dispose() {
			lock(_lock) {
				if(_isStopRequested) {
					return;
				}
				_isStopRequested=true;
			}
			_eventWork.Set();
			if(!IsWorkerThread) {
				_thread.Join();
			}
		}

		private void Run() {
			while(true) {
				WaitHandle[] arrayHandles=_waitHandleSignal==null ? new WaitHandle[] { _eventWork } : new WaitHandle[] { _eventWork,_waitHandleSignal };
				int index;
				try {
					index=WaitHandle.WaitAny(arrayHandles);
				}
				catch(Exception) {
					//The extra handle became invalid. Stop waiting on it.
					_waitHandleSignal=null;
					_actionSignal=null;
					continue;
				}
				if(index==1 && _actionSignal!=null) {
					try {
						_actionSignal();
					}
					catch(Exception) {
						//The handler reports its own problems. One that throws must not end the thread.
					}
				}
				while(true) {
					WorkItem workItem;
					lock(_lock) {
						if(_queueWork.Count==0) {
							if(_isStopRequested) {
								return;
							}
							break;
						}
						workItem=_queueWork.Dequeue();
					}
					workItem.Run();
				}
			}
		}

		private sealed class WorkItem {
			private readonly Action _action;
			public readonly ManualResetEventSlim EventDone=new ManualResetEventSlim(false);
			public ExceptionDispatchInfo ExceptionDispatchInfo;

			public WorkItem(Action action) {
				_action=action;
			}

			public void Run() {
				try {
					_action();
				}
				catch(Exception ex) {
					ExceptionDispatchInfo=ExceptionDispatchInfo.Capture(ex);
				}
				finally {
					EventDone.Set();
				}
			}
		}
	}
}
