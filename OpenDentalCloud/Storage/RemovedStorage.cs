// Stand-in tasks for an A to Z storage type this program no longer supports (Dropbox; docs/specs/vendor-direct/dropbox.md, owner decision
// 2026-10-18). Each task fails without contacting any server, the same way a failed transfer did, so CloudStorage's callers keep their
// "no exceptions unless HasExceptions" contract.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading.Tasks;
using OpenDentalCloud.Core;

namespace OpenDentalCloud {
	///<summary>Thrown, or stored in TaskState.Error, when the database still names an A to Z storage type that this program no longer supports.</summary>
	public class StorageRemovedException:Exception {
		public StorageRemovedException(string message):base(message) {
		}
	}

	///<summary>Tasks for a removed storage type. They never touch the network or the disk: every one ends with a StorageRemovedException
	///in Error, which TaskState.Execute rethrows only when HasExceptions is set.</summary>
	public class RemovedStorage {

		private static Task Fail(string message) {
			TaskCompletionSource<bool> taskCompletionSource=new TaskCompletionSource<bool>();
			taskCompletionSource.SetException(new StorageRemovedException(message));
			return taskCompletionSource.Task;
		}

		public class Upload:TaskStateUpload {
			private readonly string _message;

			public Upload(string message) {
				_message=message;
			}

			protected override Task PerformIO() {
				return Fail(_message);
			}
		}

		public class Download:TaskStateDownload {
			private readonly string _message;

			public Download(string message) {
				_message=message;
			}

			protected override Task PerformIO() {
				return Fail(_message);
			}
		}

		public class Delete:TaskStateDelete {
			private readonly string _message;

			public Delete(string message) {
				_message=message;
			}

			protected override Task PerformIO() {
				return Fail(_message);
			}
		}

		public class Thumbnail:TaskStateThumbnail {
			private readonly string _message;

			public Thumbnail(string message) {
				_message=message;
			}

			protected override Task PerformIO() {
				return Fail(_message);
			}
		}

		public class ListFolders:TaskStateListFolders {
			private readonly string _message;

			public ListFolders(string message) {
				_message=message;
			}

			protected override Task PerformIO() {
				return Fail(_message);
			}
		}

		public class Move:TaskStateMove {
			private readonly string _message;

			public Move(string message) {
				_message=message;
			}

			protected override Task PerformIO() {
				CountTotal=1;
				CountFailed=1;//Nothing moved, so CloudStorage.Move reports failure.
				return Fail(_message);
			}
		}

		public class Copy:TaskStateCopy {
			private readonly string _message;

			public Copy(string message) {
				_message=message;
			}

			protected override Task PerformIO() {
				CountTotal=1;
				CountFailed=1;
				return Fail(_message);
			}
		}
	}
}
