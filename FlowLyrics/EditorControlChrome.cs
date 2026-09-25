using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FlowLyrics.Services;

namespace FlowLyrics;

// Shared editor chrome uses the existing Settings controls, including their hover/pressed states.
internal static class EditorControlChrome
{
	public static void ConfigureClose(Button button, Brush accent)
	{
		button.Content = "CLOSE"; button.Tag = "NoTranslate";
		LocalizedUiFont.Technical(button);
		button.FontSize = 11; button.MinHeight = 32; button.Padding = new Thickness(12, 6, 12, 6);
		button.BorderThickness = new Thickness(1); button.BorderBrush = accent;
		button.Background = accent; button.Foreground = Brushes.White;
		button.Template = CreateButtonTemplate(accent);
	}

	public static void ApplyScrollBars(Window window, Brush accent, Brush track, Brush grip)
	{
		window.Resources["FaderAccentBrush"] = accent;
		window.Resources["FaderTrackBrush"] = track;
		window.Resources["FaderGripBrush"] = grip;
		Style style = CreateFaderScrollBarStyle();
		window.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = style;
		// ScrollViewer's theme template can give its bars an explicit system style.
		window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
			new System.Action(() => Apply(window)));
		void Apply(DependencyObject parent)
		{
			if (parent is System.Windows.Controls.Primitives.ScrollBar { Orientation: Orientation.Vertical } bar) bar.Style = style;
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) Apply(VisualTreeHelper.GetChild(parent, i));
		}
	}

	public static ControlTemplate CreateButtonTemplate(System.Windows.Media.Brush accent)
	{
		FrameworkElementFactory surface = new FrameworkElementFactory(typeof(Border), "Surface");
		surface.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(8.0));
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("ContentTemplate") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		presenter.SetValue(System.Windows.FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
		presenter.SetValue(System.Windows.FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		surface.AppendChild(presenter);

		ControlTemplate template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = surface };
		Trigger hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
		hover.Setters.Add(new Setter(Border.BorderBrushProperty, accent, "Surface"));
		hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.86, "Surface"));
		template.Triggers.Add(hover);
		Trigger pressed = new Trigger { Property = System.Windows.Controls.Button.IsPressedProperty, Value = true };
		pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.68, "Surface"));
		template.Triggers.Add(pressed);
		Trigger disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
		disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.38, "Surface"));
		template.Triggers.Add(disabled);
		return template;
	}

	public static Style CreateFaderScrollBarStyle()
	{
		const string xaml = """
<Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       TargetType="{x:Type ScrollBar}">
  <Setter Property="Width" Value="16" />
  <Setter Property="Orientation" Value="Vertical" />
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="{x:Type ScrollBar}">
        <Grid Width="16" Background="Transparent">
          <Border Width="2" HorizontalAlignment="Center" Background="{DynamicResource FaderTrackBrush}" CornerRadius="1" />
          <Track x:Name="PART_Track" Orientation="Vertical" IsDirectionReversed="True">
            <Track.DecreaseRepeatButton>
              <RepeatButton Command="{x:Static ScrollBar.PageUpCommand}" Background="Transparent" BorderThickness="0" Opacity="0.01" />
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command="{x:Static ScrollBar.PageDownCommand}" Background="Transparent" BorderThickness="0" Opacity="0.01" />
            </Track.IncreaseRepeatButton>
            <Track.Thumb>
              <Thumb Width="12" MinHeight="34">
                <Thumb.Template>
                  <ControlTemplate TargetType="{x:Type Thumb}">
                    <Border Background="{DynamicResource FaderAccentBrush}" CornerRadius="3">
                      <Grid Width="7" Height="9" HorizontalAlignment="Center" VerticalAlignment="Center">
                        <Rectangle Height="1" VerticalAlignment="Top" Fill="{DynamicResource FaderGripBrush}" />
                        <Rectangle Height="1" VerticalAlignment="Center" Fill="{DynamicResource FaderGripBrush}" />
                        <Rectangle Height="1" VerticalAlignment="Bottom" Fill="{DynamicResource FaderGripBrush}" />
                      </Grid>
                    </Border>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
""";
		return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
	}

}
