using System;

namespace FlowLyrics.Models;

public sealed record LyricLine(TimeSpan Time, string Text);
