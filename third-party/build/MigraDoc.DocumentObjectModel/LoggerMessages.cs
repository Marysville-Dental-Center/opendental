// The implementation of the [LoggerMessage] method of MigraDoc's DocumentObjectModel.Internals/LogMessages.cs, as the logging source
// generator of Microsoft.Extensions.Logging.Abstractions writes it for upstream's build: the level is a parameter, so the message is logged
// with ILogger.Log when the logger is enabled for that level, under the event name of the method (the attribute gives no EventId). The
// generator comes with the NuGet package as a compiler analyzer, which this build does not restore, so the Open PMS - Dental build of
// MigraDoc.DocumentObjectModel compiles this file instead. Part of the build, not of the upstream source. A new [LoggerMessage] method upstream does not compile (CS8795) until it is added here.
// The program sets no logger factory, so MigraDoc logs to its NullLogger and this writes nothing.

using Microsoft.Extensions.Logging;

namespace MigraDoc.DocumentObjectModel.Internals
{
    public static partial class MigraDocLogMessages
    {
        public static partial void ArgbValueIsConsideredEmptyColor(this ILogger logger, LogLevel level)
        {
            if (logger.IsEnabled(level))
                logger.Log(level, new EventId(0, nameof(ArgbValueIsConsideredEmptyColor)), "ARGB value 0 creates Color.Empty.");
        }
    }
}
