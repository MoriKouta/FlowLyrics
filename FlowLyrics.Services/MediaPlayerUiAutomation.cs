using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;

namespace FlowLyrics.Services;

/// <summary>
/// Conservative fallback for players that publish a GSMTC timeline but reject
/// absolute seek commands. It changes only a high-confidence horizontal range
/// control belonging to the selected player's process; ambiguous controls such
/// as volume sliders are deliberately ignored.
/// </summary>
internal static class MediaPlayerUiAutomation
{
	internal static bool TrySeek(string? sourceAppUserModelId, TimeSpan currentPosition, TimeSpan duration, TimeSpan destination)
	{
		if (string.IsNullOrWhiteSpace(sourceAppUserModelId) || duration <= TimeSpan.FromSeconds(10)) return false;
		double targetRatio = Math.Clamp(destination.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
		Candidate? best = null;

		foreach (Process process in Process.GetProcesses())
		{
			using (process)
			{
				try
				{
					string processName = process.ProcessName;
					string? processAppId = SystemVolumeService.TryGetProcessApplicationUserModelId(checked((uint)process.Id));
					if (!SystemVolumeService.IsSourceSessionIdentity(sourceAppUserModelId, processName, processAppId, null, null, null, null))
					{
						continue;
					}
					foreach (AutomationElement root in GetProcessRoots(process))
					{
						AutomationElementCollection ranges = root.FindAll(
							TreeScope.Descendants,
							new AndCondition(
								new PropertyCondition(AutomationElement.IsEnabledProperty, true),
								new PropertyCondition(AutomationElement.IsRangeValuePatternAvailableProperty, true)));
						for (int index = 0; index < ranges.Count; index++)
						{
							AutomationElement element = ranges[index];
							if (element.GetCurrentPattern(RangeValuePattern.Pattern) is not RangeValuePattern pattern || pattern.Current.IsReadOnly)
							{
								continue;
							}
							RangeValuePattern.RangeValuePatternInformation range = pattern.Current;
							Rect bounds = element.Current.BoundingRectangle;
							double score = ScoreCandidate(
								element.Current.Name,
								element.Current.AutomationId,
								element.Current.ClassName,
								range.Minimum,
								range.Maximum,
								range.Value,
								bounds.Width,
								bounds.Height,
								currentPosition.TotalSeconds,
								duration.TotalSeconds);
							if (double.IsNegativeInfinity(score) || best != null && best.Score >= score) continue;
							best = new Candidate(pattern, range.Minimum, range.Maximum, score);
						}
					}
				}
				catch (Exception ex) when (ex is InvalidOperationException
					or ElementNotAvailableException
					or UnauthorizedAccessException
					or OverflowException
					or System.ComponentModel.Win32Exception
					or System.Runtime.InteropServices.COMException
					or NotSupportedException)
				{
				}
			}
		}

		if (best == null) return false;
		try
		{
			double value = best.Minimum + (best.Maximum - best.Minimum) * targetRatio;
			best.Pattern.SetValue(Math.Clamp(value, best.Minimum, best.Maximum));
			return true;
		}
		catch (Exception ex) when (ex is InvalidOperationException
			or ArgumentException
			or ElementNotAvailableException
			or ElementNotEnabledException
			or System.Runtime.InteropServices.COMException)
		{
			return false;
		}
	}

	internal static double ScoreCandidate(
		string? name,
		string? automationId,
		string? className,
		double minimum,
		double maximum,
		double value,
		double width,
		double height,
		double currentSeconds,
		double durationSeconds)
	{
		double span = maximum - minimum;
		if (!double.IsFinite(span) || !double.IsFinite(value) || span <= 0.0 || durationSeconds <= 10.0) return double.NegativeInfinity;
		string identity = ((name ?? string.Empty) + " " + (automationId ?? string.Empty) + " " + (className ?? string.Empty)).ToLowerInvariant();
		bool namedAsTimeline = ContainsAny(identity, "seek", "scrub", "position", "progress", "playback", "timeline", "elapsed", "time slider");
		double unitsPerSecond = span / durationSeconds;
		bool durationScaled = NearScale(unitsPerSecond, 1.0) || NearScale(unitsPerSecond, 1000.0) || NearScale(unitsPerSecond, TimeSpan.TicksPerSecond);
		bool hasUsableBounds = width > 0.0 && height > 0.0;
		if (hasUsableBounds && (width < 120.0 || width < height * 2.0) && !(namedAsTimeline && durationScaled)) return double.NegativeInfinity;
		if (!namedAsTimeline && !durationScaled) return double.NegativeInfinity;

		double expectedRatio = Math.Clamp(currentSeconds / durationSeconds, 0.0, 1.0);
		double actualRatio = Math.Clamp((value - minimum) / span, 0.0, 1.0);
		double ratioDifference = Math.Abs(expectedRatio - actualRatio);
		if (ratioDifference > (namedAsTimeline ? 0.35 : 0.18)) return double.NegativeInfinity;

		double score = (durationScaled ? 8.0 : 0.0) + (namedAsTimeline ? 6.0 : 0.0) + Math.Max(0.0, 4.0 - ratioDifference * 20.0);
		if (hasUsableBounds) score += Math.Min(3.0, width / 180.0);
		return score;
	}

	private static IEnumerable<AutomationElement> GetProcessRoots(Process process)
	{
		HashSet<string> seen = new(StringComparer.Ordinal);
		if (process.MainWindowHandle != IntPtr.Zero)
		{
			AutomationElement root = AutomationElement.FromHandle(process.MainWindowHandle);
			if (root != null && seen.Add(RuntimeId(root))) yield return root;
		}
		AutomationElementCollection windows = AutomationElement.RootElement.FindAll(
			TreeScope.Children,
			new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
		for (int index = 0; index < windows.Count; index++)
		{
			AutomationElement root = windows[index];
			if (seen.Add(RuntimeId(root))) yield return root;
		}
	}

	private static string RuntimeId(AutomationElement element)
	{
		try
		{
			return string.Join(".", element.GetRuntimeId());
		}
		catch (InvalidOperationException)
		{
			return element.Current.NativeWindowHandle.ToString();
		}
	}

	private static bool NearScale(double actual, double expected)
	{
		return Math.Abs(actual - expected) <= expected * 0.12;
	}

	private static bool ContainsAny(string value, params string[] terms) => terms.Any(term => value.Contains(term, StringComparison.Ordinal));

	private sealed record Candidate(RangeValuePattern Pattern, double Minimum, double Maximum, double Score);
}
