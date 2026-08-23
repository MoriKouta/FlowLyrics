using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public sealed class PersonalSyncStore
{
	private readonly string _directory;
	private readonly string _path;
	private readonly SemaphoreSlim _lock = new(1, 1);
	private readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};
	private List<PersonalSyncProfile>? _profiles;

	public string DirectoryPath => _directory;
	public string FilePath => _path;

	public event EventHandler? ProfilesChanged;

	public PersonalSyncStore(string appDataDirectory)
	{
		_directory = Path.Combine(appDataDirectory, "personal-sync");
		_path = Path.Combine(_directory, "profiles.json");
	}

	public async Task<PersonalSyncResolution> ResolveAsync(PersonalSyncContext context, CancellationToken cancellationToken = default)
	{
		await _lock.WaitAsync(cancellationToken);
		try
		{
			await EnsureLoadedAsync(cancellationToken);
			PersonalSyncProfile? sourceProfile = _profiles!
				.Where(profile => SameTrack(profile, context) && SameLyrics(profile, context)
					&& profile.Scope == PersonalSyncScope.Source && SameSource(profile, context))
				.OrderByDescending(profile => profile.UpdatedAtUtc)
				.FirstOrDefault();
			PersonalSyncProfile? globalProfile = _profiles!
				.Where(profile => SameTrack(profile, context) && SameLyrics(profile, context)
					&& profile.Scope == PersonalSyncScope.Track)
				.OrderByDescending(profile => profile.UpdatedAtUtc)
				.FirstOrDefault();
			bool differentLyrics = _profiles!.Any(profile => SameTrack(profile, context)
				&& !SameLyrics(profile, context)
				&& (profile.Scope == PersonalSyncScope.Track || SameSource(profile, context)));
			return new PersonalSyncResolution((sourceProfile ?? globalProfile)?.Clone(), differentLyrics);
		}
		finally
		{
			_lock.Release();
		}
	}

	public async Task<IReadOnlyList<PersonalSyncProfile>> ListAsync(CancellationToken cancellationToken = default)
	{
		await _lock.WaitAsync(cancellationToken);
		try
		{
			await EnsureLoadedAsync(cancellationToken);
			return _profiles!.OrderByDescending(profile => profile.UpdatedAtUtc).Select(profile => profile.Clone()).ToArray();
		}
		finally
		{
			_lock.Release();
		}
	}

	public async Task<PersonalSyncProfile> UpsertAsync(PersonalSyncProfile profile, CancellationToken cancellationToken = default)
	{
		PersonalSyncProfile normalized = Normalize(profile.Clone(), touchUpdatedAt: true);
		bool changed = false;
		await _lock.WaitAsync(cancellationToken);
		try
		{
			await EnsureLoadedAsync(cancellationToken);
			if (normalized.Mode == PersonalSyncMode.None)
			{
				changed = _profiles!.RemoveAll(candidate => candidate.Id == normalized.Id || SameProfileTarget(candidate, normalized)) > 0;
			}
			else
			{
				int index = _profiles!.FindIndex(candidate => candidate.Id == normalized.Id);
				if (index < 0) index = _profiles.FindIndex(candidate => SameProfileTarget(candidate, normalized));
				if (index >= 0)
				{
					normalized.Id = _profiles[index].Id;
					_profiles[index] = normalized;
				}
				else
				{
					_profiles.Add(normalized);
				}
				changed = true;
			}
			if (changed) await SaveAsync(cancellationToken);
		}
		finally
		{
			_lock.Release();
		}
		if (changed) ProfilesChanged?.Invoke(this, EventArgs.Empty);
		return normalized.Clone();
	}

	public async Task<bool> DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
	{
		bool removed;
		await _lock.WaitAsync(cancellationToken);
		try
		{
			await EnsureLoadedAsync(cancellationToken);
			removed = _profiles!.RemoveAll(profile => profile.Id == profileId) > 0;
			if (removed) await SaveAsync(cancellationToken);
		}
		finally
		{
			_lock.Release();
		}
		if (removed) ProfilesChanged?.Invoke(this, EventArgs.Empty);
		return removed;
	}

	public async Task<bool> DeleteForContextAsync(PersonalSyncContext context, bool sourceOnly, CancellationToken cancellationToken = default)
	{
		bool removed;
		await _lock.WaitAsync(cancellationToken);
		try
		{
			await EnsureLoadedAsync(cancellationToken);
			removed = _profiles!.RemoveAll(profile => SameTrack(profile, context) && SameLyrics(profile, context)
				&& (sourceOnly ? profile.Scope == PersonalSyncScope.Source && SameSource(profile, context) : profile.Scope == PersonalSyncScope.Track)) > 0;
			if (removed) await SaveAsync(cancellationToken);
		}
		finally
		{
			_lock.Release();
		}
		if (removed) ProfilesChanged?.Invoke(this, EventArgs.Empty);
		return removed;
	}

	private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
	{
		if (_profiles != null) return;
		if (!File.Exists(_path))
		{
			_profiles = new List<PersonalSyncProfile>();
			return;
		}
		try
		{
			await using FileStream stream = File.OpenRead(_path);
			_profiles = await JsonSerializer.DeserializeAsync<List<PersonalSyncProfile>>(stream, _jsonOptions, cancellationToken)
				?? new List<PersonalSyncProfile>();
			_profiles = _profiles.Select(profile => Normalize(profile, touchUpdatedAt: false)).ToList();
		}
		catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
		{
			try
			{
				Directory.CreateDirectory(_directory);
				File.Copy(_path, Path.Combine(_directory, $"profiles.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json"), overwrite: false);
			}
			catch { }
			_profiles = new List<PersonalSyncProfile>();
		}
	}

	private async Task SaveAsync(CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(_directory);
		string temporary = _path + ".tmp";
		await using (FileStream stream = File.Create(temporary))
		{
			await JsonSerializer.SerializeAsync(stream, _profiles, _jsonOptions, cancellationToken);
		}
		File.Move(temporary, _path, overwrite: true);
	}

	private static PersonalSyncProfile Normalize(PersonalSyncProfile profile, bool touchUpdatedAt)
	{
		profile.SchemaVersion = PersonalSyncProfile.CurrentSchemaVersion;
		profile.Track ??= new PersonalSyncTrackIdentity();
		profile.Source ??= new PersonalSyncSourceIdentity();
		if (string.IsNullOrWhiteSpace(profile.Source.Provider)) profile.Source.Provider = profile.Source.Source;
		if (string.IsNullOrWhiteSpace(profile.Source.ContextLabel)) profile.Source.ContextLabel = profile.Source.Source;
		profile.Lyrics ??= new PersonalSyncLyricsIdentity();
		profile.Anchors ??= new List<PersonalSyncAnchor>();
		profile.Segments ??= new List<PersonalSyncSegment>();
		profile.OffsetSeconds = Math.Clamp(profile.OffsetSeconds, -3600.0, 3600.0);
		profile.Anchors = profile.Anchors
			.Where(anchor => double.IsFinite(anchor.PlaybackSeconds) && double.IsFinite(anchor.LyricsSeconds))
			.Select(anchor => { anchor.PlaybackSeconds = Math.Max(0.0, anchor.PlaybackSeconds); anchor.LyricsSeconds = Math.Max(0.0, anchor.LyricsSeconds); return anchor; })
			.OrderBy(anchor => anchor.PlaybackSeconds).Take(200).ToList();
		profile.Segments = profile.Segments
			.Where(segment => double.IsFinite(segment.PlaybackStartSeconds) && double.IsFinite(segment.PlaybackEndSeconds)
				&& segment.PlaybackEndSeconds > segment.PlaybackStartSeconds)
			.Select(segment => { segment.PlaybackStartSeconds = Math.Max(0.0, segment.PlaybackStartSeconds); segment.PlaybackEndSeconds = Math.Max(segment.PlaybackStartSeconds, segment.PlaybackEndSeconds); segment.LyricsTimeSeconds = Math.Max(0.0, segment.LyricsTimeSeconds); return segment; })
			.OrderBy(segment => segment.PlaybackStartSeconds).Take(100).ToList();
		if (touchUpdatedAt || profile.UpdatedAtUtc == default) profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
		return profile;
	}

	private static bool SameProfileTarget(PersonalSyncProfile left, PersonalSyncProfile right) =>
		string.Equals(left.Track.StableTrackKey, right.Track.StableTrackKey, StringComparison.Ordinal)
		&& string.Equals(left.Lyrics.Key, right.Lyrics.Key, StringComparison.Ordinal)
		&& left.Scope == right.Scope
		&& (left.Scope == PersonalSyncScope.Track
			|| string.Equals(left.Source.StableSourceKey, right.Source.StableSourceKey, StringComparison.Ordinal));

	private static bool SameTrack(PersonalSyncProfile profile, PersonalSyncContext context) =>
		string.Equals(profile.Track.StableTrackKey, context.Track.StableTrackKey, StringComparison.Ordinal);

	private static bool SameLyrics(PersonalSyncProfile profile, PersonalSyncContext context) =>
		string.Equals(profile.Lyrics.Key, context.Lyrics.Key, StringComparison.Ordinal);

	private static bool SameSource(PersonalSyncProfile profile, PersonalSyncContext context) =>
		string.Equals(profile.Source.StableSourceKey, context.Source.StableSourceKey, StringComparison.Ordinal);
}
