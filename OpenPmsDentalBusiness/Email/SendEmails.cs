using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography.X509Certificates;
using System.Web;
using CodeBase;
using MimeKit;
using MailKit.Security;
using GmailApi=Google.Apis.Gmail.v1;
using Google;
using Google.Apis.Upload;
using System.Text.RegularExpressions;
using System.Linq;
using OpenPmsDentalBusiness.Microsoft365;

namespace OpenPmsDentalBusiness.Email {

	public static class SendEmail {

		public const int EMAIL_SEND_TIMEOUT_MS=180000;//3 minutes

		///<summary>Throws exceptions. Attempts to physically send the message over the network wire. This is used from wherever email needs to be
		///sent throughout the program. With x509Certificate2Signing (which must hold its private key) the message is signed (S/MIME, not
		///encrypted).</summary>
		public static void WireEmailUnsecure(BasicEmailAddress address,BasicEmailMessage emailMessage,X509Certificate2 x509Certificate2Signing=null) {
			try {
				WireEmailUnsecure(address,emailMessage,EMAIL_SEND_TIMEOUT_MS,x509Certificate2Signing);
			}
			catch(Exception ex){
				//System.Net.Mail puts the server's reply code in the message; MailKit (implicit SSL, signed messages) gives it as StatusCode.
				MailKit.Net.Smtp.SmtpCommandException smtpCommandException=ex as MailKit.Net.Smtp.SmtpCommandException;
				if(ex.Message.Split(' ').Contains("451") || (smtpCommandException!=null && (int)smtpCommandException.StatusCode==451)) {
					throw new ODException("Email provider is having temporary send issues. This could be due to high email demand. Please try again later.");
				}
				else {
					throw new ODException("Error sending email",ex);
				}
			}
		}

		///<summary>Throws exceptions. Attempts to physically send the message over the network wire. This is used from wherever email needs to be
		///sent throughout the program. With x509Certificate2Signing (which must hold its private key) the message is signed (S/MIME, not encrypted)
		///and goes through MailKit on every port, or through the Gmail or Microsoft 365 API for those addresses, as unsigned mail does.</summary>
		public static void WireEmailUnsecure(BasicEmailAddress address,BasicEmailMessage emailMessage,int emailSendTimeoutMs,
			X509Certificate2 x509Certificate2Signing=null)
		{
			if(address.AuthenticationType.In(BasicOAuthType.Google,BasicOAuthType.Microsoft)) {
				SendEmailOAuth(address,emailMessage,x509Certificate2Signing);
			}
			else {
				if(x509Certificate2Signing!=null) {//System.Net.Mail can't send a multipart/signed message as it is; MailKit sends the MIME as built.
					MimeMessage mimeMessage=CreateMimeMessageForSmtp(address,emailMessage);
					SignMimeMessage(mimeMessage,x509Certificate2Signing);
					SendSmtpMailKit(address,mimeMessage,GetSecureSocketOptions(address),emailSendTimeoutMs);
				}
				else if(IsImplicitSsl(address)) {//System.Net.Mail.SmtpClient only supports explicit SSL (STARTTLS); MailKit does TLS from the first byte.
					SendSmtpMailKit(address,CreateMimeMessageForSmtp(address,emailMessage),SecureSocketOptions.SslOnConnect,emailSendTimeoutMs);
				}
				else {//No SSL or explicit SSL on port 587  
					SmtpClient client=null;
					MailMessage message=null;
					try {
						client=new SmtpClient(address.SMTPserver,address.ServerPort);
						//The default credentials are not used by default, according to: 
						//http://msdn2.microsoft.com/en-us/library/system.net.mail.smtpclient.usedefaultcredentials.aspx
						client.Credentials=new NetworkCredential(address.EmailUsername.Trim(),address.EmailPassword);
						client.DeliveryMethod=SmtpDeliveryMethod.Network;
						client.EnableSsl=address.UseSSL;
						client.Timeout=emailSendTimeoutMs;
						message=BasicEmailMessageToMailMessage(emailMessage);
						client.Send(message);
					}
					finally {
						//Dispose of the client and messages here. For large customers, sending thousands of emails will start to fail until they restart the
						//app. Freeing memory here can prevent OutOfMemoryExceptions.
						client?.Dispose();
						if(message!=null) {
							if(message.Attachments!=null) {
								message.Attachments.ForEach(x => x.Dispose());
							}
							message.Dispose();
						}
					}
				}
			}
		}

