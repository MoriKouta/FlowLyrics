using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowLyrics.Services;

namespace FlowLyrics.Controls;

internal static class PlayerControlVisuals
{
	public const double ButtonSize = 30;
	public const double ButtonSpacing = 2;
	public const double BorderWidth = 1.25;
	public const double IconFontSize = 16.5;
	public const double NeutralOpacity = .72;
	public const double HoverOpacity = 1;
	public const double DotDiameter = 1.8;
	public const double DotPitch = 2.55;
	public const double EllipsisDotDiameter = 2;
	public const double EllipsisPitch = 6;
	public const double DotIconExtent = 6 * DotPitch + DotDiameter;

	// Canonical player visuals, shared by the overlay and the timing editor.
	[ThreadStatic] private static ResourceDictionary? _playbackResources;
	private static ResourceDictionary PlaybackResources => _playbackResources ??= (ResourceDictionary)System.Windows.Markup.XamlReader.Parse("""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
<GeometryGroup x:Key="PlayDotGeometry">
        <EllipseGeometry Center="2.5,2.5" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="2.5,5.5" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="2.5,8.5" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="2.5,11.5" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="2.5,14.5" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="5.5,4" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="5.5,7" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="5.5,10" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="5.5,13" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="8.5,5.5" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="8.5,8.5" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="8.5,11.5" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="11.5,7" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="11.5,10" RadiusX="1.1" RadiusY="1.1" />
        <EllipseGeometry Center="14.5,8.5" RadiusX="1.1" RadiusY="1.1" />
      </GeometryGroup>
<Style x:Key="MediaButton" TargetType="{x:Type Button}">
        <Setter Property="Width" Value="42" />
        <Setter Property="Height" Value="42" />
        <Setter Property="Margin" Value="5,0" />
        <Setter Property="Padding" Value="0" />
        <Setter Property="Foreground" Value="#FFFFFFFF" />
		<Setter Property="Background" Value="#2EFFFFFF" />
        <Setter Property="BorderBrush" Value="{DynamicResource UiAccentBrush}" />
        <Setter Property="BorderThickness" Value="1.25" />
        <Setter Property="FontFamily" Value="{DynamicResource DotFont}" />
        <Setter Property="FontSize" Value="18" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="FrameworkElement.Cursor" Value="Hand" />
        <Setter Property="Template">
          <Setter.Value>
            <ControlTemplate TargetType="{x:Type Button}">
              <Border Name="ButtonSurface" CornerRadius="100" Background="{TemplateBinding Control.Background}" BorderBrush="{TemplateBinding Control.BorderBrush}" BorderThickness="{TemplateBinding Control.BorderThickness}">
                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="UIElement.IsMouseOver" Value="True">
                  <Setter TargetName="ButtonSurface" Value="{DynamicResource UiAccentBrush}" Property="Border.Background" />
                  <Setter TargetName="ButtonSurface" Value="{DynamicResource UiAccentBrush}" Property="Border.BorderBrush" />
                </Trigger>
                <Trigger Property="IsPressed" Value="True">
                  <Setter TargetName="ButtonSurface" Property="UIElement.Opacity" Value="0.72" />
                </Trigger>
                <Trigger Property="IsEnabled" Value="False">
                  <Setter TargetName="ButtonSurface" Property="UIElement.Opacity" Value="0.3" />
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Style>
</ResourceDictionary>
""");
	public static Geometry PlayGeometry => (Geometry)PlaybackResources["PlayDotGeometry"];
	public static Style PlaybackButtonStyle => (Style)PlaybackResources["MediaButton"];
	public static UIElement PauseIcon() => (UIElement)System.Windows.Markup.XamlReader.Parse("""
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" HorizontalAlignment="Center" VerticalAlignment="Center" Orientation="Horizontal">
                      <StackPanel Margin="0,0,3,0">
                        <Ellipse Width="3" Height="3" Margin="0,0,0,1" Fill="#FFFFFFFF" />
                        <Ellipse Width="3" Height="3" Margin="0,0,0,1" Fill="#FFFFFFFF" />
                        <Ellipse Width="3" Height="3" Margin="0,0,0,1" Fill="#FFFFFFFF" />
                        <Ellipse Width="3" Height="3" Fill="#FFFFFFFF" />
                      </StackPanel>
                      <StackPanel Margin="3,0,0,0">
                        <Ellipse Width="3" Height="3" Margin="0,0,0,1" Fill="#FFFFFFFF" />
                        <Ellipse Width="3" Height="3" Margin="0,0,0,1" Fill="#FFFFFFFF" />
                        <Ellipse Width="3" Height="3" Margin="0,0,0,1" Fill="#FFFFFFFF" />
                        <Ellipse Width="3" Height="3" Fill="#FFFFFFFF" />
                      </StackPanel>
                    </StackPanel>
""");
	public static Grid PlaybackIcon(bool playing)
	{
		Grid icon = new() { Width = 24, Height = 21, IsHitTestVisible = false };
		icon.Children.Add(playing ? PauseIcon() : new Path { Data = PlayGeometry, Width = 17, Height = 18, Stretch = Stretch.Uniform, Fill = Brushes.White });
		return icon;
	}
	public static Canvas RepeatIcon() => DotIcon(["0000010", "0111111", "1000010", "1000001", "0100001", "1111110", "0100000"]);
	public static Canvas ShuffleIcon() => DotIcon(["0000010", "1100111", "0010010", "0001000", "0010010", "1100111", "0000010"]);

	private static Canvas DotIcon(string[] rows)
	{
		Canvas icon = new() { Width = DotIconExtent, Height = DotIconExtent, IsHitTestVisible = false };
		for (int y = 0; y < rows.Length; y++)
			for (int x = 0; x < rows[y].Length; x++)
				if (rows[y][x] == '1')
				{
					Ellipse dot = new() { Width = DotDiameter, Height = DotDiameter, Fill = Brushes.White };
					Canvas.SetLeft(dot, x * DotPitch); Canvas.SetTop(dot, y * DotPitch); icon.Children.Add(dot);
				}
		return icon;
	}

	public static void Size(Button button)
	{
		button.Width = button.Height = ButtonSize;
		button.Margin = new Thickness(ButtonSpacing, 0, ButtonSpacing, 0);
		button.Padding = new Thickness(0);
		button.BorderThickness = new Thickness(BorderWidth);
		button.UseLayoutRounding = true;
		button.SnapsToDevicePixels = true;
	}

	public static TextBlock Letter(string letter)
	{
		var icon = new TextBlock { Text = letter, FontFamily = LocalizedUiFont.EnglishDotFont,
			FontSize = IconFontSize, LineHeight = IconFontSize, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
			TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
			RenderTransform = new TranslateTransform(1.2, 0), IsHitTestVisible = false };
		LocalizedUiFont.Technical(icon);
		TextOptions.SetTextFormattingMode(icon, TextFormattingMode.Display);
		return icon;
	}

	public static void IconState(Button button, UIElement icon, bool active, double neutral = NeutralOpacity)
		=> icon.Opacity = active || (button.IsEnabled && button.IsMouseOver) ? HoverOpacity : neutral;

	public static void TrackHover(Button button, Action refresh)
	{
		button.MouseEnter += (_, _) => refresh(); button.MouseLeave += (_, _) => refresh();
		button.IsEnabledChanged += (_, _) => refresh();
	}
}
