using System;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using static diff_buddy.Constants;

namespace diff_buddy;

public static class Functions
{
    public static string[] FindCodeLines(PatchEntryChanges change)
    {
        return change.Patch.Split(new[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .SkipWhile(l => !l.StartsWith("@@"))
            .Skip(1)
            .ToArray();
    }

    public static string[] FindInterestingLines(
        string[] patchLines,
        string prefix,
        Regex[] ignoreLineExpressions
    )
    {
        return patchLines
            .Where(l => l.StartsWith(prefix))
            .Select(l => string.Join("", l.Substring(1).Where(c => Char.IsAscii(c))))
            .Where(l => !ignoreLineExpressions.Any(re => re.IsMatch(l)))
            .ToArray();
    }

    public static void PrintEntry(
        Options options,
        int seen,
        int indexSize,
        PatchEntryChanges change
    )
    {
        if (options.CountOnly)
        {
            return;
        }

        var idx = options.ShowIndexes
            ? $"[{(seen + options.Offset).ToString($"D{indexSize}")}] "
            : "";
        var operation = options.ShowOperations
            ? $"{OperationFor(change)} "
            : "";
        var toPrint = $"{idx}{operation}{change.Path}";
        if (options.ShowPatches)
        {
            Console.WriteLine(toPrint.BrightMagenta());
        }
        else
        {
            if (LinePrinters.TryGetValue(change.Status, out var p))
            {
                p(toPrint);
            }
            else
            {
                Console.WriteLine(toPrint);
            }
        }
    }

    public static void ShowPatchIfRequired(Options options, string[] patchLines)
    {
        if (options.CountOnly)
        {
            return;
        }

        if (!options.ShowPatches)
        {
            return;
        }

        foreach (var line in patchLines)
        {
            Console.WriteLine(Colorise(line));
        }

        Console.WriteLine("");
    }

    private static string Colorise(string line)
    {
        if (line.StartsWith("+"))
        {
            return line.BrightGreen();
        }

        if (line.StartsWith("-"))
        {
            return line.BrightRed();
        }

        return line;
    }

    private static string OperationFor(PatchEntryChanges p)
    {
        return StatusLookup.TryGetValue(p.Status, out var result)
            ? result
            : "    -";
    }

    public static bool AreSameWithoutWhitespace(string[] added, string[] removed)
    {
        var squishedAdded = Squish(added);
        var squishedRemoved = Squish(removed);
        return squishedAdded == squishedRemoved;
    }

    private static readonly Regex WhitespaceRegex = new Regex("\\s+");

    private static string Squish(string[] lines)
    {
        return string.Join("",
            lines.Select(
                l => WhitespaceRegex.Replace(l, "")
            ).Where(
                l => !string.IsNullOrWhiteSpace(l)
            ));
    }


    public static string[] AsQuotedArgs(
        this string[] args
    )
    {
        return args
            .Select(s => s.Contains(" ")
                ? $"\"{s}\""
                : s
            ).ToArray();
    }
}