		///<summary>Throws exceptions. Sends a MIME message exactly as it was built: a Direct message, which the Direct agent encrypted (and signed inside
		///the encryption). A password address sends it through MailKit on every port (TLS from the first byte on 465, STARTTLS when the address uses
		///SSL, plain text otherwise), a Gmail address through the Gmail API as the raw message, a Microsoft 365 address through Graph's MIME send, which
		///refuses a message larger than one request before any call (Graph rebuilds larger messages, and the encryption would be lost).
		///address.AccessToken must be a fresh bearer token for a Gmail or Microsoft 365 address (EmailMessages.GetOAuthAccessToken).</summary>
		public static void WireMimeMessage(BasicEmailAddress address,MimeMessage mimeMessage) {
			try {
				WireMimeMessage(address,mimeMessage,EMAIL_SEND_TIMEOUT_MS);
			}
			catch(Exception ex) {
				MailKit.Net.Smtp.SmtpCommandException smtpCommandException=ex as MailKit.Net.Smtp.SmtpCommandException;
				if(smtpCommandException!=null && (int)smtpCommandException.StatusCode==451) {
					throw new ODException("Email provider is having temporary send issues. This could be due to high email demand. Please try again later.");
				}
				throw new ODException("Error sending email",ex);
			}
		}

		///<summary>WireMimeMessage with the timeout of an SMTP send.</summary>
		public static void WireMimeMessage(BasicEmailAddress address,MimeMessage mimeMessage,int emailSendTimeoutMs) {
			if(address.AuthenticationType==BasicOAuthType.Google) {
				SendGmailMime(address,() => mimeMessage);
			}
			else if(address.AuthenticationType==BasicOAuthType.Microsoft) {
				ThrowIfTooLargeForMicrosoft(mimeMessage,isSigned:false,isEncrypted:true);
				SendMicrosoft(() => MicrosoftApiConnector.SendMime(address.EmailUsername,address.AccessToken,mimeMessage));//The MIME as built: Graph sends it as it is.
			}
			else {
				SendSmtpMailKit(address,mimeMessage,GetSecureSocketOptions(address),emailSendTimeoutMs);
			}
		}

		///<summary>Tests only (tests/LibraryUpdateTests): checks the certificate of the fake SMTP server, which no authority signed. Null in
		///production, where MailKit checks the server's certificate itself.</summary>
		internal static System.Net.Security.RemoteCertificateValidationCallback SmtpCertificateValidationForTests;

		///<summary>Port 465 means TLS from the first byte (implicit SSL, SMTPS); every other port is plain text or STARTTLS.</summary>
		internal static bool IsImplicitSsl(BasicEmailAddress address) {
			return address.ServerPort==465;
		}

		///<summary>How MailKit connects for the address, as System.Net.Mail and CDO did: TLS from the first byte on port 465, STARTTLS (required)
		///when the address uses SSL on another port, plain text otherwise.</summary>
		internal static SecureSocketOptions GetSecureSocketOptions(BasicEmailAddress address) {
			if(IsImplicitSsl(address)) {
				return SecureSocketOptions.SslOnConnect;
			}
			if(address.UseSSL) {
				return SecureSocketOptions.StartTls;
			}
			return SecureSocketOptions.None;
		}

		///<summary>Throws exceptions. Signs the body of the message (S/MIME, multipart/signed with a detached signature, SHA-256) with the certificate,
		///which must hold its private key. The signature also carries the certificate, so the receiver can see who signed. Signed with MimeKit's
		///Windows S/MIME context, which signs with the certificate's own key (a key that can't be exported works too).</summary>
		internal static void SignMimeMessage(MimeMessage mimeMessage,X509Certificate2 x509Certificate2) {
			if(!x509Certificate2.HasPrivateKey) {
				throw new ApplicationException(Lans.g("EmailMessages","The certificate for signing has no private key."));
			}
			MailboxAddress mailboxAddressSigner=mimeMessage.From.Mailboxes.FirstOrDefault()??new MailboxAddress("",x509Certificate2.GetNameInfo(X509NameType.EmailName,false));
			try {
				using(SigningSecureMimeContext signingSecureMimeContext=new SigningSecureMimeContext(x509Certificate2)) {
					mimeMessage.Body=CreateMultipartSigned(signingSecureMimeContext,mailboxAddressSigner,MimeKit.Cryptography.DigestAlgorithm.Sha256,mimeMessage.Body);
				}
			}
			catch(System.Security.Cryptography.CryptographicException ex) {
				throw new ApplicationException(Lans.g("EmailMessages","Failed to sign outgoing email message, probably due to permissions: ")+ex.Message,ex);
			}
		}

