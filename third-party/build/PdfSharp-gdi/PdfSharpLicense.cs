// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.
//
// Copyright (c) 2005-2026 empira Software GmbH, Troisdorf (Cologne Area), Germany.
// Licensed under the GNU Affero General Public License, version 3 or (at your option) any later version (LICENSE of PDFsharp:
// https://github.com/empira/PDFsharp/blob/v6.5.0/LICENSE).
//
// MODIFIED FILE (GNU AGPL version 3, section 5a). Changed by the Open PMS - Dental contributors on 2026-11-02.
// This is a modified version of src/foundation/src/PDFsharp/src/PdfSharp/root/PdfSharpLicense.cs of PDFsharp 6.5.0
// (https://github.com/empira/PDFsharp, tag v6.5.0, commit 96d5ef81d304d314228712e28e5d7ac3d5e29f86). The Open PMS - Dental build of
// PdfSharp-gdi.dll compiles it in place of that file (third-party/build/PdfSharp-gdi in the Open PMS - Dental repository).
// What changed: empira's file passes every call to PdfSharpLicenseImpl in PdfSharp.License.dll, a library empira publishes only prebuilt,
// without its source. This version no longer uses that library. PdfSharpLicenseImpl below holds the AGPL licence path itself, with the same
// public API and the same behaviour as the prebuilt library on that path: SetAgplLicense() takes no key, opens no network connection and
// writes no message; after it, CheckLicense() passes and every PDF saved names the licence in its Producer and Creator properties
// ("(AGPL license)"), as before. Without it, CheckLicense() throws PdfSharpLicenseException ("The loaded license is not valid."), as before.
// LoadLicense() refuses every key: nothing that needs empira's commercial licence is ever enabled.

using PdfSharp.Internal;
using PdfSharp.License;
using PdfSharp.Pdf;

namespace PdfSharp
{
    /// <summary>
    /// Static license manager for setting or loading the license keys.
    /// </summary>
    public static class PdfSharpLicense
    {
        static PdfSharpLicense()
        {
            PdfSharpLicenseImpl.Initialize();
        }

        /// <summary>
        /// Loads a license from a base64String.
        /// For more information see https://docs.pdfsharp.net/link/license-loading.html
        /// This build of PDFsharp is the AGPL build from source and takes no license key: it always throws
        /// PdfSharpLicenseException and leaves the current license as it is. Use SetAgplLicense.
        /// </summary>
        public static void LoadLicense(string licenseString)
        {
            PdfSharpLicenseImpl.LoadLicense(licenseString);
        }

        /// <summary>
        /// Sets the AGPL license.
        /// For more information see  https://docs.pdfsharp.net/link/license-loading.html
        /// </summary>
        public static void SetAgplLicense()
        {
            PdfSharpLicenseImpl.SetAgplLicense();
        }

        /// <summary>
        /// Unloads the current license.
        /// </summary>
        internal static void ResetLicense()
        {
            PdfSharpLicenseImpl.ResetLicense();
        }

        /// <summary>
        /// Returns true if the loaded license is valid for the current release line.
        /// </summary>
        internal static bool HasValidLicense()
        {
            return PdfSharpLicenseImpl.HasValidLicense();
        }

        /// <summary>
        /// Ensure that the current license is valid for the current release line.
        /// </summary>
        internal static void CheckLicense()
        {
            PdfSharpLicenseImpl.CheckLicense();
        }
    }
}

namespace PdfSharp.License
{
    /// <summary>
    /// The exception PDFsharp throws when it is used before a license is selected. Same name, namespace, base class and message as the
    /// one of empira's PdfSharp.License.dll.
    /// </summary>
    public class PdfSharpLicenseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PdfSharpLicenseException"/> class.
        /// </summary>
        public PdfSharpLicenseException(string message) : base(message)
        { }
    }

    /// <summary>
    /// The AGPL licence path of PDFsharp, in place of empira's closed PdfSharpLicenseImpl: a flag that SetAgplLicense sets, and the
    /// licence information PDFsharp writes into each PDF it saves. It reads no key from anywhere, connects to nothing and writes no message.
    /// </summary>
    static class PdfSharpLicenseImpl
    {
        /// <summary>
        /// What the licence adds after the name of the library in the Producer and Creator properties of a PDF.
        /// </summary>
        internal const string AgplLicenseInfo = " (AGPL license)";

        const string NotValidMessage = "The loaded license is not valid.";

        static volatile bool _isAgplLicenseSet;

        /// <summary>
        /// Nothing to do: no licence is loaded from a file, the registry, the environment or the network.
        /// </summary>
        internal static void Initialize()
        { }

        /// <summary>
        /// Refuses every key. The current licence stays as it is.
        /// </summary>
        internal static void LoadLicense(string licenseString)
        {
            throw new PdfSharpLicenseException("This build of PDFsharp is built from its AGPL source and takes no license key. "
                + "Call PdfSharpLicense.SetAgplLicense() to use it under the GNU Affero General Public License.");
        }

        internal static void SetAgplLicense()
        {
            PdfDocument.PrepareForSaveCallback = MarkAgplLicense;
            _isAgplLicenseSet = true;
        }

        internal static void ResetLicense()
        {
            _isAgplLicenseSet = false;
            PdfDocument.PrepareForSaveCallback = null;
        }

        internal static bool HasValidLicense()
        {
            return _isAgplLicenseSet;
        }

        internal static void CheckLicense()
        {
            if (!_isAgplLicenseSet)
                throw new PdfSharpLicenseException(NotValidMessage);
        }

        /// <summary>
        /// Called by PdfDocument.PrepareForSave after it has set the Producer property: names the AGPL licence after each mention of the
        /// library in the Producer ("PDFsharp 6.5.0-gdi (AGPL license)", also inside "(Original: ...)") and in the Creator when PDFsharp
        /// or MigraDoc set it ("PDFsharp 6.5.0 (www.pdfsharp.com) (AGPL license)", "empira MigraDoc 6.5.0 (www.pdfsharp.com) (AGPL license)").
        /// A Creator the application set is left alone. PrepareForSave removes the licence information from a Producer it reads back.
        /// </summary>
        static void MarkAgplLicense(object document)
        {
            if (document is not PdfDocument pdfDocument)
                return;
            var info = pdfDocument.Info;
            var library = PdfSharpProductVersionInformation.Creator.Trim();
            var producer = info.Elements.GetString(PdfDocumentInformation.Keys.Producer);
            if (producer.Length > 0)
                info.Elements.SetString(PdfDocumentInformation.Keys.Producer, producer.Replace(library, library + AgplLicenseInfo));
            var creator = info.Elements.GetString(PdfDocumentInformation.Keys.Creator);
            if (creator.Length > 0)
            {
                var creatorPdfSharp = PdfSharpProductVersionInformation.Producer;
                var creatorMigraDoc = $"empira MigraDoc {PdfSharpGitVersionInformation.InformationalVersion} ({PdfSharpProductVersionInformation.Url})";
                var marked = creator.Replace(creatorPdfSharp, creatorPdfSharp + AgplLicenseInfo)
                    .Replace(creatorMigraDoc, creatorMigraDoc + AgplLicenseInfo);
                if (marked != creator)
                    info.Elements.SetString(PdfDocumentInformation.Keys.Creator, marked);
            }
        }
    }
}
