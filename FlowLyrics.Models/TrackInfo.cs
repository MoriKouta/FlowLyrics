using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowLyrics.Models;

public sealed record TrackInfo(string Title, string Artist, string Album, TimeSpan Duration, string? SpotifyTrackId = null)
{
	public string CacheKey
	{
		get
		{
			InlineArray4<object> buffer = default(InlineArray4<object>);
			buffer[0] = Title.Trim();
			buffer[1] = Artist.Trim();
			buffer[2] = Album.Trim();
			buffer[3] = Math.Max(0, (int)Math.Round(Duration.TotalSeconds));
			return string.Join("|", (ReadOnlySpan<object?>)buffer);
		}
	}

	public string StableIdentityKey
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(SpotifyTrackId))
			{
				string text = SpotifyTrackId.Trim();
				if (!text.StartsWith("spotify:track:", StringComparison.OrdinalIgnoreCase))
				{
					return "spotify:track:" + text.ToLowerInvariant();
				}
				return text.ToLowerInvariant();
			}
			InlineArray4<object> buffer = default(InlineArray4<object>);
			buffer[0] = NormalizeIdentityPart(Title);
			buffer[1] = NormalizeIdentityPart(Artist);
			buffer[2] = NormalizeIdentityPart(Album);
			buffer[3] = Math.Max(0, (int)Math.Round(Duration.TotalSeconds));
			string s = string.Join("|", (ReadOnlySpan<object?>)buffer);
			return "metadata:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
		}
	}

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Artist))
			{
				return Title + " — " + Artist;
			}
			return Title;
		}
	}

	private static string NormalizeIdentityPart(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant(), "\\s+", " ");
	}
}
