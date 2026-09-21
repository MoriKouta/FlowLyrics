using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

/// <summary>Per-track opt-in/out, independent of files, LRCLIB selection and sync profiles.</summary>
internal sealed class LocalLrcPreferenceStore(string appDataDirectory)
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private string GetPath(TrackInfo track) => Path.Combine(appDataDirectory, "local-lrc-preferences",
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(track.StableIdentityKey))) + ".txt");
	public string GetSavedLyricsPath(TrackInfo track) => Path.ChangeExtension(GetPath(track), ".lrc");

	public async Task SaveLyricsAsync(TrackInfo track, string lyrics, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			string path = GetSavedLyricsPath(track);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			await File.WriteAllTextAsync(path + ".tmp", lyrics, cancellationToken);
			File.Move(path + ".tmp", path, overwrite: true);
		}
		finally { _gate.Release(); }
	}

	public async Task<bool?> GetAsync(TrackInfo track, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			string path = GetPath(track);
			if (!File.Exists(path)) return null;
			return bool.TryParse(await File.ReadAllTextAsync(path, cancellationToken), out bool enabled) ? enabled : null;
		}
		finally { _gate.Release(); }
	}

	public async Task SetAsync(TrackInfo track, bool enabled, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			string path = GetPath(track);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			await File.WriteAllTextAsync(path + ".tmp", enabled.ToString(), cancellationToken);
			File.Move(path + ".tmp", path, overwrite: true);
		}
		finally { _gate.Release(); }
	}
}
