; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
DLEVT001 | Deveel.Events | Warning | [Event] class must be partial.
DLEVT002 | Deveel.Events | Error | [Event] must specify DataVersion or an absolute DataSchema URI.
DLEVT003 | Deveel.Events | Warning | [Event] class must be public.
DLEVT004 | Deveel.Events | Error | [EventAttributes] name collides with event metadata.
DLEVT005 | Deveel.Events | Error | [EventAttributes] uses an invalid extension attribute name.



