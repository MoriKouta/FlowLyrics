using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public sealed class LyricsService : IDisposable
{
	private sealed record CachedJsonResponse(string Json, DateTimeOffset ExpiresAtUtc);

	private sealed record CacheReadResult(LyricsCacheEntry Entry, bool IsCurrentBuild, string SourceName);

	private static readonly TimeSpan NegativeCacheLifetime = TimeSpan.FromMinutes(1L);

	private static readonly TimeSpan CandidateCacheLifetime = TimeSpan.FromMinutes(10L);

	private static readonly TimeSpan RequestCacheLifetime = TimeSpan.FromMinutes(3L);

	private const int CurrentMatcherVersion = 5;

	private readonly HttpClient _httpClient;

	private readonly string _cacheDirectory;

	private readonly string _localLrcDirectory;

	private readonly FileSystemWatcher _localLrcWatcher;

	private readonly LyricsCacheStore _cacheStore;

	private readonly IReadOnlyList<LyricsCacheStore> _fallbackCacheStores;

	private readonly LyricsOverrideStore _overrideStore;

	private readonly AppLogger _logger;

	private readonly ConcurrentDictionary<string, CachedJsonResponse> _requestCache = new ConcurrentDictionary<string, CachedJsonResponse>(StringComparer.Ordinal);

	private readonly ConcurrentDictionary<string, LyricsCacheEntry> _positiveMemoryCache = new ConcurrentDictionary<string, LyricsCacheEntry>(StringComparer.Ordinal);

	private readonly object _serverBackoffLock = new object();

	private DateTimeOffset _serverBackoffUntilUtc;

	private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public string LyricsDirectory => _localLrcDirectory;

	public string ManualSelectionsPath => _overrideStore.Path;

	public event Action? LocalLrcFilesChanged;

	public LyricsService(string appDataDirectory)
	{
		_localLrcDirectory = Path.Combine(appDataDirectory, "lyrics-cache");
		_cacheDirectory = Path.Combine(appDataDirectory, "dev-cache", BuildInfo.CacheNamespace, "lyrics-cache");
		Directory.CreateDirectory(_localLrcDirectory);
		Directory.CreateDirectory(_cacheDirectory);
		_cacheStore = new LyricsCacheStore(_cacheDirectory);
		_fallbackCacheStores = CreateFallbackCacheStores(appDataDirectory, _cacheDirectory);
		_overrideStore = new LyricsOverrideStore(appDataDirectory);
		_logger = new AppLogger(appDataDirectory);
		_localLrcWatcher = new FileSystemWatcher(_localLrcDirectory, "*.lrc")
		{
			NotifyFilter = (NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite),
			IncludeSubdirectories = false,
			EnableRaisingEvents = true
		};
		_localLrcWatcher.Created += LocalLrcWatcher_Changed;
		_localLrcWatcher.Changed += LocalLrcWatcher_Changed;
		_localLrcWatcher.Deleted += LocalLrcWatcher_Changed;
		_localLrcWatcher.Renamed += LocalLrcWatcher_Changed;
		HttpClientHandler handler = new HttpClientHandler
		{
			AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli),
			UseCookies = false,
			MaxConnectionsPerServer = 2
		};
		_httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://lrclib.net/"),
			Timeout = Timeout.InfiniteTimeSpan
		};
		_httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FlowLyrics", BuildInfo.Version));
		_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
	}

	private static IReadOnlyList<LyricsCacheStore> CreateFallbackCacheStores(string appDataDirectory, string currentCacheDirectory)
	{
		List<string> directories = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			Path.GetFullPath(currentCacheDirectory)
		};
		string developmentRoot = Path.Combine(appDataDirectory, "dev-cache");
		if (Directory.Exists(developmentRoot))
		{
			try
			{
				foreach (DirectoryInfo buildDirectory in new DirectoryInfo(developmentRoot).EnumerateDirectories().OrderByDescending((DirectoryInfo directory) => directory.LastWriteTimeUtc).Take(12))
				{
					string candidate = Path.Combine(buildDirectory.FullName, "lyrics-cache");
					if (Directory.Exists(candidate) && seen.Add(Path.GetFullPath(candidate)))
					{
						directories.Add(candidate);
					}
				}
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
			}
		}
		string legacySharedCache = Path.Combine(appDataDirectory, "lyrics-cache");
		if (Directory.Exists(legacySharedCache) && seen.Add(Path.GetFullPath(legacySharedCache)))
		{
			directories.Add(legacySharedCache);
		}
		return directories.Select((string directory) => new LyricsCacheStore(directory)).ToArray();
	}

	private async Task<CacheReadResult?> ReadCacheAcrossBuildsAsync(TrackInfo track, CancellationToken cancellationToken)
	{
		LyricsCacheEntry? current = await _cacheStore.ReadAsync(track, cancellationToken);
		if (current != null)
		{
			return new CacheReadResult(current, true, BuildInfo.CacheNamespace);
		}
		foreach (LyricsCacheStore fallback in _fallbackCacheStores)
		{
			LyricsCacheEntry? cached = await fallback.ReadAsync(track, cancellationToken);
			if (cached?.CacheKind == "Positive")
			{
				string sourceName = Directory.GetParent(fallback.DirectoryPath)?.Name ?? "legacy";
				return new CacheReadResult(cached, false, sourceName);
			}
		}
		return null;
	}

	private async Task PromoteCacheAsync(TrackInfo track, CacheReadResult cache, CancellationToken cancellationToken)
	{
		if (cache.IsCurrentBuild)
		{
			return;
		}
		await _cacheStore.WriteAsync(track, cache.Entry, cancellationToken);
		await _logger.WriteAsync($"cache promoted source={cache.SourceName} target={BuildInfo.CacheNamespace} cacheKey={track.CacheKey}");
	}

	public async Task<LyricsLookupResult> GetLyricsAsync(TrackInfo track, bool forceRefresh, CancellationToken cancellationToken, Action? networkSearchStarting = null)
	{
		await LogTrackAsync(track, forceRefresh ? "lookup:force" : "lookup:auto");
		ManualLyricsSelection manual = await _overrideStore.GetAsync(track, cancellationToken);
		if (manual != null)
		{
			if (!forceRefresh)
			{
				CacheReadResult? manualCacheRead = await ReadCacheAcrossBuildsAsync(track, cancellationToken);
				LyricsCacheEntry? manualCache = manualCacheRead?.Entry;
				if (manualCache?.CacheKind == "Positive" && manualCache.LrclibId == manual.LrclibId && string.Equals(manualCache.SelectionMode, "Manual", StringComparison.OrdinalIgnoreCase))
				{
					await PromoteCacheAsync(track, manualCacheRead!, cancellationToken);
					_positiveMemoryCache[track.StableIdentityKey] = manualCache;
					await _logger.WriteAsync($"manual-cache accepted id={manual.LrclibId} cacheKey={track.CacheKey}");
					return FromCache(manualCache, selectedManually: true);
				}
			}
			LrclibRecord manualRecord = await GetRecordByIdAsync(manual.LrclibId, cancellationToken);
			if (manualRecord == null)
			{
				throw new LyricsServiceException(LyricsErrorKind.LrclibId, LocalizationService.TranslateCurrent("Could not load the selected LRCLIB ID."));
			}
			await _logger.WriteAsync($"manual-selection accepted id={manualRecord.Id} cacheKey={track.CacheKey}");
			return await CacheAndCreateResultAsync(track, manualRecord, "Manual", cancellationToken);
		}
		LyricsLookupResult local = await TryReadLocalLrcAsync(track, cancellationToken);
		if (local != null)
		{
			await _logger.WriteAsync("local-lrc accepted path=" + Path.GetFileName(local.LocalLrcPath) + " cacheKey=" + track.CacheKey);
			return local;
		}
		if (!forceRefresh && _positiveMemoryCache.TryGetValue(track.StableIdentityKey, out LyricsCacheEntry memoryCache) && IsValidPositiveCache(track, memoryCache))
		{
			await _logger.WriteAsync($"memory-cache accepted id={memoryCache.LrclibId} cacheKey={track.CacheKey}");
			return FromCache(memoryCache, selectedManually: false);
		}
		if (!forceRefresh)
		{
			CacheReadResult? cacheRead = await ReadCacheAcrossBuildsAsync(track, cancellationToken);
			LyricsCacheEntry? manualCache = cacheRead?.Entry;
			if (manualCache != null && manualCache.MatcherVersion < 5)
			{
				await _logger.WriteAsync($"cache rejected reason=matcher-upgrade oldVersion={manualCache.MatcherVersion} cacheKey={track.CacheKey}");
				if (cacheRead!.IsCurrentBuild)
				{
					await _cacheStore.DeleteAsync(track, cancellationToken);
				}
				manualCache = null;
			}
			if (manualCache != null)
			{
				if (manualCache.CacheKind == "Negative")
				{
					await _logger.WriteAsync($"negative-cache accepted cacheKey={track.CacheKey} expires={manualCache.ExpiresAtUtc:O}");
					return new LyricsLookupResult
					{
						Status = LyricsLookupStatus.NoLyrics,
						LoadedFromCache = true
					};
				}
				if (manualCache.CacheKind == "Candidates")
				{
					await _logger.WriteAsync("candidate-cache accepted ids=" + string.Join(',', manualCache.CandidateIds) + " cacheKey=" + track.CacheKey);
					return new LyricsLookupResult
					{
						Status = LyricsLookupStatus.CandidatesFound,
						LoadedFromCache = true
					};
				}
				if (IsValidPositiveCache(track, manualCache))
				{
					await PromoteCacheAsync(track, cacheRead!, cancellationToken);
					_positiveMemoryCache[track.StableIdentityKey] = manualCache;
					await _logger.WriteAsync($"positive-cache accepted id={manualCache.LrclibId} cacheKey={track.CacheKey}");
					return FromCache(manualCache, selectedManually: false);
				}
				await _logger.WriteAsync($"positive-cache rejected reason=metadata-conflict id={manualCache.LrclibId} cacheKey={track.CacheKey}");
				await _cacheStore.DeleteAsync(track, cancellationToken);
			}
		}
		networkSearchStarting?.Invoke();
		List<LrclibRecord> candidates = new List<LrclibRecord>();
		LrclibRecord exact = null;
		try
		{
			exact = await TryGetExactAsync(track, cancellationToken);
		}
		catch (LyricsServiceException ex) when ((uint)(ex.Kind - 1) <= 2u)
		{
			await _logger.WriteAsync($"exact lookup failed kind={ex.Kind} fallback=api/search cacheKey={track.CacheKey}");
		}
		if (exact != null)
		{
			candidates.Add(exact);
			LyricsCandidate exactEvaluation = LyricsMatcher.Evaluate(track, exact);
			await LogCandidateAsync(track, "api/get", exactEvaluation);
			if (exactEvaluation.AutoEligible && (exact.Instrumental || !string.IsNullOrWhiteSpace(exact.SyncedLyrics)))
			{
				return await CacheAndCreateResultAsync(track, exact, "Auto", cancellationToken);
			}
		}
		IReadOnlyList<LyricsCandidate> ranked = await SearchCandidatesAsync(track, new LyricsSearchRequest(track.Title, track.Artist, track.Album, string.Empty), cancellationToken, candidates, stopWhenSafeSyncedFound: true, null);
		LyricsCandidate selected = LyricsMatcher.SelectSafeAutomaticCandidate(track, ranked.Select((LyricsCandidate candidate) => candidate.Record));
		if (selected != null)
		{
			await _logger.WriteAsync($"automatic-selection accepted id={selected.Record.Id} score={selected.Score} cacheKey={track.CacheKey}");
			return await CacheAndCreateResultAsync(track, selected.Record, "Auto", cancellationToken);
		}
		if (ranked.Count > 0)
		{
			await _logger.WriteAsync("automatic-selection rejected reason=ambiguous candidates=" + string.Join(',', ranked.Select((LyricsCandidate candidate) => candidate.Record.Id)) + " cacheKey=" + track.CacheKey);
			await _cacheStore.WriteAsync(track, new LyricsCacheEntry
			{
				TrackKey = track.CacheKey,
				CacheKind = "Candidates",
				CandidateIds = ranked.Select((LyricsCandidate candidate) => candidate.Record.Id).ToList(),
				MatcherVersion = 5,
				SavedAtUtc = DateTimeOffset.UtcNow,
				ExpiresAtUtc = DateTimeOffset.UtcNow.Add(CandidateCacheLifetime)
			}, cancellationToken);
			return new LyricsLookupResult
			{
				Status = LyricsLookupStatus.CandidatesFound,
				Candidates = ranked
			};
		}
		await _cacheStore.WriteAsync(track, new LyricsCacheEntry
		{
			TrackKey = track.CacheKey,
			CacheKind = "Negative",
			MatcherVersion = 5,
			SavedAtUtc = DateTimeOffset.UtcNow,
			ExpiresAtUtc = DateTimeOffset.UtcNow.Add(NegativeCacheLifetime)
		}, cancellationToken);
		await _logger.WriteAsync("no-lyrics cached ttlMinutes=1 cacheKey=" + track.CacheKey);
		return new LyricsLookupResult
		{
			Status = LyricsLookupStatus.NoLyrics
		};
	}

	public Task<IReadOnlyList<LyricsCandidate>> SearchCandidatesAsync(TrackInfo track, LyricsSearchRequest request, CancellationToken cancellationToken)
	{
		return SearchCandidatesAsync(track, request, cancellationToken, null, stopWhenSafeSyncedFound: false, null);
	}

	public Task<IReadOnlyList<LyricsCandidate>> SearchCandidatesAsync(TrackInfo track, LyricsSearchRequest request, CancellationToken cancellationToken, IProgress<LyricsSearchProgress>? progress)
	{
		return SearchCandidatesAsync(track, request, cancellationToken, null, stopWhenSafeSyncedFound: false, progress);
	}

	public async Task<LyricsLookupResult> ApplyManualSelectionAsync(TrackInfo track, int lrclibId, CancellationToken cancellationToken)
	{
		LrclibRecord record = await GetRecordByIdAsync(lrclibId, cancellationToken);
		if (record == null)
		{
			throw new LyricsServiceException(LyricsErrorKind.LrclibId, LocalizationService.TranslateCurrent("Could not load the selected LRCLIB ID."));
		}
		if (!record.Instrumental && string.IsNullOrWhiteSpace(record.SyncedLyrics) && string.IsNullOrWhiteSpace(record.PlainLyrics))
		{
			throw new LyricsServiceException(LyricsErrorKind.NoSyncedLyrics, LocalizationService.TranslateCurrent("The selected LRCLIB record has no usable lyrics."));
		}
		try
		{
			await _overrideStore.SetAsync(track, lrclibId, cancellationToken);
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
		{
			throw new LyricsServiceException(LyricsErrorKind.ManualSelectionSave, LocalizationService.TranslateCurrent("Could not save the manual lyrics selection."), ex);
		}
		await ClearTrackCacheAsync(track, cancellationToken);
		await _logger.WriteAsync($"manual-selection saved identity={track.StableIdentityKey} id={lrclibId} cacheKey={track.CacheKey}");
		return await CacheAndCreateResultAsync(track, record, "Manual", cancellationToken);
	}

	public async Task ResetManualSelectionAsync(TrackInfo track, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			await _overrideStore.RemoveAsync(track, cancellationToken);
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
		{
			throw new LyricsServiceException(LyricsErrorKind.ManualSelectionSave, LocalizationService.TranslateCurrent("Could not reset the manual lyrics selection."), ex);
		}
		await ClearTrackCacheAsync(track, cancellationToken);
		await _logger.WriteAsync("manual-selection reset identity=" + track.StableIdentityKey + " cacheKey=" + track.CacheKey);
	}

	public async Task ClearTrackCacheAsync(TrackInfo track, CancellationToken cancellationToken = default(CancellationToken))
	{
		_ = 1;
		try
		{
			_positiveMemoryCache.TryRemove(track.StableIdentityKey, out LyricsCacheEntry _);
			await _cacheStore.DeleteAsync(track, cancellationToken);
			await _logger.WriteAsync("track-cache cleared cacheKey=" + track.CacheKey + " path=" + Path.GetFileName(_cacheStore.GetPath(track)));
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			throw new LyricsServiceException(LyricsErrorKind.CacheDelete, LocalizationService.TranslateCurrent("Could not clear this track's cache."), ex);
		}
	}

	public void ClearCache(TrackInfo track)
	{
		ClearTrackCacheAsync(track).GetAwaiter().GetResult();
	}

	public async Task<string> ImportLocalLrcFileAsync(TrackInfo track, string sourcePath, CancellationToken cancellationToken = default(CancellationToken))
	{
		string text = await File.ReadAllTextAsync(sourcePath, cancellationToken);
		if (LrcParser.Parse(text).Count == 0)
		{
			throw new LyricsServiceException(LyricsErrorKind.NoSyncedLyrics, LocalizationService.TranslateCurrent("Could not read timestamped LRC lyrics."));
		}
		string fileName = SanitizeFileName((string.IsNullOrWhiteSpace(track.Artist) ? string.Empty : (track.Artist + " - ")) + track.Title) + ".lrc";
		string destination = Path.Combine(_localLrcDirectory, fileName);
		if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
		{
			await File.WriteAllTextAsync(destination, text, Encoding.UTF8, cancellationToken);
		}
		await ClearTrackCacheAsync(track, cancellationToken);
		await _logger.WriteAsync("local-lrc imported file=" + fileName + " cacheKey=" + track.CacheKey);
		return destination;
	}

	public async Task<LyricsResult> ImportManualLrcAsync(TrackInfo track, string lrc, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (LrcParser.Parse(lrc).Count == 0)
		{
			throw new LyricsServiceException(LyricsErrorKind.NoSyncedLyrics, LocalizationService.TranslateCurrent("Could not read timestamped LRC lyrics."));
		}
		LyricsCacheEntry entry = new LyricsCacheEntry
		{
			TrackKey = track.CacheKey,
			SyncedLyrics = lrc,
			Source = "LOCAL LRC",
			SelectionMode = "Local",
			CacheKind = "Positive",
			MatcherVersion = 5,
			SavedAtUtc = DateTimeOffset.UtcNow
		};
		await _cacheStore.WriteAsync(track, entry, cancellationToken);
		return ToLyricsResult(entry);
	}

	public async Task<LrclibRecord?> GetRecordByIdAsync(int id, CancellationToken cancellationToken)
	{
		if (id <= 0)
		{
			return null;
		}
		return await GetJsonWithRetryAsync<LrclibRecord>($"api/get/{id}", 2, cancellationToken, throwOnNotFound: true);
	}

	public static Uri GetLrclibRecordUri(int id)
	{
		return new Uri($"https://lrclib.net/api/get/{id}");
	}

	public void Dispose()
	{
		_localLrcWatcher.EnableRaisingEvents = false;
		_localLrcWatcher.Dispose();
		_httpClient.Dispose();
	}

	private async Task<IReadOnlyList<LyricsCandidate>> SearchCandidatesAsync(TrackInfo track, LyricsSearchRequest request, CancellationToken cancellationToken, IEnumerable<LrclibRecord>? seed, bool stopWhenSafeSyncedFound, IProgress<LyricsSearchProgress>? progress)
	{
		Dictionary<int, LrclibRecord> records = new Dictionary<int, LrclibRecord>();
		if (seed != null)
		{
			foreach (LrclibRecord item in seed)
			{
				records[item.Id] = item;
			}
		}
		List<Exception> errors = new List<Exception>();
		HashSet<string> requestedUrls = new HashSet<string>(StringComparer.Ordinal);
		int successfulRequests = 0;
		int completedRequests = 0;
		bool haltFurtherRequests = false;
		CancellationToken token;
		using (CancellationTokenSource totalCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
		{
			totalCancellation.CancelAfter(TimeSpan.FromSeconds(90L));
			token = totalCancellation.Token;
			foreach (string knownArtistSearchAlias in LyricsMatcher.GetKnownArtistSearchAliases(request.Artist))
			{
				await RunSearchAsync("fields:known-artist-alias", Fields(request.Title, knownArtistSearchAlias, null));
				if (ShouldStop())
				{
					break;
				}
			}
			if (!ShouldStop())
			{
				await RunSearchAsync("fields:title+artist", Fields(request.Title, request.Artist, null));
			}
			if (!stopWhenSafeSyncedFound && !ShouldStop())
			{
				await RunSearchAsync("fields:title+artist+album", Fields(request.Title, request.Artist, request.Album));
			}
			if (!ShouldStop())
			{
				await RunSearchAsync("fields:title", Fields(request.Title, null, null));
			}
			if (!ShouldStop() && (!stopWhenSafeSyncedFound || records.Count == 0))
			{
				await RunSearchAsync("q:title+artist", Query((request.Title + " " + request.Artist).Trim()));
			}
			if (!ShouldStop())
			{
				await RunSearchAsync("q:keyword", Query(request.Keyword));
			}
			IReadOnlyList<LyricsCandidate> source = LyricsMatcher.RankCandidates(track, records.Values);
			if (!ShouldStop() && !source.Any((LyricsCandidate candidate) => candidate.AutoEligible && !string.IsNullOrWhiteSpace(candidate.Record.SyncedLyrics)))
			{
				foreach (LrclibRecord item2 in DiscoverAlternativeArtistQueries(track, records.Values).Take(2))
				{
					await RunSearchAsync("fields:lrclib-artist-alias", Fields(item2.TrackName, item2.ArtistName, null));
					if (ShouldStop())
					{
						break;
					}
				}
			}
			IReadOnlyList<LyricsCandidate> ranked = LyricsMatcher.RankCandidates(track, records.Values);
			await LogCandidatesAsync(track, "api/search", ranked);
			if (successfulRequests == 0 && errors.Count > 0)
			{
				List<Exception> list = errors;
				throw list[list.Count - 1];
			}
			if (stopWhenSafeSyncedFound && errors.Count > 0 && !HasSafeSyncedCandidate())
			{
				List<Exception> list2 = errors;
				throw list2[list2.Count - 1];
			}
			return ranked;
		}
		bool HasSafeSyncedCandidate()
		{
			LyricsCandidate lyricsCandidate = LyricsMatcher.SelectSafeAutomaticCandidate(track, records.Values);
			if (lyricsCandidate != null)
			{
				return !string.IsNullOrWhiteSpace(lyricsCandidate.Record.SyncedLyrics);
			}
			return false;
		}
		void ReportProgress(string method)
		{
			if (progress != null)
			{
				IReadOnlyList<LyricsCandidate> candidates = LyricsMatcher.RankCandidates(track, records.Values);
				progress.Report(new LyricsSearchProgress
				{
					CompletedQueries = completedRequests,
					CandidateCount = records.Count,
					Stage = method,
					Candidates = candidates
				});
			}
		}
		async Task RunSearchAsync(string method, IReadOnlyDictionary<string, string>? values)
		{
			if (values != null && values.Count != 0)
			{
				string text = "api/search?" + BuildQuery(values);
				if (requestedUrls.Add(text))
				{
					ReportProgress(method);
					try
					{
						LrclibRecord[] array = (await GetJsonWithRetryAsync<LrclibRecord[]>(text, 2, token)) ?? Array.Empty<LrclibRecord>();
						successfulRequests++;
						LrclibRecord[] array2 = array;
						foreach (LrclibRecord lrclibRecord in array2)
						{
							records[lrclibRecord.Id] = lrclibRecord;
						}
						await _logger.WriteAsync($"search method={method} results={array.Length} spotifyTrackId={track.SpotifyTrackId ?? "unavailable"} cacheKey={track.CacheKey}");
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						throw;
					}
					catch (Exception ex2) when (ex2 is LyricsServiceException || ex2 is OperationCanceledException)
					{
						if (ex2 is OperationCanceledException)
						{
							new LyricsServiceException(LyricsErrorKind.Timeout, LocalizationService.TranslateCurrent("LRCLIB request timed out."), ex2);
						}
						errors.Add(ex2);
						LyricsServiceException ex3 = ex2 as LyricsServiceException;
						bool flag = ex3 != null;
						if (flag)
						{
							LyricsErrorKind kind = ex3.Kind;
							bool flag2 = (uint)(kind - 1) <= 2u;
							flag = flag2;
						}
						if (flag)
						{
							haltFurtherRequests = true;
						}
					}
					finally
					{
						completedRequests++;
						ReportProgress(method);
					}
				}
			}
		}
		bool ShouldStop()
		{
			if (!haltFurtherRequests)
			{
				if (stopWhenSafeSyncedFound)
				{
					return HasSafeSyncedCandidate();
				}
				return false;
			}
			return true;
		}
	}

	private async Task<LrclibRecord?> TryGetExactAsync(TrackInfo track, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(track.Artist) || track.Duration <= TimeSpan.Zero)
		{
			return null;
		}
		Dictionary<string, string> dictionary = Fields(track.Title, track.Artist, track.Album);
		dictionary["duration"] = Math.Round(track.Duration.TotalSeconds).ToString(CultureInfo.InvariantCulture);
		return await GetJsonWithRetryAsync<LrclibRecord>("api/get?" + BuildQuery(dictionary), 1, cancellationToken);
	}

	private async Task<T?> GetJsonWithRetryAsync<T>(string relativeUrl, int maxAttempts, CancellationToken cancellationToken, bool throwOnNotFound = false)
	{
		if (TryReadRequestCache<T>(relativeUrl, out T value))
		{
			return value;
		}
		Exception lastError = null;
		for (int attempt = 0; attempt < Math.Clamp(maxAttempts, 1, 3); attempt++)
		{
			await WaitForServerBackoffAsync(cancellationToken);
			using CancellationTokenSource requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			requestCancellation.CancelAfter((attempt == 0) ? TimeSpan.FromSeconds(15L) : TimeSpan.FromSeconds(30L));
			Stopwatch requestTimer = Stopwatch.StartNew();
			try
			{
				using HttpResponseMessage response = await _httpClient.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, requestCancellation.Token);
				await _logger.WriteAsync($"http path={RequestPath(relativeUrl)} status={(int)response.StatusCode} attempt={attempt + 1} elapsedMs={requestTimer.ElapsedMilliseconds}");
				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					if (throwOnNotFound)
					{
						throw new LyricsServiceException(LyricsErrorKind.NotFound, LocalizationService.TranslateCurrent("LRCLIB record was not found (404)."));
					}
					return default(T);
				}
				if (IsTransientStatus(response.StatusCode))
				{
					TimeSpan retryDelay = GetRetryDelay(response, attempt);
					SetServerBackoff(retryDelay);
					lastError = ((response.StatusCode == HttpStatusCode.TooManyRequests) ? new LyricsServiceException(LyricsErrorKind.RateLimited, LocalizationService.TranslateCurrent("Could not connect to LRCLIB.")) : new LyricsServiceException(LyricsErrorKind.Network, LocalizationService.TranslateCurrent("Could not connect to LRCLIB.")));
					if (attempt + 1 < maxAttempts)
					{
						await WaitForServerBackoffAsync(cancellationToken);
						continue;
					}
					break;
				}
				response.EnsureSuccessStatusCode();
				string json = await response.Content.ReadAsStringAsync(requestCancellation.Token);
				T? result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
				_requestCache[relativeUrl] = new CachedJsonResponse(json, DateTimeOffset.UtcNow.Add(RequestCacheLifetime));
				TrimRequestCache();
				return result;
			}
			catch (OperationCanceledException innerException) when (!cancellationToken.IsCancellationRequested)
			{
				lastError = new LyricsServiceException(LyricsErrorKind.Timeout, LocalizationService.TranslateCurrent("LRCLIB request timed out."), innerException);
				goto IL_04ec;
			}
			catch (JsonException innerException2)
			{
				throw new LyricsServiceException(LyricsErrorKind.Json, LocalizationService.TranslateCurrent("LRCLIB returned invalid JSON."), innerException2);
			}
			catch (HttpRequestException innerException3)
			{
				lastError = new LyricsServiceException(LyricsErrorKind.Network, LocalizationService.TranslateCurrent("Could not connect to LRCLIB."), innerException3);
				goto IL_04ec;
			}
			IL_04ec:
			if (attempt + 1 < maxAttempts)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(300 + attempt * 350), cancellationToken);
			}
			continue;
		}
		throw lastError ?? new LyricsServiceException(LyricsErrorKind.Network, LocalizationService.TranslateCurrent("Could not connect to LRCLIB."));
	}

	private async Task<LyricsLookupResult> CacheAndCreateResultAsync(TrackInfo track, LrclibRecord record, string selectionMode, CancellationToken cancellationToken)
	{
		LyricsCacheEntry entry = new LyricsCacheEntry
		{
			TrackKey = track.CacheKey,
			SyncedLyrics = record.SyncedLyrics,
			PlainLyrics = record.PlainLyrics,
			Source = $"LRCLIB #{record.Id}",
			IsInstrumental = record.Instrumental,
			SavedAtUtc = DateTimeOffset.UtcNow,
			CacheKind = "Positive",
			LrclibId = record.Id,
			LrclibTrackName = record.TrackName,
			LrclibArtistName = record.ArtistName,
			LrclibAlbumName = record.AlbumName,
			LrclibDuration = record.Duration,
			SelectionMode = selectionMode,
			MatcherVersion = 5
		};
		await _cacheStore.WriteAsync(track, entry, cancellationToken);
		_positiveMemoryCache[track.StableIdentityKey] = entry;
		return new LyricsLookupResult
		{
			Lyrics = ToLyricsResult(entry),
			Status = ((!(selectionMode == "Manual")) ? LyricsLookupStatus.LrclibAuto : LyricsLookupStatus.LrclibManual),
			LrclibRecord = record,
			SelectedManually = (selectionMode == "Manual")
		};
	}

	private async Task<LyricsLookupResult?> TryReadLocalLrcAsync(TrackInfo track, CancellationToken cancellationToken)
	{
		string expectedTitle = LyricsMatcher.NormalizeForComparison(track.Title);
		string expectedArtistTitle = LyricsMatcher.NormalizeForComparison(track.Artist + " " + track.Title);
		string expectedTitleArtist = LyricsMatcher.NormalizeForComparison(track.Title + " " + track.Artist);
		IEnumerable<string> enumerable;
		try
		{
			enumerable = Directory.EnumerateFiles(_localLrcDirectory, "*.lrc", SearchOption.TopDirectoryOnly).ToArray();
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			return null;
		}
		foreach (string path in enumerable)
		{
			string text = LyricsMatcher.NormalizeForComparison(Path.GetFileNameWithoutExtension(path));
			if (text != expectedTitle && text != expectedArtistTitle && text != expectedTitleArtist)
			{
				continue;
			}
			try
			{
				IReadOnlyList<LyricLine> readOnlyList = LrcParser.Parse(await File.ReadAllTextAsync(path, cancellationToken));
				if (readOnlyList.Count > 0)
				{
					return new LyricsLookupResult
					{
						Lyrics = new LyricsResult(readOnlyList, null, "LOCAL LRC · " + Path.GetFileName(path)),
						Status = LyricsLookupStatus.LocalLrc,
						LocalLrcPath = path
					};
				}
			}
			catch (Exception ex2) when (ex2 is IOException || ex2 is UnauthorizedAccessException)
			{
			}
		}
		return null;
	}

	private static bool IsValidPositiveCache(TrackInfo track, LyricsCacheEntry entry)
	{
		if (string.Equals(entry.SelectionMode, "Manual", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (entry.Source.StartsWith("LOCAL LRC", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (!entry.LrclibId.HasValue || string.IsNullOrWhiteSpace(entry.LrclibTrackName) || string.IsNullOrWhiteSpace(entry.LrclibArtistName))
		{
			return false;
		}
		LrclibRecord candidate = new LrclibRecord
		{
			Id = entry.LrclibId.Value,
			TrackName = entry.LrclibTrackName,
			ArtistName = entry.LrclibArtistName,
			AlbumName = entry.LrclibAlbumName,
			Duration = entry.LrclibDuration,
			Instrumental = entry.IsInstrumental,
			SyncedLyrics = entry.SyncedLyrics,
			PlainLyrics = entry.PlainLyrics
		};
		return LyricsMatcher.Evaluate(track, candidate).AutoEligible;
	}

	private static LyricsLookupResult FromCache(LyricsCacheEntry entry, bool selectedManually)
	{
		LrclibRecord lrclibRecord = (entry.LrclibId.HasValue ? new LrclibRecord
		{
			Id = entry.LrclibId.Value,
			TrackName = entry.LrclibTrackName,
			ArtistName = entry.LrclibArtistName,
			AlbumName = entry.LrclibAlbumName,
			Duration = entry.LrclibDuration,
			Instrumental = entry.IsInstrumental,
			SyncedLyrics = entry.SyncedLyrics,
			PlainLyrics = entry.PlainLyrics
		} : null);
		return new LyricsLookupResult
		{
			Lyrics = ToLyricsResult(entry),
			Status = (entry.Source.StartsWith("LOCAL LRC", StringComparison.OrdinalIgnoreCase) ? LyricsLookupStatus.LocalLrc : ((!selectedManually) ? LyricsLookupStatus.LrclibAuto : LyricsLookupStatus.LrclibManual)),
			LrclibRecord = lrclibRecord,
			LoadedFromCache = true,
			SelectedManually = selectedManually
		};
	}

	private static LyricsResult ToLyricsResult(LyricsCacheEntry entry)
	{
		return new LyricsResult(LrcParser.Parse(entry.SyncedLyrics), entry.PlainLyrics, entry.Source, entry.IsInstrumental);
	}

	private async Task LogTrackAsync(TrackInfo track, string method)
	{
		await _logger.WriteAsync($"track method={method} spotifyTrackId={track.SpotifyTrackId ?? "unavailable"} title={Safe(track.Title)} artist={Safe(track.Artist)} album={Safe(track.Album)} duration={track.Duration.TotalSeconds:0.###} identity={track.StableIdentityKey} cacheKey={track.CacheKey}");
	}

	private async Task LogCandidateAsync(TrackInfo track, string method, LyricsCandidate candidate)
	{
		string value = (candidate.AutoEligible ? "eligible" : string.Join(";", candidate.RejectionReasons));
		await _logger.WriteAsync($"candidate method={method} id={candidate.Record.Id} score={candidate.Score} eligible={candidate.AutoEligible} reason={Safe(value)} cacheKey={track.CacheKey}");
	}

	private async Task LogCandidatesAsync(TrackInfo track, string method, IEnumerable<LyricsCandidate> candidates)
	{
		await _logger.WriteBatchAsync(candidates.Select(delegate(LyricsCandidate candidate)
		{
			string value = (candidate.AutoEligible ? "eligible" : string.Join(";", candidate.RejectionReasons));
			return $"candidate method={method} id={candidate.Record.Id} score={candidate.Score} eligible={candidate.AutoEligible} reason={Safe(value)} cacheKey={track.CacheKey}";
		}));
	}

	private static Dictionary<string, string> Fields(string? title, string? artist, string? album)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		if (!string.IsNullOrWhiteSpace(title))
		{
			dictionary["track_name"] = title.Trim();
		}
		if (!string.IsNullOrWhiteSpace(artist))
		{
			dictionary["artist_name"] = artist.Trim();
		}
		if (!string.IsNullOrWhiteSpace(album))
		{
			dictionary["album_name"] = album.Trim();
		}
		return dictionary;
	}

	private static IReadOnlyDictionary<string, string>? Query(string? query)
	{
		if (!string.IsNullOrWhiteSpace(query))
		{
			return new Dictionary<string, string> { ["q"] = query.Trim() };
		}
		return null;
	}

	private static string BuildQuery(IReadOnlyDictionary<string, string> values)
	{
		return string.Join("&", values.Select<KeyValuePair<string, string>, string>((KeyValuePair<string, string> pair) => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
	}

	private static bool IsTransientStatus(HttpStatusCode statusCode)
	{
		if (statusCode != HttpStatusCode.RequestTimeout && statusCode != HttpStatusCode.TooManyRequests)
		{
			return statusCode >= HttpStatusCode.InternalServerError;
		}
		return true;
	}

	private bool TryReadRequestCache<T>(string relativeUrl, out T? value)
	{
		value = default(T);
		if (!_requestCache.TryGetValue(relativeUrl, out CachedJsonResponse value2))
		{
			return false;
		}
		CachedJsonResponse value3;
		if (value2.ExpiresAtUtc <= DateTimeOffset.UtcNow)
		{
			_requestCache.TryRemove(relativeUrl, out value3);
			return false;
		}
		try
		{
			value = JsonSerializer.Deserialize<T>(value2.Json, _jsonOptions);
			return true;
		}
		catch (JsonException)
		{
			_requestCache.TryRemove(relativeUrl, out value3);
			return false;
		}
	}

	private async Task WaitForServerBackoffAsync(CancellationToken cancellationToken)
	{
		TimeSpan timeSpan;
		lock (_serverBackoffLock)
		{
			timeSpan = _serverBackoffUntilUtc - DateTimeOffset.UtcNow;
		}
		if (timeSpan > TimeSpan.Zero)
		{
			await Task.Delay(timeSpan, cancellationToken);
		}
	}

	private void SetServerBackoff(TimeSpan delay)
	{
		DateTimeOffset dateTimeOffset = DateTimeOffset.UtcNow + delay;
		lock (_serverBackoffLock)
		{
			if (dateTimeOffset > _serverBackoffUntilUtc)
			{
				_serverBackoffUntilUtc = dateTimeOffset;
			}
		}
	}

	private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
	{
		return TimeSpan.FromMilliseconds(Math.Clamp((response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMilliseconds(600 + attempt * 800)).TotalMilliseconds, 300.0, 8000.0));
	}

	private void TrimRequestCache()
	{
		if (_requestCache.Count <= 128)
		{
			return;
		}
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		foreach (KeyValuePair<string, CachedJsonResponse> item in _requestCache)
		{
			if (item.Value.ExpiresAtUtc <= utcNow)
			{
				_requestCache.TryRemove(item.Key, out CachedJsonResponse _);
			}
		}
	}

	private static string RequestPath(string relativeUrl)
	{
		int num = relativeUrl.IndexOf('?');
		if (num >= 0)
		{
			return relativeUrl.Substring(0, num);
		}
		return relativeUrl;
	}

	private static string SanitizeFileName(string value)
	{
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			value = value.Replace(oldChar, '_');
		}
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return "lyrics";
	}

	private static string Safe(string? value)
	{
		return (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
	}

	private static IEnumerable<LrclibRecord> DiscoverAlternativeArtistQueries(TrackInfo track, IEnumerable<LrclibRecord> records)
	{
		string expectedTitle = LyricsMatcher.NormalizeForComparison(track.Title);
		string expectedArtist = LyricsMatcher.NormalizeForComparison(track.Artist);
		return from record in records
			where LyricsMatcher.ExtractTitleAliases(record.TrackName).Any((string alias) => LyricsMatcher.NormalizeForComparison(alias) == expectedTitle)
			where LyricsMatcher.NormalizeForComparison(record.ArtistName) != expectedArtist
			where track.Duration <= TimeSpan.Zero || record.Duration <= 0.0 || Math.Abs(track.Duration.TotalSeconds - record.Duration) <= 3.0
			where (record.ArtistName ?? string.Empty).Any((char character) => character <= '\u007f' && char.IsLetter(character))
			group record by LyricsMatcher.NormalizeForComparison(record.ArtistName) into @group
			select @group.First();
	}

	private void LocalLrcWatcher_Changed(object sender, FileSystemEventArgs e)
	{
		this.LocalLrcFilesChanged?.Invoke();
	}
}