		///<summary>The multipart/signed entity MimeKit's MultipartSigned.Create builds, step for step: the entity is prepared for 7-bit transport,
		///written through the filters RFC 3156 and RFC 2015 ask for (an armored From, no trailing white space, CRLF line ends) and read back, so
		///that the part sent is byte for byte the part signed; then the detached signature (application/pkcs7-signature) follows it. Built here
		///because that overload of MultipartSigned.Create has an OpenPGP twin with a BouncyCastle parameter, which would need a BouncyCastle
		///reference in this project only to compile.</summary>
		private static MimeKit.Cryptography.MultipartSigned CreateMultipartSigned(MimeKit.Cryptography.SecureMimeContext secureMimeContext,
			MailboxAddress mailboxAddressSigner,MimeKit.Cryptography.DigestAlgorithm digestAlgorithm,MimeEntity mimeEntity)
		{
			mimeEntity.Prepare(EncodingConstraint.SevenBit,78);
			using(MimeKit.IO.MemoryBlockStream memoryBlockStream=new MimeKit.IO.MemoryBlockStream()) {
				using(MimeKit.IO.FilteredStream filteredStream=new MimeKit.IO.FilteredStream(memoryBlockStream)) {
					filteredStream.Add(new MimeKit.IO.Filters.ArmoredFromFilter());//RFC 3156, section 3.
					filteredStream.Add(new MimeKit.IO.Filters.TrailingWhitespaceFilter());//RFC 3156, section 5.4.
					filteredStream.Add(new MimeKit.IO.Filters.Unix2DosFilter(false));//RFC 2015 and RFC 3156, section 5.1.
					mimeEntity.WriteTo(filteredStream);
					filteredStream.Flush();
				}
				memoryBlockStream.Position=0;
				//The entity as it was written (and is signed), parsed again so that it is sent the same way.
				MimeParser mimeParser=new MimeParser(memoryBlockStream,MimeFormat.Entity,false);
				MimeEntity mimeEntityParsed=mimeParser.ParseEntity();
				memoryBlockStream.Position=0;
				MimePart mimePartSignature=secureMimeContext.Sign(mailboxAddressSigner,digestAlgorithm,memoryBlockStream);
				MimeKit.Cryptography.MultipartSigned multipartSigned=new MimeKit.Cryptography.MultipartSigned();
				multipartSigned.ContentType.Parameters["protocol"]=secureMimeContext.SignatureProtocol;
				multipartSigned.ContentType.Parameters["micalg"]=secureMimeContext.GetDigestAlgorithmName(digestAlgorithm);
				multipartSigned.Add(mimeEntityParsed);
				multipartSigned.Add(mimePartSignature);
				return multipartSigned;
			}
		}

		///<summary>MimeKit's Windows S/MIME context, signing with one certificate given to it (the one GetCertFromPrivateStore found in the NHINDPrivate
		///store) instead of looking for the sender's certificate in the user's personal store.</summary>
		private class SigningSecureMimeContext:MimeKit.Cryptography.WindowsSecureMimeContext {
			private readonly X509Certificate2 _x509Certificate2;

			public SigningSecureMimeContext(X509Certificate2 x509Certificate2) : base(StoreLocation.CurrentUser) {
				_x509Certificate2=x509Certificate2;
			}

			protected override X509Certificate2 GetSignerCertificate(MailboxAddress mailbox) {
				return _x509Certificate2;
			}
		}

