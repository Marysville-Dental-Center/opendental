using System;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Reflection;
using System.Web.Hosting;
using System.Web.Services;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDentalServer {
	/// <summary></summary>
	[WebService(Namespace="http://www.open-dent.com/OpenDentalServer")]
	[WebServiceBinding(ConformsTo=WsiProfiles.BasicProfile1_1)]
	[ToolboxItem(false)]
	public class ServiceMain:System.Web.Services.WebService {

		///<summary>Pass in a serialized dto.  It returns a dto which must be deserialized by the client.
		///Gives the business layer this request's host context, the values it read from System.Web itself before (MiddleTierHost): the
		///application's folder and virtual path from HostingEnvironment, and the client's address. ProcessDto sets them on this thread for the
		///request and clears them in a finally block when the request ends, so the pooled thread does not carry them into the next request.
		///The folder is also where the config file is; it was Server.MapPath(".") here, the same folder, since ServiceMain.asmx is in it.</summary>
		[WebMethod]
		public string ProcessRequest(string dtoString) {
			return DtoProcessor.ProcessDto(dtoString,HostingEnvironment.ApplicationPhysicalPath,HostingEnvironment.ApplicationVirtualPath,
				Context.Request.UserHostAddress);
		}

	}
}
