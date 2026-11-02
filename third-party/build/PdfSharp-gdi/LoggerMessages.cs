// The implementations of the [LoggerMessage] methods of PDFsharp's Logging/LogMessages.cs, as the logging source generator of
// Microsoft.Extensions.Logging.Abstractions writes them for upstream's build: a cached LoggerMessage.Define delegate per message, called only
// when the logger is enabled for its level. The generator comes with the NuGet package as a compiler analyzer, which this build does not
// restore, so the Open PMS - Dental build of PdfSharp-gdi compiles this file instead. Part of the build, not of the upstream source. A new [LoggerMessage] method upstream does not compile (CS8795) until it is added here.
// The program sets no logger factory, so PDFsharp logs to its NullLogger and none of these write anything.

using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;

namespace PdfSharp.Logging
{
    public static partial class LogMessages
    {
        static readonly LogDefineOptions SkipEnabledCheck = new() { SkipEnabledCheck = true };

        static readonly Action<ILogger, string?, Exception?> _pdfDocumentCreated = LoggerMessage.Define<string?>(LogLevel.Information,
            new EventId(PdfSharpEventId.DocumentCreated, PdfSharpEventName.DocumentCreated), "New PDF document '{DocumentName}' created.", SkipEnabledCheck);

        static readonly Action<ILogger, string?, Exception?> _pdfDocumentSaved = LoggerMessage.Define<string?>(LogLevel.Information,
            new EventId(PdfSharpEventId.DocumentSaved, PdfSharpEventName.DocumentSaved), "PDF document '{documentName}' saved.", SkipEnabledCheck);

        static readonly Action<ILogger, string?, Exception?> _newPdfPageCreated = LoggerMessage.Define<string?>(LogLevel.Information,
            new EventId(PdfSharpEventId.PageCreated, PdfSharpEventName.PageCreated), "New PDF page added to document '{documentName}'.", SkipEnabledCheck);

        static readonly Action<ILogger, string?, Exception?> _existingPdfPageAdded = LoggerMessage.Define<string?>(LogLevel.Information,
            new EventId(PdfSharpEventId.PageAdded, PdfSharpEventName.PageAdded), "Existing PDF page added to document '{documentName}'.", SkipEnabledCheck);

        static readonly Action<ILogger, string?, Exception?> _xGraphicsCreated = LoggerMessage.Define<string?>(LogLevel.Information,
            new EventId(PdfSharpEventId.GraphicsCreated, PdfSharpEventName.GraphicsCreated), "New XGraphics created from '{source}'.", SkipEnabledCheck);

        static readonly Action<ILogger, string, int, int, Exception?> _streamIssue = LoggerMessage.Define<string, int, int>(LogLevel.Error,
            new EventId(PdfSharpEventId.StreamIssue, PdfSharpEventName.StreamIssue),
            "{Status} {BytesRead} of {Length} bytes were received. " +
            "We strongly recommend using streams with PdfReader whose content is fully available. " +
            "Copy the stream containing the file to a MemoryStream for example.", SkipEnabledCheck);

        static readonly Action<ILogger, int, SizeType, int, Exception?> _endOfStreamReached = LoggerMessage.Define<int, SizeType, int>(LogLevel.Warning,
            new EventId(PdfSharpEventId.EndOfStreamReached, PdfSharpEventName.EndOfStreamReached),
            "End of stream reached while reading {Length} bytes at position {Position}, but got only {BytesRead} bytes.", SkipEnabledCheck);

        static readonly Action<ILogger, int, SizeType, PdfObjectID, Exception?> _skippedIllegalBlanksAfterStreamKeyword =
            LoggerMessage.Define<int, SizeType, PdfObjectID>(LogLevel.Warning,
            new EventId(PdfSharpEventId.SkippedIllegalBlanksAfterStreamKeyword, PdfSharpEventName.SkippedIllegalBlanksAfterStreamKeyword),
            "Skipped {BlankCount} illegal blanks behind keyword 'stream' at position {Position} in object {ObjectId}.", SkipEnabledCheck);

        static readonly Action<ILogger, SizeType, PdfObjectID, Exception?> _streamKeywordFollowedBySingleCR =
            LoggerMessage.Define<SizeType, PdfObjectID>(LogLevel.Warning,
            new EventId(PdfSharpEventId.StreamKeywordFollowedBySingleCR, PdfSharpEventName.StreamKeywordFollowedBySingleCR),
            "Keyword 'stream' followed by single CR is illegal at position {Position} in object {ObjectId}.", SkipEnabledCheck);

        static readonly Action<ILogger, SizeType, PdfObjectID, Exception?> _streamKeywordFollowedByIllegalBytes =
            LoggerMessage.Define<SizeType, PdfObjectID>(LogLevel.Warning,
            new EventId(PdfSharpEventId.StreamKeywordFollowedByIllegalBytes, PdfSharpEventName.StreamKeywordFollowedByIllegalBytes),
            "Keyword 'stream' followed by illegal bytes at position {Position} in object {ObjectId}.", SkipEnabledCheck);

        public static partial void PdfDocumentCreated(this ILogger logger, string? documentName)
        {
            if (logger.IsEnabled(LogLevel.Information))
                _pdfDocumentCreated(logger, documentName, null);
        }

        public static partial void PdfDocumentSaved(this ILogger logger, string? documentName)
        {
            if (logger.IsEnabled(LogLevel.Information))
                _pdfDocumentSaved(logger, documentName, null);
        }

        public static partial void NewPdfPageCreated(this ILogger logger, string? documentName)
        {
            if (logger.IsEnabled(LogLevel.Information))
                _newPdfPageCreated(logger, documentName, null);
        }

        public static partial void ExistingPdfPageAdded(this ILogger logger, string? documentName)
        {
            if (logger.IsEnabled(LogLevel.Information))
                _existingPdfPageAdded(logger, documentName, null);
        }

        public static partial void XGraphicsCreated(this ILogger logger, string? source)
        {
            if (logger.IsEnabled(LogLevel.Information))
                _xGraphicsCreated(logger, source, null);
        }

        public static partial void StreamIssue(this ILogger logger, string status, int bytesRead, int length)
        {
            if (logger.IsEnabled(LogLevel.Error))
                _streamIssue(logger, status, bytesRead, length, null);
        }

        public static partial void EndOfStreamReached(this ILogger logger, int length, SizeType position, int bytesRead)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                _endOfStreamReached(logger, length, position, bytesRead, null);
        }

        public static partial void SkippedIllegalBlanksAfterStreamKeyword(this ILogger logger, int blankCount, SizeType position, PdfObjectID objectId)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                _skippedIllegalBlanksAfterStreamKeyword(logger, blankCount, position, objectId, null);
        }

        public static partial void StreamKeywordFollowedBySingleCR(this ILogger logger, SizeType position, PdfObjectID objectId)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                _streamKeywordFollowedBySingleCR(logger, position, objectId, null);
        }

        public static partial void StreamKeywordFollowedByIllegalBytes(this ILogger logger, SizeType position, PdfObjectID objectId)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                _streamKeywordFollowedByIllegalBytes(logger, position, objectId, null);
        }
    }
}