		///<summary>The MIME message an SMTP server gets through MailKit: the same message the Gmail and Microsoft 365 sends build (CreateMIMEMsg),
		///plus what the CDO send of earlier versions set on each attachment: a Content-ID of the attachment's file name. Inline images keep the
		///Content-ID the html refers to (HttpUtility.UrlEncode of the file name), and every attachment has "Content-Disposition: attachment" with
		///the displayed file name.</summary>
		internal static MimeMessage CreateMimeMessageForSmtp(BasicEmailAddress address,BasicEmailMessage emailMessage) {
			MimeMessage mimeMessage=CreateMIMEMsg(address,emailMessage);
			//Every attachment, a MessagePart (an attached .eml) too, so that the index matches emailMessage.ListAttachments.
			List<MimeEntity> listMimeEntitiesAttachment=mimeMessage.Attachments.ToList();
			for(int i=0;i<listMimeEntitiesAttachment.Count && i<emailMessage.ListAttachments.Count;i++) {
				//Set as a header, as CDO set it: the A to Z file name can hold characters (a space) that the ContentId property would not take.
				listMimeEntitiesAttachment[i].Headers.Replace(HeaderId.ContentId,"<"+Path.GetFileName(emailMessage.ListAttachments[i].FullPath)+">");
			}
			return mimeMessage;
		}

		///<summary>Throws exceptions. Sends the message with MailKit: secureSocketOptions SslOnConnect for implicit SSL (port 465), StartTls when the
		///address uses SSL on another port, None otherwise. Signs in with the user name and password when the user name is not blank and the
		///server offers authentication; never with a token (Gmail and Microsoft 365 addresses send through their APIs).</summary>
		internal static void SendSmtpMailKit(BasicEmailAddress address,MimeMessage mimeMessage,SecureSocketOptions secureSocketOptions,int emailSendTimeoutMs) {
			using(MailKit.Net.Smtp.SmtpClient smtpClient=new MailKit.Net.Smtp.SmtpClient()) {//Disposing the client closes the connection.
				smtpClient.Timeout=emailSendTimeoutMs;
				if(SmtpCertificateValidationForTests!=null) {
					smtpClient.ServerCertificateValidationCallback=SmtpCertificateValidationForTests;
				}
				smtpClient.Connect(address.SMTPserver,address.ServerPort,secureSocketOptions);
				//A password address never signs in with a token.
				smtpClient.AuthenticationMechanisms.Remove("XOAUTH2");
				smtpClient.AuthenticationMechanisms.Remove("OAUTHBEARER");
				string userName=(address.EmailUsername??"").Trim();
				if(userName!="" && smtpClient.Capabilities.HasFlag(MailKit.Net.Smtp.SmtpCapabilities.Authentication)) {
					smtpClient.Authenticate(userName,address.EmailPassword??"");
				}
				smtpClient.Send(mimeMessage);
				smtpClient.Disconnect(true);
			}
		}

		public static MailMessage BasicEmailMessageToMailMessage(BasicEmailMessage basicMessage) {
			MailMessage message=new MailMessage();
			message.From=new MailAddress(basicMessage.FromAddress.Trim());
			if(!string.IsNullOrWhiteSpace(basicMessage.ToAddress)) {
				message.To.Add(basicMessage.ToAddress.Trim());
			}
			if(!string.IsNullOrWhiteSpace(basicMessage.CcAddress)) {
				message.CC.Add(basicMessage.CcAddress.Trim());
			}
			if(!string.IsNullOrWhiteSpace(basicMessage.BccAddress)) {
				message.Bcc.Add(basicMessage.BccAddress.Trim());
			}
			message.Subject=basicMessage.Subject;
			if(basicMessage.IsHtml) {
				//create alternate view in case browser cannot render html
				message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(basicMessage.HtmlBody
					,new System.Net.Mime.ContentType("text/html")));
				message.IsBodyHtml=true;
				message.Body=basicMessage.HtmlBody;
				if(!basicMessage.ListHtmlImages.IsNullOrEmpty()) {
					foreach(string imagePath in basicMessage.ListHtmlImages) {
						Attachment imgAttach=new Attachment(imagePath);
						imgAttach.ContentId=HttpUtility.UrlEncode(Path.GetFileName(imagePath));
						imgAttach.ContentDisposition.Inline=true;
						message.Attachments.Add(imgAttach);
					}
				}
			}
			else {
				message.IsBodyHtml=false;
				message.Body=basicMessage.BodyText;
			}
			if(!basicMessage.ListAttachments.IsNullOrEmpty()) {
				foreach(BasicEmailAttachment attachment in basicMessage.ListAttachments) {
					//@"C:\OpenDentalData\EmailAttachments\1");
					Attachment attach=new Attachment(attachment.FullPath);
					//"canadian.gif";
					attach.Name=attachment.DisplayedFilename;
					message.Attachments.Add(attach);
				}
			}
			return message;
		}

