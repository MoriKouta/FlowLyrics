using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowLyrics.Controls;

/// <summary>Blurred glyphs outside the scrolling foreground subtree; never participates in text layout.</summary>
public sealed class LyricGlowOverlay : FrameworkElement
{
	public const double WindowBleed = 48;
	private readonly ScrollViewer _viewport;
	private readonly Func<IReadOnlyList<OutlinedText>> _sources;
	private readonly VisualCollection _visuals;
	private readonly Dictionary<OutlinedText, Layer> _layers = new();
	private sealed record State(Geometry Geometry, Matrix Matrix, Rect Viewport, Color Color, double Radius, double Opacity);
	private sealed class Layer { public DrawingVisual Visual = new(); public State? State; }
	public LyricGlowOverlay(ScrollViewer viewport, Func<IReadOnlyList<OutlinedText>> sources)
	{
		_viewport = viewport; _sources = sources; _visuals = new(this);
		IsHitTestVisible = false; Focusable = false; ClipToBounds = false;
		Loaded += (_, _) => CompositionTarget.Rendering += Rendering;
		Unloaded += (_, _) => { CompositionTarget.Rendering -= Rendering; _visuals.Clear(); _layers.Clear(); };
	}
	protected override int VisualChildrenCount => _visuals.Count;
	protected override Visual GetVisualChild(int index) => _visuals[index];
	protected override Size MeasureOverride(Size availableSize) => new(0, 0);
	protected override Geometry GetLayoutClip(Size layoutSlotSize) => null;
	private void Rendering(object? sender, EventArgs e) => Refresh();
	public void Refresh()
	{
		HashSet<OutlinedText> active = new();
		if (IsVisible && _viewport.IsVisible && _viewport.ActualWidth > 0 && _viewport.ActualHeight > 0)
		{
			Rect viewport = _viewport.TransformToVisual(this).TransformBounds(new Rect(_viewport.RenderSize));
			foreach (OutlinedText text in _sources())
			{
				if (!text.IsVisible || !text.HasGlow || text.GlyphGeometry == null || !text.IsDescendantOf(_viewport)) continue;
				GeneralTransform transform = text.TransformToVisual(this);
				Point origin = transform.Transform(new()), x = transform.Transform(new(1, 0)), y = transform.Transform(new(0, 1));
				Matrix matrix = new(x.X - origin.X, x.Y - origin.Y, y.X - origin.X, y.Y - origin.Y, origin.X, origin.Y);
				if (!transform.TransformBounds(text.GlyphGeometry.Bounds).IntersectsWith(viewport)) continue;
				double opacity = Math.Clamp(text.GlowOpacity, 0, 1) * .8;
				for (DependencyObject? node = text; node != null && node != VisualTreeHelper.GetParent(this); node = VisualTreeHelper.GetParent(node))
					if (node is UIElement element) opacity *= element.Opacity;
				State state = new(text.GlyphGeometry, matrix, viewport, text.GlowColor, Math.Clamp(text.GlowRadius, 0, 40), opacity);
				active.Add(text);
				if (!_layers.TryGetValue(text, out Layer? layer)) { layer = new(); _layers[text] = layer; _visuals.Add(layer.Visual); }
				if (layer.State == state) continue;
				layer.State = state;
				// Clip the source silhouette before blur. The blurred output has no viewport clip.
				using (DrawingContext dc = layer.Visual.RenderOpen())
				{
					dc.PushClip(new RectangleGeometry(viewport));
					dc.PushTransform(new MatrixTransform(matrix));
					dc.DrawGeometry(new SolidColorBrush(state.Color), null, state.Geometry);
				}
				if (layer.Visual.Effect is not BlurEffect blur || blur.Radius != state.Radius)
				{
					BlurEffect effect = new() { Radius = state.Radius, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
					effect.Freeze(); layer.Visual.Effect = effect;
				}
				layer.Visual.Opacity = state.Opacity;
			}
		}
		foreach (var stale in _layers.Keys.Where(text => !active.Contains(text)).ToArray())
		{ _visuals.Remove(_layers[stale].Visual); _layers.Remove(stale); }
	}
}
