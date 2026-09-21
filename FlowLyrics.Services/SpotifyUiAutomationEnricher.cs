using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation;
using FlowLyrics.Core;

namespace FlowLyrics.Services;

public enum SpotifyWindowState { Unknown, Visible, Minimized, Offscreen, Closed }

/// <summary>Optional, read-only enrichment. UIA never runs on or blocks the media polling thread.</summary>
public sealed class SpotifyUiAutomationEnricher : IArtistCreditEnricher
{
	private readonly object _gate = new();
	private readonly Action<string>? _audit;
	private readonly Func<string, MediaTrackMetadata, ArtistCreditEnrichment?> _read;
	private readonly Func<string, SpotifyWindowState> _readWindowState;
	private readonly Func<DateTimeOffset> _utcNow;
	private readonly Dictionary<string, ArtistCreditEnrichment> _sessionCredits = new(StringComparer.Ordinal);
	private Task<(string Key, ArtistCreditEnrichment? Value)>? _pending;
	private string _currentKey = string.Empty;
	private ArtistCreditEnrichment? _cached;
	private DateTimeOffset _nextAttempt;
	private DateTimeOffset _validUntil;
	private DateTimeOffset _nextWindowCheck;
	private SpotifyWindowState _windowState;
	private volatile bool _disposed;

	public string WindowState { get { lock (_gate) return _windowState.ToString().ToUpperInvariant(); } }

	public SpotifyUiAutomationEnricher()
	{
		try
		{
			AppLogger logger = new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLyrics"));
			_audit = message => { _ = logger.WriteAsync(message); };
		}
		catch { /* Diagnostics are optional, just like enrichment. */ }
		_read = Read;
		_readWindowState = ReadWindowState;
		_utcNow = () => DateTimeOffset.UtcNow;
	}
	public SpotifyUiAutomationEnricher(Func<string, MediaTrackMetadata, ArtistCreditEnrichment?> read,
		Func<string, SpotifyWindowState>? readWindowState = null, Func<DateTimeOffset>? utcNow = null, Action<string>? audit = null)
	{
		_audit = audit;
		_read = read ?? throw new ArgumentNullException(nameof(read));
		_readWindowState = readWindowState ?? (_ => SpotifyWindowState.Visible);
		_utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
	}

	public ArtistCreditEnrichment? TryGet(string sourceId, MediaTrackMetadata metadata)
	{
		if (!MediaSourceClassifier.IsSpotify(sourceId)) return null;
		string key = CacheKey(sourceId, metadata);
		lock (_gate)
		{
			if (_disposed) return null;
			DateTimeOffset now = _utcNow();
			if (now >= _nextWindowCheck)
			{
				SpotifyWindowState previous = _windowState;
				try { _windowState = _readWindowState(sourceId); }
				catch { _windowState = SpotifyWindowState.Unknown; }
				_nextWindowCheck = now.AddSeconds(1);
				if (previous != _windowState)
				{
					_nextAttempt = DateTimeOffset.MinValue;
					Audit($"UIA window at={now:O} state={_windowState}");
				}
			}
			if (!SpotifyArtistCredit.ShouldProbe(sourceId, metadata)) return null;
			if (_currentKey != key) { _currentKey = key; _cached = null; _nextAttempt = DateTimeOffset.MinValue; }
			if (_pending?.IsCompleted == true)
			{
				var completed = _pending.GetAwaiter().GetResult();
				_pending = null;
				if (completed.Key == key)
				{
					_cached = completed.Value;
					if (_cached != null)
					{
						// Bounded session memory only; never share with persistent lyrics/sync keys.
						if (_sessionCredits.Count >= 128 && !_sessionCredits.ContainsKey(key))
							_sessionCredits.Remove(_sessionCredits.Keys.First());
						_sessionCredits[key] = _cached;
					}
					_validUntil = now.AddSeconds(30);
					_nextAttempt = now.AddSeconds(_cached == null ? 2 : 30);
				}
			}
			if (_windowState is SpotifyWindowState.Minimized or SpotifyWindowState.Offscreen)
				return _sessionCredits.TryGetValue(key, out ArtistCreditEnrichment? remembered)
					? remembered with { Source = SpotifyArtistCredit.Source + " (cached)" } : null;
			if (_windowState != SpotifyWindowState.Visible) return null;
			if (_pending == null && now >= _nextAttempt)
			{
				// Keep at most one native call in flight, including a hung UIA provider.
				_pending = Task.Run(() =>
				{
					Stopwatch elapsed = Stopwatch.StartNew();
					var before = SampleProcessUsage();
					Audit($"UIA scan-start at={DateTimeOffset.UtcNow:O}");
					try
					{
						ArtistCreditEnrichment? result = _read(sourceId, metadata);
						if (result != null && (string.IsNullOrWhiteSpace(result.Credit)
							|| MetadataNormalizer.ComparisonKey(result.Title) != MetadataNormalizer.ComparisonKey(metadata.TitleRaw))) result = null;
						Audit($"UIA result found={result != null}");
						return (key, result);
					}
					catch (Exception ex)
					{
						Debug.WriteLine("Spotify enrichment unavailable: " + ex.GetType().Name);
						return (key, (ArtistCreditEnrichment?)null);
					}
					finally
					{
						var after = SampleProcessUsage();
						Audit($"UIA scan-end at={DateTimeOffset.UtcNow:O} elapsedMs={elapsed.ElapsedMilliseconds} appCpuMs={Math.Max(0, after.CpuMs - before.CpuMs):0} appWorkingSetMiB={after.MemoryMiB}");
					}
				});
			}
			return now <= _validUntil ? _cached : null;
		}
	}

