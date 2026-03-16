using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DevelopmentHub.Api.Logging;

public sealed class CallerInfoEnricher : ILogEventEnricher
{
    private const string FallbackClass = "UnknownClass";
    private const string FallbackMethod = "UnknownMethod";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var (className, methodName) = ResolveCaller();

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CallerClass", className));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CallerMethod", methodName));
    }

    private static (string ClassName, string MethodName) ResolveCaller()
    {
        try
        {
            var frames = new StackTrace(skipFrames: 2, fNeedFileInfo: false).GetFrames();
            if (frames is null)
                return (FallbackClass, FallbackMethod);

            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                if (method is null)
                    continue;

                var declaringType = method.DeclaringType;
                if (declaringType is null)
                    continue;

                var ns = declaringType.Namespace ?? string.Empty;
                if (!ns.StartsWith("DevelopmentHub.Api", StringComparison.Ordinal))
                    continue;

                if (ns.StartsWith("DevelopmentHub.Api.Logging", StringComparison.Ordinal))
                    continue;

                var methodName = UnwrapGeneratedMethodName(method);
                return (declaringType.Name, methodName);
            }
        }
        catch
        {
            // Fall back to static placeholders when stack inspection fails.
        }

        return (FallbackClass, FallbackMethod);
    }

    private static string UnwrapGeneratedMethodName(MethodBase method)
    {
        if (method.Name != nameof(IAsyncStateMachine.MoveNext))
            return method.Name;

        var declaringType = method.DeclaringType;
        var parentType = declaringType?.DeclaringType;
        if (parentType is null)
            return method.Name;

        var generatedName = declaringType?.Name ?? string.Empty;
        var startIndex = generatedName.IndexOf('<');
        var endIndex = generatedName.IndexOf('>');
        if (startIndex >= 0 && endIndex > startIndex)
            return generatedName[(startIndex + 1)..endIndex];

        return method.Name;
    }
}
