using FlowLyrics.Core;

namespace FlowLyrics.Models;

public sealed record MediaSessionUpdate(MediaMetadataState State, PlaybackSnapshot? Snapshot = null, MediaSessionInfo? Session = null);
