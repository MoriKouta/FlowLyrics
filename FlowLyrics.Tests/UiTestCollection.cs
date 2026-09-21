using Xunit;

namespace FlowLyrics.Tests;

// Native WPF windows, tray integration and application-wide localization are
// shared desktop resources. Keep window tests out of concurrent test execution.
[CollectionDefinition("WPF UI", DisableParallelization = true)]
public sealed class UiTestCollection { }
