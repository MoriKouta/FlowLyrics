using System;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public sealed class LyricsServiceException : Exception
{
	public LyricsErrorKind Kind { get; }

	public LyricsServiceException(LyricsErrorKind kind, string message, Exception? innerException = null)
		: base(message, innerException)
	{
		Kind = kind;
	}
}
