using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public sealed class LyricsCacheStore
{
	private readonly string _directory;

	private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

	private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public string DirectoryPath => _directory;

	public LyricsCacheStore(string directory)
	{
		_directory = directory;
		Directory.CreateDirectory(_directory);
	}

	public string GetPath(TrackInfo track)
	{
		return GetPath(track.StableIdentityKey);
	}

	public async Task<LyricsCacheEntry?> ReadAsync(TrackInfo track, CancellationToken cancellationToken)
	{
		string path = GetPath(track);
		if (!File.Exists(path))
		{
			path = GetPath(track.CacheKey);
		}
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			LyricsCacheEntry entry;
			await using (FileStream stream = File.OpenRead(path))
			{
				entry = await JsonSerializer.DeserializeAsync<LyricsCacheEntry>((Stream)stream, _jsonOptions, cancellationToken);
			}
			if (entry == null || (!string.Equals(entry.TrackKey, track.StableIdentityKey, StringComparison.Ordinal) && !string.Equals(entry.TrackKey, track.CacheKey, StringComparison.Ordinal)))
			{
				return null;
			}
			if (entry.ExpiresAtUtc.HasValue && entry.ExpiresAtUtc.Value <= DateTimeOffset.UtcNow)
			{
				await DeleteAsync(track, cancellationToken);
				return null;
			}
			return entry;
		}
		catch (JsonException)
		{
			await DeleteAsync(track, cancellationToken);
			return null;
		}
		catch (IOException)
		{
			return null;
		}
	}

	public async Task WriteAsync(TrackInfo track, LyricsCacheEntry entry, CancellationToken cancellationToken)
	{
		entry.TrackKey = track.StableIdentityKey;
		Directory.CreateDirectory(_directory);
		await _writeLock.WaitAsync(cancellationToken);
		try
		{
			string path = GetPath(track);
			string temporaryPath = path + ".tmp";
			await using (FileStream stream = File.Create(temporaryPath))
			{
				await JsonSerializer.SerializeAsync((Stream)stream, entry, _jsonOptions, cancellationToken);
			}
			File.Move(temporaryPath, path, overwrite: true);
		}
		finally
		{
			_writeLock.Release();
		}
	}

	public async Task DeleteAsync(TrackInfo track, CancellationToken cancellationToken = default(CancellationToken))
	{
		await _writeLock.WaitAsync(cancellationToken);
		try
		{
			string path = GetPath(track);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			string path2 = path + ".tmp";
			if (File.Exists(path2))
			{
				File.Delete(path2);
			}
			string path3 = GetPath(track.CacheKey);
			if (!string.Equals(path, path3, StringComparison.OrdinalIgnoreCase))
			{
				if (File.Exists(path3))
				{
					File.Delete(path3);
				}
				string path4 = path3 + ".tmp";
				if (File.Exists(path4))
				{
					File.Delete(path4);
				}
			}
		}
		finally
		{
			_writeLock.Release();
		}
	}

	private string GetPath(string trackKey)
	{
		byte[] inArray = SHA256.HashData(Encoding.UTF8.GetBytes(trackKey));
		return Path.Combine(_directory, Convert.ToHexString(inArray).ToLowerInvariant() + ".json");
	}
}
