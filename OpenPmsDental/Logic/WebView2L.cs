// The browser setup of the windows that cannot work without their WebView2 and show an error of their own when it fails (FormCareCreditWeb,
// FormPayConnect2, FormPayConnect2iFrame, the Images module's PDF print): the user sees exactly one message, never ODWebView2.Init's message
// and then the window's FriendlyException as well.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase.Controls;
using Task=System.Threading.Tasks.Task;

namespace OpenPmsDental {
	public static class WebView2L {
		///<summary>Shows a window's own error, with the exception's details (FriendlyException). LibWebTests replaces it to count the messages, so
		///no window shows.</summary>
		public static Action<string,Exception> ActionShowError=(textError,ex) => FriendlyException.Show(textError,ex);

		///<summary>Initializes odWebView2 (ODWebView2.Init), then runs funcSetup, the window's own setup of the control (its events and scripts),
		///which may be null. True when both worked. When either fails the result is false and the user has seen exactly one message: Init's own
		///when the WebView2 Runtime is missing or would not start (ODWebView2.TryShowInitFailure, which shows it here when Init held it back),
		///none for a window closed while its browser started, and textError with the error's details for anything else. Never throws.</summary>
		public static async System.Threading.Tasks.Task<bool> TryInit(ODWebView2 odWebView2,string textError,Func<Task> funcSetup=null) {
			try {
				await odWebView2.Init();
				if(funcSetup!=null) {
					await funcSetup();
				}
				return true;
			}
			catch(Exception ex) {
				if(!ODWebView2.TryShowInitFailure(ex)) {
					ActionShowError(textError,ex);
				}
				return false;
			}
		}
	}
}
