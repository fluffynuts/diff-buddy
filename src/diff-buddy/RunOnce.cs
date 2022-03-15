using System;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using PeanutButter.Utils;
using static diff_buddy.Functions;

namespace diff_buddy;

public static class RunOnce
{
    public static int With(Options options, bool showFilePathAtEnd)
    {
        if (options.At != 0)
        {
            options.Offset = options.At - 1;
            options.Limit = 1;
            options.ShowPatches = true;
        }

        using var repo = new Repository(options.Repo);
        if (string.IsNullOrWhiteSpace(options.ToBranch))
        {
            options.ToBranch = repo.Head.FriendlyName;
        }

        var leftTree = repo.Branches.FirstOrDefault(b => b.FriendlyName == options.FromBranch)?.Tip.Tree;
        var rightTree = repo.Branches.FirstOrDefault(b => b.FriendlyName == options.ToBranch)?.Tip.Tree;

        var ignoreLineExpressions = (options.IgnoreLines ?? new string[0]).Select(s => new Regex(s)).ToArray();
        var ignoreFileExpressions = (options.IgnoreFiles ?? new string[0]).Select(s => new Regex(s)).ToArray();

        var toSkip = options.Offset;
        var seen = 0;
        var allChanges = repo.Diff.Compare<Patch>(leftTree, rightTree, new CompareOptions());
        var indexSize = $"{Math.Min(options.Limit, allChanges.Count())}".Length;
        foreach (var change in allChanges)
        {
            if (ignoreFileExpressions.Any(re => re.IsMatch(change.Path)))
            {
                continue;
            }

            var patchLines = FindCodeLines(change);
            var added = FindInterestingLines(patchLines, "+", ignoreLineExpressions);
            var removed = FindInterestingLines(patchLines, "-", ignoreLineExpressions);
            if (added.IsEmpty() && removed.IsEmpty())
            {
                continue;
            }

            if (options.IgnoreWhitespace &&
                AreSameWithoutWhitespace(added, removed))
            {
                continue;
            }

            if (--toSkip >= 0)
            {
                continue;
            }

            seen++;
            if (showFilePathAtEnd)
            {
                ShowPatchIfRequired(options, patchLines);
                PrintEntry(options, seen, indexSize, change);
            }
            else
            {
                PrintEntry(options, seen, indexSize, change);
                ShowPatchIfRequired(options, patchLines);
            }


            if (seen >= options.Limit)
            {
                if (options.At == 0 && !options.CountOnly)
                {
                    Console.WriteLine($"Output limit reached: {options.Limit}");
                }

                break;
            }
        }

        if (options.CountOnly)
        {
            Console.WriteLine($"{seen}");
        }
        else if (seen < options.Limit && !options.ShowIndexes)
        {
            Console.WriteLine($"{seen} results");
        }

        return 0;
    }
}