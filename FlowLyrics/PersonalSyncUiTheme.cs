using System.Windows;
using System.Windows.Markup;
using FlowLyrics.Services;

namespace FlowLyrics;

internal static class PersonalSyncUiTheme
{
	private const string AppliedKey = "PersonalSyncUiThemeApplied";

	public static void Apply(Window window, string language)
	{
		LocalizedUiFont.Apply(window, language, LocalizedUiFont.EnglishDotFont);
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
    <Setter Property="ContentTemplate"><Setter.Value><DataTemplate>
      <TextBlock Text="{Binding}" TextWrapping="Wrap" TextTrimming="None" TextAlignment="Center" />
    </DataTemplate></Setter.Value></Setter>
    <Setter Property="Foreground" Value="{StaticResource SyncText}" />
    <Setter Property="Background" Value="{StaticResource SyncControl}" />
    <Setter Property="BorderBrush" Value="{StaticResource SyncBorder}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="10,6" />
    <Setter Property="MinHeight" Value="32" />
    <Setter Property="HorizontalContentAlignment" Value="Center" />
    <Setter Property="VerticalContentAlignment" Value="Center" />
    <Setter Property="Margin" Value="3" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type Button}">
          <Border x:Name="Surface" Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"
                  CornerRadius="4" Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
            <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                              VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                              RecognizesAccessKey="True" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Surface" Property="Opacity" Value="0.86" />
              <Setter TargetName="Surface" Property="BorderBrush" Value="{StaticResource SyncAccent}" />
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Surface" Property="BorderBrush" Value="{StaticResource SyncAccent}" /></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Surface" Property="Opacity" Value="0.68" /></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Surface" Property="Opacity" Value="0.34" /></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="{x:Type Expander}">
    <Setter Property="Foreground" Value="{StaticResource SyncText}" />
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="{x:Type Expander}">
      <StackPanel>
        <ToggleButton x:Name="HeaderToggle" Content="{TemplateBinding Header}" Foreground="{TemplateBinding Foreground}"
                      IsChecked="{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" HorizontalContentAlignment="Left" Cursor="Hand">
          <ToggleButton.Template><ControlTemplate TargetType="{x:Type ToggleButton}">
            <Border Padding="2,7" Background="Transparent">
              <StackPanel Orientation="Horizontal">
                <TextBlock x:Name="Arrow" Text="›" Width="18" Foreground="{TemplateBinding Foreground}" />
                <ContentPresenter />
              </StackPanel>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsChecked" Value="True"><Setter TargetName="Arrow" Property="Text" Value="⌄" /></Trigger>
              <Trigger Property="IsMouseOver" Value="True"><Setter Property="Foreground" Value="{StaticResource SyncAccent}" /></Trigger>
              <Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="Foreground" Value="{StaticResource SyncAccent}" /></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate></ToggleButton.Template>
        </ToggleButton>
        <ContentPresenter x:Name="Body" Visibility="Collapsed" />
      </StackPanel>
      <ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="True"><Setter TargetName="Body" Property="Visibility" Value="Visible" /></Trigger></ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
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
    <Setter Property="ContentTemplate"><Setter.Value><DataTemplate>
      <TextBlock Text="{Binding}" TextWrapping="Wrap" TextTrimming="None" />
    </DataTemplate></Setter.Value></Setter>
    <Setter Property="Foreground" Value="{StaticResource SyncText}" />
    <Setter Property="VerticalAlignment" Value="Center" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="{x:Type CheckBox}">
      <Grid x:Name="Toggle" Background="Transparent">
        <Grid.ColumnDefinitions><ColumnDefinition Width="Auto" /><ColumnDefinition Width="*" /></Grid.ColumnDefinitions>
        <Border x:Name="Box" Width="17" Height="17" CornerRadius="3" BorderThickness="1"
                BorderBrush="{StaticResource SyncBorder}" Background="{StaticResource SyncControl}" VerticalAlignment="Center">
          <Path x:Name="Check" Data="M 3,7 L 6,10 L 12,3" Stroke="#FF1C191C" StrokeThickness="2" Visibility="Collapsed" />
        </Border>
        <ContentPresenter Grid.Column="1" Margin="8,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True" />
      </Grid>
      <ControlTemplate.Triggers>
        <Trigger Property="IsChecked" Value="True">
          <Setter TargetName="Box" Property="Background" Value="{StaticResource SyncAccent}" />
          <Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource SyncAccent}" />
          <Setter TargetName="Check" Property="Visibility" Value="Visible" />
        </Trigger>
        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource SyncAccent}" /></Trigger>
        <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="White" /></Trigger>
        <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Toggle" Property="Opacity" Value="0.38" /></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>

  <Style TargetType="{x:Type ListBox}">
    <Setter Property="Background" Value="#FF201E21" />
    <Setter Property="Foreground" Value="{StaticResource SyncText}" />
    <Setter Property="BorderBrush" Value="{StaticResource SyncBorder}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled" />
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="{x:Type ListBox}">
      <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="10" Padding="3">
        <ScrollViewer Focusable="False" Padding="{TemplateBinding Padding}"><ItemsPresenter /></ScrollViewer>
      </Border>
    </ControlTemplate></Setter.Value></Setter>
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
              <Setter TargetName="ItemSurface" Property="Background" Value="#FF36343A" />
              <Setter TargetName="ItemSurface" Property="BorderBrush" Value="#FF66616B" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

</ResourceDictionary>
""";
		ResourceDictionary dictionary = (ResourceDictionary)XamlReader.Parse(xaml);
		window.Resources.MergedDictionaries.Add(dictionary);
		EditorControlChrome.ApplyScrollBars(window, (System.Windows.Media.Brush)dictionary["SyncAccent"],
			(System.Windows.Media.Brush)dictionary["SyncBorder"], System.Windows.Media.Brushes.White);
		window.Resources[AppliedKey] = true;
	}
}