	private static (double CpuMs, long MemoryMiB) SampleProcessUsage()
	{
		try { using Process process = Process.GetCurrentProcess(); return (process.TotalProcessorTime.TotalMilliseconds, process.WorkingSet64 / 1048576); }
		catch { return (0, 0); }
	}

	private void Audit(string message) { try { _audit?.Invoke(message); } catch { } }

	private static string CacheKey(string sourceId, MediaTrackMetadata metadata)
	{
		static string Part(string? value) => MetadataNormalizer.NormalizeWhitespace(value).ToLowerInvariant();
		// Preserve punctuation/edition words; title aliases alone cannot prove this is
		// the previously observed release. Length prefixes avoid delimiter collisions.
		return string.Concat(new[] { Part(sourceId), Part(metadata.OriginalTitleRaw),
			Part(metadata.OriginalAlbumRaw), Part(metadata.OriginalArtistRaw),
			Math.Round(metadata.Duration.TotalSeconds).ToString(CultureInfo.InvariantCulture) }
			.Select(value => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value));
	}

	private static SpotifyWindowState ReadWindowState(string sourceId)
	{
		SpotifyWindowState state = SpotifyWindowState.Closed;
		foreach (Process process in Process.GetProcessesByName("Spotify"))
		{
			using (process)
			{
				if (!SystemVolumeService.IsSourceSessionIdentity(sourceId, process.ProcessName,
					SystemVolumeService.TryGetProcessApplicationUserModelId((uint)process.Id), null, null, null, null)) continue;
				nint handle = process.MainWindowHandle;
				if (handle == 0) continue;
				if (IsIconic(handle)) { state = SpotifyWindowState.Minimized; continue; }
				if (IsWindowVisible(handle)) return SpotifyWindowState.Visible;
				if (state != SpotifyWindowState.Minimized) state = SpotifyWindowState.Offscreen;
			}
		}
		return state;
	}

	[DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsIconic(nint handle);
	[DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWindowVisible(nint handle);

	private ArtistCreditEnrichment? Read(string sourceId, MediaTrackMetadata metadata)
	{
		Stopwatch timer = Stopwatch.StartNew();
		List<ArtistCreditEnrichment> results = new();
		foreach (Process process in Process.GetProcessesByName("Spotify"))
		{
			using (process)
			{
				if (_disposed || timer.Elapsed > TimeSpan.FromSeconds(2)) return null;
				if (!SystemVolumeService.IsSourceSessionIdentity(sourceId, process.ProcessName,
					SystemVolumeService.TryGetProcessApplicationUserModelId((uint)process.Id), null, null, null, null)) continue;
				AutomationElementCollection roots = AutomationElement.RootElement.FindAll(TreeScope.Children,
					new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
				foreach (AutomationElement root in roots)
				{
					if (_disposed || timer.Elapsed > TimeSpan.FromSeconds(2)) return null;
					if (root.Current.IsOffscreen || root.Current.ProcessId != process.Id) continue;
					AutomationElementCollection bars = root.FindAll(TreeScope.Descendants, new AndCondition(
						new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group),
						new OrCondition(new PropertyCondition(AutomationElement.NameProperty, "再生中バー"),
							new PropertyCondition(AutomationElement.NameProperty, "Now playing bar", PropertyConditionFlags.IgnoreCase))));
					if (bars.Count != 1 || bars[0].Current.IsOffscreen) continue;
					foreach (AutomationElement region in bars[0].FindAll(TreeScope.Children,
						new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group)))
					{
						if (_disposed || timer.Elapsed > TimeSpan.FromSeconds(2)) return null;
						string label = region.Current.Name;
						if (region.Current.IsOffscreen || region.Current.ProcessId != process.Id) continue;
						if (!label.StartsWith("再生中：", StringComparison.Ordinal)
							&& !label.StartsWith("Now playing:", StringComparison.OrdinalIgnoreCase)) continue;
						AutomationElement[] links = region.FindAll(TreeScope.Descendants,
							new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink))
							.Cast<AutomationElement>().ToArray();
						if (links.Length is < 2 or > 32 || links.Any(link => link.Current.ProcessId != process.Id)) continue;
						AutomationElement[] titles = links.Where(link => MetadataNormalizer.ComparisonKey(link.Current.Name)
							== MetadataNormalizer.ComparisonKey(metadata.TitleRaw)).ToArray();
						if (titles.Length != 1)
						{
							Audit("UIA rejected reason=current-title-mismatch-or-ambiguous");
							continue;
						}
						AutomationElement title = titles[0];
						string uiTitle = title.Current.Name;
						AutomationElement[] artists = links.Where(link => !ReferenceEquals(link, title)).ToArray();
						// Only sibling artist links from this one credit block, never sidebar/history links.
						int[] parentId = TreeWalker.RawViewWalker.GetParent(artists[0]).GetRuntimeId();
						if (artists.Any(link => !TreeWalker.RawViewWalker.GetParent(link).GetRuntimeId().SequenceEqual(parentId))) continue;
						string[] credits = artists.Select(link => link.Current.Name).ToArray();
						ArtistCreditEnrichment? result = SpotifyArtistCredit.Validate(process.ProcessName, true,
							metadata.TitleRaw, metadata.OriginalArtistRaw, uiTitle, label, credits);
						if (result != null && region.Current.Name == label && title.Current.Name == uiTitle) results.Add(result);
					}
				}
			}
		}
		if (_disposed || timer.Elapsed > TimeSpan.FromSeconds(2)) return null;
		ArtistCreditEnrichment[] unique = results.Distinct().ToArray();
		return unique.Length == 1 ? unique[0] : null;
	}

	public void Dispose() { lock (_gate) { _disposed = true; _cached = null; _sessionCredits.Clear(); } }
}