		///<summary>Raw MIME messages larger than this go to Gmail through the media upload URI (uploadType=resumable, message/rfc822) instead of
		///inside the JSON body. Google suggests simple requests only up to about 5 MB, and the base64url "raw" field grows the message by 4/3.</summary>
		public const int GMAIL_UPLOAD_THRESHOLD_BYTES=3500000;

		///<summary>Throws exceptions if failing to send emails or authenticate with Google/Microsoft. address.AccessToken must be a fresh bearer token
		///(EmailMessages.GetOAuthAccessToken). A GoogleApiException or Microsoft365Exception is rethrown as it is, so the caller can see the HTTP
		///status, refresh the token after a 401 and show a neutral message.</summary>
		private static void SendEmailOAuth(BasicEmailAddress address,BasicEmailMessage message,X509Certificate2 x509Certificate2Signing) {
			if(address.AuthenticationType==BasicOAuthType.Google) {
				SendGmailMime(address,() => {
					MimeMessage mimeMessage=CreateMIMEMsg(address,message);
					if(x509Certificate2Signing!=null) {
						SignMimeMessage(mimeMessage,x509Certificate2Signing);//Gmail sends the raw message as it is, the signature with it.
					}
					return mimeMessage;
				});
			}
			else if(address.AuthenticationType==BasicOAuthType.Microsoft) {
				//A signed message, or one with an attached e-mail, is built here (as MicrosoftApiConnector.SendEmail builds any other one), signed
				//if asked and checked before any call.
				MimeMessage mimeMessageBuilt=null;
				if(x509Certificate2Signing!=null || HasAttachedMessage(message)) {
					BasicEmailAddress basicEmailAddressFrom=new BasicEmailAddress();
					basicEmailAddressFrom.EmailUsername=address.EmailUsername;
					mimeMessageBuilt=CreateMIMEMsg(basicEmailAddressFrom,message);
					if(x509Certificate2Signing!=null) {
						SignMimeMessage(mimeMessageBuilt,x509Certificate2Signing);
					}
					ThrowIfTooLargeForMicrosoft(mimeMessageBuilt,x509Certificate2Signing!=null);
				}
				//create and send microsoft email.
				SendMicrosoft(() => {
					if(mimeMessageBuilt==null) {
						MicrosoftApiConnector.SendEmail(address.EmailUsername,address.AccessToken,message);
					}
					else {
						MicrosoftApiConnector.SendMime(address.EmailUsername,address.AccessToken,mimeMessageBuilt);//The MIME as built: Graph sends it as it is.
					}
				});
			}
		}

		///<summary>Throws exceptions. Sends the message funcMimeMessage builds (it is built inside, so that its errors read as the send's) through the
		///Gmail API as the raw message: in the JSON body, or through the media upload URI when it is larger than GMAIL_UPLOAD_THRESHOLD_BYTES. A
		///GoogleApiException is rethrown as it is.</summary>
		private static void SendGmailMime(BasicEmailAddress address,Func<MimeMessage> funcMimeMessage) {
			using GmailApi.GmailService gService=GoogleApiConnector.CreateGmailService(address);
			try {
				MimeMessage mimeMessage=funcMimeMessage();
				byte[] arrayMime=GetMimeBytes(mimeMessage);
				if(arrayMime.Length<=GMAIL_UPLOAD_THRESHOLD_BYTES) {
					GmailApi.Data.Message gmailMessage=CreateGmailMsg(arrayMime);
					gService.Users.Messages.Send(gmailMessage,address.EmailUsername).Execute();
				}
				else {
					//Large message: send the raw RFC 2822 bytes through the upload URI.
					using MemoryStream memoryStream=new MemoryStream(arrayMime);
					GmailApi.UsersResource.MessagesResource.SendMediaUpload sendMediaUpload=gService.Users.Messages.Send(new GmailApi.Data.Message(),
						address.EmailUsername,memoryStream,"message/rfc822");
					IUploadProgress uploadProgress=sendMediaUpload.Upload();
					if(uploadProgress.Status!=UploadStatus.Completed) {
						if(uploadProgress.Exception!=null) {
							System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(uploadProgress.Exception).Throw();
						}
						throw new ODException("Gmail did not accept the message upload.");
					}
				}
			}
			catch(GoogleApiException) {
				//Rethrown unchanged: EmailMessages reads the HTTP status and Google's reason (google.md 3.5) and refreshes the token after a 401.
				throw;
			}
			catch(Exception ex) {
				//This will bubble up to the UI level and be caught in a copypaste box.
				throw new Exception($"Error sending email with OAuth authorization: {ex.Message}");
			}
		}

