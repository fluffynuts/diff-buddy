using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Pastel;
using PeanutButter.Utils;

namespace diff_buddy;

public static class StringExtensions
{
    public static bool DisableColor { get; set; } = false;
    private static readonly Color _brightRed = Color.FromArgb(255, 255, 128, 128);
    private static readonly Color _brightGreen = Color.FromArgb(255, 128, 255, 0);
    private static readonly Color _brightBlue = Color.FromArgb(255, 128, 128, 255);
    private static readonly Color _brightCyan = Color.FromArgb(255, 128, 255, 255);
    private static readonly Color _brightYellow = Color.FromArgb(255, 255, 255, 128);
    private static readonly Color _brightMagenta = Color.FromArgb(255, 255, 128, 255);
    private static readonly Color _brightPink = Color.FromArgb(255, 255, 128, 160);
    private static readonly Color _grey = Color.FromArgb(255, 128, 128, 128);
    private static readonly Color _darkGrey = Color.FromArgb(255, 80, 80, 80);
    private static readonly Color _white = Color.FromArgb(255, 255, 255, 255);

    public static string Colorise(
        this string str,
        Color color
    )
    {
        return DisableColor
            ? str
            : str.Pastel(color);
    }

    public static string White(this string str)
    {
        return str.Colorise(_white);
    }

    public static string BrightRed(this string str)
    {
        return str.Colorise(_brightRed);
    }

    public static string BrightGreen(this string str)
    {
        return str.Colorise(_brightGreen);
    }

    public static string BrightCyan(this string str)
    {
        return str.Colorise(_brightCyan);
    }

    public static string BrightYellow(this string str)
    {
        return str.Colorise(_brightYellow);
    }

    public static string BrightMagenta(this string str)
    {
        return str.Colorise(_brightMagenta);
    }

    public static string BrightPink(this string str)
    {
        return str.Colorise(_brightPink);
    }

    public static string BrightBlue(this string str)
    {
        return str.Colorise(_brightBlue);
    }

    private static int _rainbow;

    private static readonly Dictionary<int, Func<string, string>> RainbowLookup
        = new()
        {
            [0] = BrightPink,
            [1] = BrightGreen,
            [2] = BrightCyan,
            [3] = BrightYellow,
            [4] = BrightMagenta,
            [5] = BrightBlue
        };

    private static readonly int RainbowOptions = RainbowLookup.Keys.Count;

    public static string Random(this string str)
    {
        var handler = RainbowLookup[_rainbow++ % RainbowOptions];
        return handler(str);
    }

    public static string Rainbow(this string str)
    {
        return str.Aggregate(
            new List<string>(),
            (acc, cur) =>
            {
                var handler = RainbowLookup[_rainbow++ % RainbowOptions];
                acc.Add(handler(cur.ToString()));
                return acc;
            }).JoinWith("");
    }

    public static string Grey(this string str)
    {
        return str.Colorise(_grey);
    }

    public static string DarkGrey(this string str)
    {
        return str.Colorise(_darkGrey);
    }

    public static string ToHex(this byte[] bytes)
    {
        if (bytes == null)
        {
            return null;
        }

        return string.Join("",
            bytes.Select(b => $"{b:x2}")
        );
    }

    public static string DefaultTo(this string str, string fallback)
    {
        return string.IsNullOrWhiteSpace(str)
            ? fallback
            : str;
    }

    public static HashSet<string> AsCaseInsensitiveHashSet(
        this IEnumerable<string> input)
    {
        return new HashSet<string>(
            input,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public static void PrintAll(
        this IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }
    }
}