using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlowLyrics.Models;

namespace FlowLyrics.Controls;

public enum SyncRailField { Anchor, HoldStart, HoldEnd }
public sealed record SyncRailEdit(Guid Id, SyncRailField Field, double Seconds);

/// <summary>An honest duration axis, independent of the equally spaced lyric rows.</summary>
public sealed class PersonalSyncRail : FrameworkElement
{
	private PersonalSyncProfile _profile = new();
	private IReadOnlyList<double> _lyricTimes = Array.Empty<double>();
	private double _current;
	private double? _preview;
	private SyncRailEdit? _edit;
	private bool _interacting;
	public double DurationSeconds { get; private set; }
	public bool CanSeek { get; set; } = true;
	public Guid? SelectedId { get; set; }
	public bool IsInteracting => _interacting;
	public double? PreviewSeconds => _preview;
	public bool IsSeeking => _interacting && _edit == null;
	public Func<double, string>? DescribePosition { get; set; }
	public event Action<double>? SeekRequested;
	public event Action<Guid>? PointSelected;
	public event Action<SyncRailEdit>? EditStarted;
	public event Action<SyncRailEdit>? EditPreview;
	public event Action<bool>? EditFinished;
	public event Action? InteractionStarted;
	public PersonalSyncRail()
	{
		Width = 68; MinHeight = 150; Focusable = true; Cursor = Cursors.Hand;
		ToolTip = "";
	}
	public void SetTimeline(double duration, PersonalSyncProfile profile, IReadOnlyList<double> lyricTimes)
	{
		DurationSeconds = double.IsFinite(duration) ? Math.Max(0, duration) : 0;
		_profile = profile; _lyricTimes = lyricTimes; InvalidateVisual();
	}
	public void SetCurrent(double seconds) { _current = Math.Clamp(seconds, 0, Math.Max(0, DurationSeconds)); InvalidateVisual(); }
	public static double TimeAt(double y, double height, double duration) => duration > 0 && double.IsFinite(duration)
		? Math.Clamp((y - 18) / Math.Max(1, height - 36), 0, 1) * duration : 0;
	private double Y(double seconds) => 18 + Math.Clamp(DurationSeconds > 0 ? seconds / DurationSeconds : 0, 0, 1) * Math.Max(1, ActualHeight - 36);

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
		Pen rail = new(new SolidColorBrush(Color.FromRgb(94, 89, 99)), 2);
		dc.DrawLine(rail, new Point(24, 18), new Point(24, Math.Max(18, ActualHeight - 18)));
		if (DurationSeconds <= 0) return;
		foreach (double time in _lyricTimes) dc.DrawLine(new Pen(Brushes.Gray, 1), new Point(20, Y(time)), new Point(28, Y(time)));
		foreach (var hold in _profile.Segments)
		{
			double top = Y(hold.PlaybackStartSeconds), bottom = Y(hold.PlaybackEndSeconds);
			dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(130, 91, 145, 172)),
				SelectedId == hold.Id ? new Pen(Brushes.LightBlue, 1) : null, new Rect(9, top, 24, Math.Max(2, bottom - top)), 3, 3);
			dc.DrawRoundedRectangle(Brushes.LightBlue, null, new Rect(6, top - 3, 30, 6), 2, 2);
			dc.DrawRoundedRectangle(Brushes.LightBlue, null, new Rect(6, bottom - 3, 30, 6), 2, 2);
		}
		foreach (var anchor in _profile.Anchors)
			dc.DrawEllipse(Brushes.LightBlue, SelectedId == anchor.Id ? new Pen(Brushes.White, 2) : null, new Point(45, Y(anchor.PlaybackSeconds)), 5, 5);
		double currentY = Y(_edit == null ? _preview ?? _current : _current);
		StreamGeometry thumb = new();
		using (var shape = thumb.Open()) { shape.BeginFigure(new Point(0, currentY - 6), true, true); shape.LineTo(new Point(15, currentY), true, false); shape.LineTo(new Point(0, currentY + 6), true, false); }
		dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 125, 70)), null, thumb);
		Label(dc, "0:00", 0);
		Label(dc, TimeSpan.FromSeconds(DurationSeconds).ToString(@"m\:ss"), Math.Max(0, ActualHeight - 14));
	}
	private void Label(DrawingContext dc, string text, double y) => dc.DrawText(new FormattedText(text,
		CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 10, Brushes.Gray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(3, y));

	private SyncRailEdit? HitHandle(Point point)
	{
		if (DurationSeconds <= 0) return null;
		if (point.X > 36)
		{
			var anchor = _profile.Anchors.OrderBy(a => Math.Abs(Y(a.PlaybackSeconds) - point.Y)).FirstOrDefault();
			if (anchor != null && Math.Abs(Y(anchor.PlaybackSeconds) - point.Y) <= 9) return new(anchor.Id, SyncRailField.Anchor, anchor.PlaybackSeconds);
		}
		else if (point.X >= 16)
		{
			var handles = _profile.Segments.SelectMany(h => new[] { new SyncRailEdit(h.Id, SyncRailField.HoldStart, h.PlaybackStartSeconds), new SyncRailEdit(h.Id, SyncRailField.HoldEnd, h.PlaybackEndSeconds) });
			var handle = handles.OrderBy(h => Math.Abs(Y(h.Seconds) - point.Y)).FirstOrDefault();
			if (handle != null && Math.Abs(Y(handle.Seconds) - point.Y) <= 8) return handle;
		}
		return null;
	}
	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e); Focus();
		Point p = e.GetPosition(this);
		var handle = HitHandle(p);
		if (handle != null)
		{
			BeginEdit(handle); CaptureMouse();
		}
		else if (p.X is >= 6 and <= 36 && _profile.Segments.FirstOrDefault(h => p.Y >= Y(h.PlaybackStartSeconds) && p.Y <= Y(h.PlaybackEndSeconds)) is { } range)
		{ SelectedId = range.Id; PointSelected?.Invoke(range.Id); InvalidateVisual(); }
		else if (DurationSeconds > 0 && CanSeek) { BeginSeek(TimeAt(p.Y, ActualHeight, DurationSeconds)); CaptureMouse(); }
		e.Handled = true;
	}
	protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
	{
		Point p = e.GetPosition(this);
		var handle = HitHandle(p);
		if (handle != null) { SelectedId = handle.Id; PointSelected?.Invoke(handle.Id); }
		else if (p.X is >= 6 and <= 36 && _profile.Segments.FirstOrDefault(h => p.Y >= Y(h.PlaybackStartSeconds) && p.Y <= Y(h.PlaybackEndSeconds)) is { } range)
		{ SelectedId = range.Id; PointSelected?.Invoke(range.Id); }
		base.OnMouseRightButtonDown(e);
	}
	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		double time = TimeAt(e.GetPosition(this).Y, ActualHeight, DurationSeconds);
		ToolTip = DescribePosition?.Invoke(time) ?? TimeSpan.FromSeconds(time).ToString(@"m\:ss\.f");
		if (_interacting) MoveInteraction(time);
	}
	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); EndInteraction(); e.Handled = true; }
	protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); if (_interacting) EndInteraction(cancel: true); }
	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.Key == Key.Escape && _interacting) { EndInteraction(cancel: true); e.Handled = true; return; }
		if (CanSeek && DurationSeconds > 0 && e.Key is Key.Home or Key.End or Key.Up or Key.Down)
		{
			BeginSeek(e.Key == Key.Home ? 0 : e.Key == Key.End ? DurationSeconds : _current + (e.Key == Key.Up ? -1 : 1)); EndInteraction(); e.Handled = true;
		}
		base.OnKeyDown(e);
	}
	public void BeginSeek(double seconds)
	{
		if (!CanSeek || DurationSeconds <= 0 || !double.IsFinite(seconds)) return;
		_edit = null; _interacting = true; InteractionStarted?.Invoke(); MoveInteraction(seconds);
	}
	public void BeginEdit(SyncRailEdit edit)
	{
		if (DurationSeconds <= 0 || !double.IsFinite(edit.Seconds)) return;
		_edit = edit; SelectedId = edit.Id; PointSelected?.Invoke(edit.Id); _interacting = true;
		InteractionStarted?.Invoke(); EditStarted?.Invoke(edit);
	}
	public void MoveInteraction(double seconds)
	{
		if (!_interacting || !double.IsFinite(seconds)) return;
		_preview = Math.Clamp(seconds, 0, DurationSeconds);
		if (_edit != null) EditPreview?.Invoke(_edit with { Seconds = _preview.Value });
		InvalidateVisual();
	}
	public void EndInteraction(bool cancel = false)
	{
		if (!_interacting) return;
		_interacting = false;
		if (_edit != null) EditFinished?.Invoke(cancel);
		else if (!cancel && CanSeek && _preview.HasValue) SeekRequested?.Invoke(_preview.Value);
		_edit = null; _preview = null; ReleaseMouseCapture(); InvalidateVisual();
	}
}