		///<summary>Throws exceptions. Runs one Microsoft Graph send. A Microsoft365Exception is rethrown as it is; any other error becomes an Exception
		///with its message (or its inner exception's).</summary>
		private static void SendMicrosoft(Action actionSend) {
			try {
				actionSend();
			}
			catch(Microsoft365Exception) {
				//Rethrown unchanged: EmailMessages refreshes the token after a 401 and maps the kind to a neutral message (microsoft-365.md 3.5).
				throw;
			}
			catch(Exception ex) {
				//This will bubble up to the UI level and be caught in a copypaste box.
				string errorMessage=ex.Message;
				if(ex.InnerException!=null) {
					errorMessage=ex.InnerException.Message;
				}
				throw new Exception($"Error sending email with OAuth authorization: {errorMessage}");
			}
		}

		///<summary>True when MimeKit attaches one of the files as the message it holds (message/rfc822 by its extension: .eml, .nws), a MessagePart
		///rather than a MimePart; BodyBuilder decides by the same MimeTypes.GetMimeType.</summary>
		internal static bool HasAttachedMessage(BasicEmailMessage emailMessage) {
			if(emailMessage.ListAttachments.IsNullOrEmpty()) {
				return false;
			}
			return emailMessage.ListAttachments.Any(x => string.Equals(MimeTypes.GetMimeType(x.FullPath),"message/rfc822",StringComparison.OrdinalIgnoreCase));
		}

		///<summary>Throws ODException when a signed message, an encrypted (Direct) one, or one with an attached e-mail, is too large for Microsoft
		///365's one-request MIME send (/me/sendMail). A larger message is rebuilt as a draft with attachments, which would drop the signature or the
		///encryption, and the draft path (Microsoft365GraphClient.GetAttachmentParts) uploads MimePart attachments only, which would drop an attached
		///e-mail without a word.</summary>
		private static void ThrowIfTooLargeForMicrosoft(MimeMessage mimeMessage,bool isSigned,bool isEncrypted=false) {
			long length;
			using(MemoryStream memoryStream=new MemoryStream()) {
				mimeMessage.WriteTo(memoryStream);
				length=memoryStream.Length;
			}
			long lengthMax=new Microsoft365GraphOptions().MimeSendThresholdBytes;
			if(length<=lengthMax) {
				return;
			}
			string megabytes=" "+(lengthMax/1000000.0).ToString("0.#")+" MB ";
			if(isEncrypted) {
				throw new ODException(Lans.g("EmailMessages","An encrypted (Direct) message can be at most")+megabytes
					+Lans.g("EmailMessages","with Microsoft 365, which rebuilds larger messages without their encryption. Send it with smaller attachments."));
			}
			if(isSigned) {
				throw new ODException(Lans.g("EmailMessages","A digitally signed message can be at most")+megabytes
					+Lans.g("EmailMessages","with Microsoft 365, which rebuilds larger messages without their signature. Send it unsigned or with smaller attachments."));
			}
			throw new ODException(Lans.g("EmailMessages","A message with an attached e-mail (.eml) can be at most")+megabytes
				+Lans.g("EmailMessages","with Microsoft 365, which rebuilds larger messages without the attached e-mail. Send it with smaller attachments."));
		}

