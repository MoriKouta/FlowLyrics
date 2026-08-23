using System.Windows;
using System.Windows.Markup;

namespace FlowLyrics;

internal static class PersonalSyncUiTheme
{
	private const string AppliedKey = "PersonalSyncUiThemeApplied";

	public static void Apply(Window window)
	{
		if (window.Resources.Contains(AppliedKey)) return;
		const string xaml = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="SyncAccent" Color="#FFFF6B2C" />
  <SolidColorBrush x:Key="SyncControl" Color="#FF312E32" />
  <SolidColorBrush x:Key="SyncControlHover" Color="#FF3A353B" />
  <SolidColorBrush x:Key="SyncBorder" Color="#FF59535B" />
  <SolidColorBrush x:Key="SyncText" Color="#FFF0EDF0" />
  <SolidColorBrush x:Key="SyncMuted" Color="#FFB5B0B5" />

  <Style TargetType="{x:Type Button}">
    <Setter Property="Foreground" Value="{StaticResource SyncText}" />
    <Setter Property="Background" Value="{StaticResource SyncControl}" />
    <Setter Property="BorderBrush" Value="{StaticResource SyncBorder}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="12,8" />
    <Setter Property="Margin" Value="3" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type Button}">
          <Border x:Name="Surface" Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"
                  CornerRadius="7" Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
            <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                              VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                              RecognizesAccessKey="True" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Surface" Property="Background" Value="{StaticResource SyncControlHover}" />
              <Setter TargetName="Surface" Property="BorderBrush" Value="{StaticResource SyncAccent}" />
            </Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Surface" Property="Opacity" Value="0.68" /></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Surface" Property="Opacity" Value="0.34" /></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="{x:Type TextBox}">
    <Setter Property="Foreground" Value="{StaticResource SyncText}" />
    <Setter Property="Background" Value="#FF201E21" />
    <Setter Property="BorderBrush" Value="{StaticResource SyncBorder}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="10,7" />
    <Setter Property="CaretBrush" Value="{StaticResource SyncAccent}" />
  </Style>

  <Style TargetType="{x:Type CheckBox}">
    <Setter Property="Foreground" Value="{StaticResource SyncText}" />
    <Setter Property="VerticalAlignment" Value="Center" />
  </Style>

  <Style TargetType="{x:Type ListBox}">
    <Setter Property="Background" Value="#FF201E21" />
    <Setter Property="Foreground" Value="{StaticResource SyncText}" />
    <Setter Property="BorderBrush" Value="{StaticResource SyncBorder}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled" />
  </Style>

  <Style TargetType="{x:Type ListBoxItem}">
    <Setter Property="Foreground" Value="{StaticResource SyncText}" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Padding" Value="8,6" />
    <Setter Property="HorizontalContentAlignment" Value="Stretch" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type ListBoxItem}">
          <Border x:Name="ItemSurface" Background="{TemplateBinding Background}"
                  BorderBrush="Transparent" BorderThickness="1" CornerRadius="6"
                  Padding="{TemplateBinding Padding}" Margin="2,1">
            <ContentPresenter />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="ItemSurface" Property="Background" Value="#FF302C31" /></Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="ItemSurface" Property="Background" Value="#3FFF6B2C" />
              <Setter TargetName="ItemSurface" Property="BorderBrush" Value="{StaticResource SyncAccent}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="SyncScrollThumb" TargetType="{x:Type Thumb}">
    <Setter Property="Background" Value="{StaticResource SyncAccent}" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type Thumb}">
          <Border Background="{TemplateBinding Background}" CornerRadius="4" Margin="1" />
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="{x:Type ScrollBar}">
    <Setter Property="Background" Value="#FF201E21" />
    <Style.Triggers>
      <Trigger Property="Orientation" Value="Vertical">
        <Setter Property="Width" Value="10" />
        <Setter Property="Template">
          <Setter.Value>
            <ControlTemplate TargetType="{x:Type ScrollBar}">
              <Grid Background="{TemplateBinding Background}">
                <Track x:Name="PART_Track" IsDirectionReversed="True">
                  <Track.DecreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageUpCommand}" Opacity="0" /></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb MinHeight="24" Style="{StaticResource SyncScrollThumb}" /></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageDownCommand}" Opacity="0" /></Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Trigger>
      <Trigger Property="Orientation" Value="Horizontal">
        <Setter Property="Height" Value="10" />
        <Setter Property="Template">
          <Setter.Value>
            <ControlTemplate TargetType="{x:Type ScrollBar}">
              <Grid Background="{TemplateBinding Background}">
                <Track x:Name="PART_Track">
                  <Track.DecreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageLeftCommand}" Opacity="0" /></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb MinWidth="24" Style="{StaticResource SyncScrollThumb}" /></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageRightCommand}" Opacity="0" /></Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Trigger>
    </Style.Triggers>
  </Style>
</ResourceDictionary>
""";
		ResourceDictionary dictionary = (ResourceDictionary)XamlReader.Parse(xaml);
		window.Resources.MergedDictionaries.Add(dictionary);
		window.Resources[AppliedKey] = true;
	}
}
