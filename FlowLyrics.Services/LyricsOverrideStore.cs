using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public sealed class LyricsOverrideStore
{
	private readonly string _path;

	private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

	private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	private Dictionary<string, ManualLyricsSelection>? _selections;

	public string Path => _path;

	public LyricsOverrideStore(string appDataDirectory)
	{
		_path = System.IO.Path.Combine(appDataDirectory, "manual-selections.json");
	}

	public async Task<ManualLyricsSelection?> GetAsync(TrackInfo track, CancellationToken cancellationToken = default(CancellationToken))
	{
		await _lock.WaitAsync(cancellationToken);
		try
		{
			await EnsureLoadedAsync(cancellationToken);
			foreach (string identityKey in GetIdentityKeys(track))
			{
				if (!_selections.TryGetValue(identityKey, out ManualLyricsSelection? value)) continue;
				if (!string.Equals(identityKey, track.StableIdentityKey, StringComparison.Ordinal))
				{
					_selections[track.StableIdentityKey] = value;
					await SaveAsync(cancellationToken);
				}
				return value;
			}
			return null;
		}
		finally
		{
			_lock.Release();
		}
	}

	public async Task SetAsync(TrackInfo track, int lrclibId, CancellationToken cancellationToken = default(CancellationToken))
	{
		await _lock.WaitAsync(cancellationToken);
		try
		{
			await EnsureLoadedAsync(cancellationToken);
			_selections[track.StableIdentityKey] = new ManualLyricsSelection
			{
				LrclibId = lrclibId,
				SelectedManually = true,
				SavedAtUtc = DateTimeOffset.UtcNow
			};
			await SaveAsync(cancellationToken);
		}
		finally
		{
			_lock.Release();
		}
	}

	public async Task<bool> RemoveAsync(TrackInfo track, CancellationToken cancellationToken = default(CancellationToken))
	{
		await _lock.WaitAsync(cancellationToken);
		try
		{
			await EnsureLoadedAsync(cancellationToken);
			bool removed = GetIdentityKeys(track).Aggregate(false, (changed, key) => _selections.Remove(key) || changed);
			if (removed)
			{
				await SaveAsync(cancellationToken);
			}
			return removed;
		}
		finally
		{
			_lock.Release();
		}
	}

	private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
	{
		if (_selections != null)
		{
			return;
		}
		if (!File.Exists(_path))
		{
			_selections = new Dictionary<string, ManualLyricsSelection>(StringComparer.Ordinal);
			return;
		}
		try
		{
			await using FileStream stream = File.OpenRead(_path);
			_selections = (await JsonSerializer.DeserializeAsync<Dictionary<string, ManualLyricsSelection>>((Stream)stream, _jsonOptions, cancellationToken)) ?? new Dictionary<string, ManualLyricsSelection>(StringComparer.Ordinal);
		}
		catch (JsonException)
		{
			_selections = new Dictionary<string, ManualLyricsSelection>(StringComparer.Ordinal);
		}
	}

	private async Task SaveAsync(CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path));
		string temporaryPath = _path + ".tmp";
		await using (FileStream stream = File.Create(temporaryPath))
		{
			await JsonSerializer.SerializeAsync((Stream)stream, _selections, _jsonOptions, cancellationToken);
		}
		File.Move(temporaryPath, _path, overwrite: true);
	}

	private static IEnumerable<string> GetIdentityKeys(TrackInfo track)
	{
		yield return track.StableIdentityKey;
		yield return track.CacheKey;
		foreach (string legacyKey in track.LegacyIdentityKeys) yield return legacyKey;
	}
}
