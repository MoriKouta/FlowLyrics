using System;
using System.Windows.Media;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

internal static class BlendModeService
{
    internal static readonly string[] Modes = { "Normal", "Auto", "Invert", "Screen", "Overlay" };

    internal static string ResolveMode(AppSettings settings, string individualMode)
    {
        return string.Equals(settings.BlendModeScope, "Individual", StringComparison.OrdinalIgnoreCase)
            ? Normalize(individualMode)
            : Normalize(settings.GlobalBlendMode);
    }

    internal static bool RequiresBackdropSampling(AppSettings settings)
    {
        if (!string.Equals(settings.BlendModeScope, "Individual", StringComparison.OrdinalIgnoreCase))
        {
            return !string.Equals(Normalize(settings.GlobalBlendMode), "Normal", StringComparison.Ordinal);
        }

        return !string.Equals(Normalize(settings.CurrentTextBlendMode), "Normal", StringComparison.Ordinal)
            || !string.Equals(Normalize(settings.NextTextBlendMode), "Normal", StringComparison.Ordinal)
            || !string.Equals(Normalize(settings.OutlineBlendMode), "Normal", StringComparison.Ordinal)
            || !string.Equals(Normalize(settings.ShadowBlendMode), "Normal", StringComparison.Ordinal)
            || !string.Equals(Normalize(settings.BackgroundBlendMode), "Normal", StringComparison.Ordinal)
            || !string.Equals(Normalize(settings.BorderBlendMode), "Normal", StringComparison.Ordinal)
            || !string.Equals(Normalize(settings.UiBlendMode), "Normal", StringComparison.Ordinal);
    }

    internal static Color Apply(Color source, Color backdrop, string mode)
    {
        mode = Normalize(mode);
        if (mode == "Normal")
        {
            return source;
        }

        byte r;
        byte g;
        byte b;
        switch (mode)
        {
            case "Auto":
                var luminance = (0.2126 * backdrop.R) + (0.7152 * backdrop.G) + (0.0722 * backdrop.B);
                r = g = b = luminance >= 142.0 ? (byte)18 : (byte)248;
                break;
            case "Invert":
                r = (byte)(255 - backdrop.R);
                g = (byte)(255 - backdrop.G);
                b = (byte)(255 - backdrop.B);
                break;
            case "Screen":
                r = Screen(source.R, backdrop.R);
                g = Screen(source.G, backdrop.G);
                b = Screen(source.B, backdrop.B);
                break;
            case "Overlay":
                r = Overlay(source.R, backdrop.R);
                g = Overlay(source.G, backdrop.G);
                b = Overlay(source.B, backdrop.B);
                break;
            default:
                return source;
        }

        return Color.FromArgb(source.A, r, g, b);
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToUpperInvariant() switch
        {
            "AUTO" => "Auto",
            "INVERT" => "Invert",
            "SCREEN" => "Screen",
            "OVERLAY" => "Overlay",
            _ => "Normal"
        };
    }

    private static byte Screen(byte source, byte backdrop)
    {
        return (byte)Math.Clamp(255 - (((255 - source) * (255 - backdrop)) / 255), 0, 255);
    }

    private static byte Overlay(byte source, byte backdrop)
    {
        var value = backdrop < 128
            ? (2 * source * backdrop) / 255
            : 255 - ((2 * (255 - source) * (255 - backdrop)) / 255);
        return (byte)Math.Clamp(value, 0, 255);
    }
}