		///<summary>Helper method that creates a new MIME message based on the parameters (to, cc, bcc, from, and body)</summary>
		internal static MimeMessage CreateMIMEMsg(BasicEmailAddress emailAddress,BasicEmailMessage emailMessage) {
			MimeMessage mimeMsg=new MimeMessage();
			if(!emailMessage.Subject.IsNullOrEmpty()) {
				mimeMsg.Subject=emailMessage.Subject;
			}
			//Create MailAddress objects for all addresses.
			//MailMessage will automatically parse out "DisplayName" <email@address.com> and every variation.
			//We are using MailMessage as an in-between because its Add() function can automatically parse a single string containing multiple email addresses into a collection which is needed for MimeMessage.
			MailMessage msg=new MailMessage();
			msg.From=new MailAddress(emailMessage.FromAddress);
			mimeMsg.From.Add(new MailboxAddress(msg.From.DisplayName,msg.From.Address));
			if(!string.IsNullOrWhiteSpace(emailMessage.ToAddress)) {//A message can go to Cc or Bcc addresses only.
				msg.To.Add(emailMessage.ToAddress.Trim());
				mimeMsg.To.AddRange(msg.To.Select(x => new MailboxAddress(x.DisplayName,x.Address)));
			}
			if(!emailMessage.CcAddress.IsNullOrEmpty()) {
				msg.CC.Add(emailMessage.CcAddress.Trim());
				mimeMsg.Cc.AddRange(msg.CC.Select(x => new MailboxAddress(x.DisplayName,x.Address)));
			}
			if(!emailMessage.BccAddress.IsNullOrEmpty()) {
				msg.Bcc.Add(emailMessage.BccAddress.Trim());
				mimeMsg.Bcc.AddRange(msg.Bcc.Select(x => new MailboxAddress(x.DisplayName,x.Address)));
			}
			BodyBuilder body=new BodyBuilder();
			if(!emailMessage.ListHtmlImages.IsNullOrEmpty()) {
				foreach(string imagePath in emailMessage.ListHtmlImages) {
					MimeEntity imgAttach=body.LinkedResources.Add(imagePath);
					imgAttach.ContentId=HttpUtility.UrlEncode(Path.GetFileName(imagePath));
					imgAttach.ContentDisposition=new ContentDisposition(ContentDisposition.Inline);
				}
			}
			if(emailMessage.ListAttachments!=null) {
				foreach(BasicEmailAttachment attachmentPath in emailMessage.ListAttachments) {
					body.Attachments.Add(attachmentPath.FullPath);
				}
			}
			if(emailMessage.IsHtml) {
				body.HtmlBody=emailMessage.HtmlBody;
			}
			else {
				body.TextBody=emailMessage.BodyText;
			}
			for(int i=0;i<body.Attachments.Count();i++) {
				//A MimePart, or a MessagePart for a message/rfc822 file (.eml, .nws): MimeKit attaches a saved e-mail as the message it holds.
				MimeEntity attachment=body.Attachments[i];
				MimePart mimePart=attachment as MimePart;
				//Can break PDFs otherwise, since the file's 0A (/r) bytes are treated as line endings when getting encoded
				if(mimePart!=null && mimePart.ContentTransferEncoding==ContentEncoding.QuotedPrintable) {
					mimePart.ContentTransferEncoding=ContentEncoding.Base64;
				}
				//Set the email attachments FileName to be the DisplayedFilename, on any entity (MimePart.FileName sets both, but only on a MimePart).
				if(attachment.ContentDisposition==null) {
					attachment.ContentDisposition=new ContentDisposition(ContentDisposition.Attachment);
				}
				attachment.ContentDisposition.FileName=emailMessage.ListAttachments[i].DisplayedFilename;
				attachment.ContentType.Name=emailMessage.ListAttachments[i].DisplayedFilename;
			}
			mimeMsg.Body=body.ToMessageBody();
			return mimeMsg;
		}

		///<summary>The MIME message as the bytes MimeKit writes, decoded and re-encoded as UTF-8 text the way Gmail messages always were built.</summary>
		private static byte[] GetMimeBytes(MimeMessage mimeMsg) {
			using Stream stream=new MemoryStream();
			mimeMsg.WriteTo(stream);
			stream.Position=0;
			using StreamReader sr=new StreamReader(stream);
			string rawString=sr.ReadToEnd();
			return System.Text.Encoding.UTF8.GetBytes(rawString);
		}

		///<summary>Helper method that returns a ready-to-send Gmail Message</summary>
		private static GmailApi.Data.Message CreateGmailMsg(byte[] raw) {
			GmailApi.Data.Message gMsg=new GmailApi.Data.Message();
			gMsg.Raw=System.Convert.ToBase64String(raw);
			//What we send to Gmail must be a Base64 File/URL safe string.  We must convert our base64 to be URL safe. (replace - and _ with + and / respectively)
			gMsg.Raw=gMsg.Raw.Replace("+","-");
			gMsg.Raw=gMsg.Raw.Replace("/","_");
			return gMsg;
		}
	}
}
