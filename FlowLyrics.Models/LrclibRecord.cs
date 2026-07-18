using System.Text.Json.Serialization;

namespace FlowLyrics.Models;

public sealed class LrclibRecord
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("trackName")]
	public string? TrackName { get; set; }

	[JsonPropertyName("artistName")]
	public string? ArtistName { get; set; }

	[JsonPropertyName("albumName")]
	public string? AlbumName { get; set; }

	[JsonPropertyName("duration")]
	public double Duration { get; set; }

	[JsonPropertyName("instrumental")]
	public bool Instrumental { get; set; }

	[JsonPropertyName("plainLyrics")]
	public string? PlainLyrics { get; set; }

	[JsonPropertyName("syncedLyrics")]
	public string? SyncedLyrics { get; set; }
}
