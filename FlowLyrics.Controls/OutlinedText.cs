using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace FlowLyrics.Controls;

public sealed class OutlinedText : FrameworkElement
{
	public static readonly DependencyProperty TextProperty = Register("Text", typeof(string), string.Empty);

	public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(OutlinedText), new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(OutlinedText), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(OutlinedText), new FrameworkPropertyMetadata(FontWeights.SemiBold, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty FillProperty = Register("Fill", typeof(Brush), Brushes.White, affectsMeasure: false);

	public static readonly DependencyProperty StrokeProperty = Register("Stroke", typeof(Brush), Brushes.Black, affectsMeasure: false);

	public static readonly DependencyProperty StrokeThicknessProperty = Register("StrokeThickness", typeof(double), 2.0);

	public static readonly DependencyProperty ShadowBrushProperty = Register("ShadowBrush", typeof(Brush), new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)), affectsMeasure: false);

	public static readonly DependencyProperty ShadowDepthProperty = Register("ShadowDepth", typeof(double), 3.0);

	public static readonly DependencyProperty TextAlignmentProperty = Register("TextAlignment", typeof(TextAlignment), TextAlignment.Left, affectsMeasure: false);

	public static readonly DependencyProperty MinimumFontSizeProperty = Register("MinimumFontSize", typeof(double), 8.0);

	public static readonly DependencyProperty AutoFitProperty = Register("AutoFit", typeof(bool), true);

	public static readonly DependencyProperty WrapProperty = Register("Wrap", typeof(bool), true);

	public static readonly DependencyProperty MaximumLinesProperty = Register("MaximumLines", typeof(int), 4);

	public string Text
	{
		get
		{
			return (string)GetValue(TextProperty);
		}
		set
		{
			SetValue(TextProperty, value);
		}
	}

	public FontFamily FontFamily
	{
		get
		{
			return (FontFamily)GetValue(FontFamilyProperty);
		}
		set
		{
			SetValue(FontFamilyProperty, value);
		}
	}

	public double FontSize
	{
		get
		{
			return (double)GetValue(FontSizeProperty);
		}
		set
		{
			SetValue(FontSizeProperty, value);
		}
	}

	public FontWeight FontWeight
	{
		get
		{
			return (FontWeight)GetValue(FontWeightProperty);
		}
		set
		{
			SetValue(FontWeightProperty, value);
		}
	}

	public Brush Fill
	{
		get
		{
			return (Brush)GetValue(FillProperty);
		}
		set
		{
			SetValue(FillProperty, value);
		}
	}

	public Brush Stroke
	{
		get
		{
			return (Brush)GetValue(StrokeProperty);
		}
		set
		{
			SetValue(StrokeProperty, value);
		}
	}

	public double StrokeThickness
	{
		get
		{
			return (double)GetValue(StrokeThicknessProperty);
		}
		set
		{
			SetValue(StrokeThicknessProperty, value);
		}
	}

	public Brush ShadowBrush
	{
		get
		{
			return (Brush)GetValue(ShadowBrushProperty);
		}
		set
		{
			SetValue(ShadowBrushProperty, value);
		}
	}

	public double ShadowDepth
	{
		get
		{
			return (double)GetValue(ShadowDepthProperty);
		}
		set
		{
			SetValue(ShadowDepthProperty, value);
		}
	}

	public TextAlignment TextAlignment
	{
		get
		{
			return (TextAlignment)GetValue(TextAlignmentProperty);
		}
		set
		{
			SetValue(TextAlignmentProperty, value);
		}
	}

	public double MinimumFontSize
	{
		get
		{
			return (double)GetValue(MinimumFontSizeProperty);
		}
		set
		{
			SetValue(MinimumFontSizeProperty, value);
		}
	}

	public bool AutoFit
	{
		get
		{
			return (bool)GetValue(AutoFitProperty);
		}
		set
		{
			SetValue(AutoFitProperty, value);
		}
	}

	public bool Wrap
	{
		get
		{
			return (bool)GetValue(WrapProperty);
		}
		set
		{
			SetValue(WrapProperty, value);
		}
	}

	public int MaximumLines
	{
		get
		{
			return (int)GetValue(MaximumLinesProperty);
		}
		set
		{
			SetValue(MaximumLinesProperty, value);
		}
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (string.IsNullOrEmpty(Text))
		{
			return new Size(1.0, 1.0);
		}
		double drawingPadding = GetDrawingPadding();
		double num = (double.IsInfinity(availableSize.Width) ? 4096.0 : Math.Max(1.0, availableSize.Width - drawingPadding * 2.0));
		FormattedText formattedText = CreateFormattedText(num, FontSize);
		double num2 = Math.Min(num, Math.Max(FontSize, formattedText.WidthIncludingTrailingWhitespace));
		return new Size(height: Math.Ceiling(formattedText.Height * 1.12 + drawingPadding * 2.0), width: Math.Ceiling(num2 + drawingPadding * 2.0));
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		InvalidateVisual();
		return finalSize;
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		if (!string.IsNullOrEmpty(Text) && !(base.ActualWidth <= 2.0) && !(base.ActualHeight <= 2.0))
		{
			double drawingPadding = GetDrawingPadding();
			double num = Math.Max(1.0, base.ActualWidth - drawingPadding * 2.0);
			double num2 = Math.Max(1.0, base.ActualHeight - drawingPadding * 2.0);
			double fontSize = SelectFontSize(num, num2);
			FormattedText formattedText = CreateFormattedText(num, fontSize);
			double x = drawingPadding;
			double y = drawingPadding + Math.Max(0.0, (num2 - formattedText.Height) / 2.0);
			Geometry geometry = formattedText.BuildGeometry(new Point(x, y));
			if (ShadowBrush != null && ShadowDepth > 0.0)
			{
				drawingContext.PushTransform(new TranslateTransform(ShadowDepth, ShadowDepth));
				drawingContext.DrawGeometry(ShadowBrush, null, geometry);
				drawingContext.Pop();
			}
			Pen pen = null;
			if (Stroke != null && StrokeThickness > 0.0)
			{
				pen = new Pen(Stroke, StrokeThickness)
				{
					LineJoin = PenLineJoin.Round,
					StartLineCap = PenLineCap.Round,
					EndLineCap = PenLineCap.Round
				};
				pen.Freeze();
			}
			drawingContext.DrawGeometry(Fill ?? Brushes.White, pen, geometry);
		}
	}

	private double SelectFontSize(double width, double height)
	{
		double num = Math.Max(0.75, FontSize);
		if (Fits(num, width, height))
		{
			return num;
		}
		double num2 = Math.Clamp(MinimumFontSize, 0.75, num);
		double num3 = (Fits(num2, width, height) ? num2 : 0.75);
		if (!Fits(num3, width, height))
		{
			return 0.75;
		}
		double num4 = num3;
		double num5 = num;
		for (int i = 0; i < 12; i++)
		{
			double num6 = (num4 + num5) / 2.0;
			if (Fits(num6, width, height))
			{
				num4 = num6;
			}
			else
			{
				num5 = num6;
			}
		}
		return num4;
	}

	private bool Fits(double fontSize, double width, double height)
	{
		FormattedText formattedText = CreateFormattedText(width, fontSize);
		double val = (double)Math.Max(1, MaximumLines) * fontSize * 1.55;
		if (formattedText.WidthIncludingTrailingWhitespace <= width + 0.5)
		{
			return formattedText.Height * 1.12 <= Math.Min(height + 0.5, val);
		}
		return false;
	}

	private FormattedText CreateFormattedText(double maxWidth, double fontSize)
	{
		double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		Typeface typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);
		return new FormattedText(Text ?? string.Empty, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, Math.Max(0.75, fontSize), Fill ?? Brushes.White, pixelsPerDip)
		{
			MaxTextWidth = Math.Max(1.0, maxWidth),
			TextAlignment = TextAlignment,
			Trimming = TextTrimming.None
		};
	}

	private double GetDrawingPadding()
	{
		return Math.Max(1.0, StrokeThickness * 1.5 + ShadowDepth + 2.0);
	}

	private static DependencyProperty Register(string name, Type type, object defaultValue, bool affectsMeasure = true)
	{
		FrameworkPropertyMetadataOptions frameworkPropertyMetadataOptions = FrameworkPropertyMetadataOptions.AffectsRender;
		if (affectsMeasure)
		{
			frameworkPropertyMetadataOptions |= FrameworkPropertyMetadataOptions.AffectsMeasure;
		}
		return DependencyProperty.Register(name, type, typeof(OutlinedText), new FrameworkPropertyMetadata(defaultValue, frameworkPropertyMetadataOptions));
	}
}
