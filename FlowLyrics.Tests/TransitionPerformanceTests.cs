using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class TransitionPerformanceTests
{
    [Fact]
    public async Task CachedTrackAlternation_RendersTheCorrectLyrics()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-transition-" + Guid.NewGuid().ToString("N"));
        TrackInfo[] tracks = [new("Cached A", "Artist", "Album", TimeSpan.FromSeconds(180)), new("Cached B", "Artist", "Album", TimeSpan.FromSeconds(200))];
        try
        {
            using var seedService = new LyricsService(directory);
            LyricsCacheStore cache = Read<LyricsCacheStore>(seedService, "_cacheStore");
            for (int i = 0; i < tracks.Length; i++) await cache.WriteAsync(tracks[i], new LyricsCacheEntry
            {
                TrackKey = tracks[i].StableIdentityKey, MatcherVersion = 8, CacheKind = "Positive", LrclibId = 100 + i,
                LrclibTrackName = tracks[i].Title, LrclibArtistName = "Artist", LrclibAlbumName = "Album", LrclibDuration = tracks[i].Duration.TotalSeconds,
                SyncedLyrics = "[00:00.00]" + tracks[i].Title, Source = "LRCLIB", SelectionMode = "Auto", SavedAtUtc = DateTimeOffset.UtcNow
            }, default);
            List<double> samples = new();
            Sta(() =>
            {
                var provider = new TransitionProvider();
                using MediaSessionService media = new(provider);
                MainWindow window = new(new SettingsService(directory), media);
                try
                {
                    window.ShowActivated = false; window.Show(); Pump();
                    Read<DispatcherTimer>(window, "_mediaTimer").Stop(); // Metadata events alone must drive lookup and confirmation.
                    for (int i = 0; i < 8; i++)
                    {
                        TrackInfo track = tracks[i % 2];
                        Stopwatch clock = Stopwatch.StartNew();
                        provider.Set(track);
                        Pump();
                        Assert.True(Read<bool>(window, "_metadataPending"));
                        Assert.Null(Read<LyricsResult?>(window, "_lyrics"));
                        Assert.DoesNotContain("WAITING", Read<System.Windows.Controls.TextBlock>(window, "TrackStatusText").Text);
                        while (clock.Elapsed < TimeSpan.FromSeconds(5))
                        {
                            Pump();
                            Assert.DoesNotContain("SEARCHING", Read<System.Windows.Controls.TextBlock>(window, "TrackStatusText").Text);
                            if (Read<LyricsResult?>(window, "_lyrics")?.Lines.FirstOrDefault()?.Text == track.Title) break;
                            Thread.Sleep(5);
                        }
                        Assert.Equal(track.Title, Read<LyricsResult?>(window, "_lyrics")?.Lines.FirstOrDefault()?.Text);
                        // Include actual WPF layout/render dispatch, not only lookup completion.
                        Pump(); samples.Add(clock.Elapsed.TotalMilliseconds);
                    }
                }
                finally
                {
                    foreach (FieldInfo field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                        if (field.GetValue(window) is DispatcherTimer timer) timer.Stop();
                    Read<IDisposable?>(window, "_hotkeys")?.Dispose(); Read<IDisposable?>(window, "_tray")?.Dispose();
                    Read<LyricsService>(window, "_lyricsService").Dispose();
                    typeof(MainWindow).GetField("_allowClose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
                    window.Close();
                }
            });
            string? report = Environment.GetEnvironmentVariable("FLOWLYRICS_PERF_REPORT");
            if (!string.IsNullOrEmpty(report)) await File.WriteAllTextAsync(report,
                "Synthetic GSMTC + real WPF dispatch; not a live-player measurement.\n" + string.Join(", ", samples.Select(v => v.ToString("F1")))
                + $"\nDisk first two: {string.Join(", ", samples.Take(2).Select(v => v.ToString("F1")))} ms\nHot median: {samples.Skip(2).Order().ElementAt(3):F1} ms; worst: {samples.Skip(2).Max():F1} ms\n");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    internal sealed class TransitionProvider : IMediaSessionProvider
    {
        public IReadOnlyList<MediaSessionInfo> Sessions = [];
        public event EventHandler? SessionsChanged;
        public void Set(TrackInfo track)
        {
            Sessions = [new() { SessionId = "test-session", SourceAppUserModelId = "Spotify", DisplaySourceName = "Spotify",
                Metadata = new(track.Title, track.Artist, track.Album, track.Duration), PlaybackState = MediaPlaybackState.Playing,
                CapturedAtUtc = DateTimeOffset.UtcNow, HasTimeline = true, Capabilities = new(true, true, true, true, true, true) }];
            SessionsChanged?.Invoke(this, new MediaMetadataChangedEventArgs("test-session"));
        }
        public Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sessions);
        public Task<bool> TryTogglePlayPauseAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> TrySkipNextAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> TrySkipPreviousAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> TrySeekAsync(string sessionId, TimeSpan position, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public void Dispose() { }
    }
}
