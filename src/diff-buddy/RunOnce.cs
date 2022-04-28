using System;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using PeanutButter.Utils;
using static diff_buddy.Functions;

namespace diff_buddy;

public static class OptionsExtensions
{
    public static void SetCurrentBranchAsToBranchIfNotSet(
        this Options options
    )
    {
        if (!string.IsNullOrWhiteSpace(options.To))
        {
            return;
        }

        using var repo = new Repository(options.Repo);
        options.To = repo.Head.FriendlyName;
    }
}

public static class RepositoryExtensions
{
    private static readonly Func<Repository, string, Tree>[] Strategies =
    {
        FindMatchingBranch,
        FindMatchingTag,
        FindMatchingSha
    };

    private static Tree FindMatchingSha(Repository repo, string sha)
    {
        return repo.Commits.FirstOrDefault(c => c.Sha == sha)?.Tree;
    }

    private static Tree FindMatchingTag(Repository repo, string search)
    {
        var sha = repo.Tags.FirstOrDefault(b => b.FriendlyName == search)?.Target?.Sha;
        return sha == null
            ? null
            : FindMatchingSha(repo, sha);
    }

    private static Tree FindMatchingBranch(Repository repo, string search)
    {
        return repo.Branches.FirstOrDefault(b => b.FriendlyName == search)?.Tip.Tree;
    }

    public static Tree FindTreeFor(
        this Repository repo,
        string identifier
    )
    {
        return Strategies.Aggregate(
            null as Tree,
            (acc, cur) => acc ?? cur(repo, identifier)
        );
    }
}

public static class RunOnce
{
    public static int With(Options options, bool showFilePathAtEnd)
    {
        if (!options.TryResolveRepository())
        {
            return Bail(2, $"{options.Repo} is not within a git repository (try --help for help)");
        }

        if (options.At != 0)
        {
            options.Offset = options.At - 1;
            options.Limit = 1;
            options.ShowPatches = true;
        }

        try
        {
            options.SetCurrentBranchAsToBranchIfNotSet();
        }
        catch (RepositoryNotFoundException)
        {
            // should never get here because of check above, but, just in case
            return Bail(2, $"{options.Repo} is not the base of a git repository (try --help for help)");
        }

        if (options.From == options.To)
        {
            return Bail(3,
                $"Nothing to compare: same branches selected for from ('{options.From}') and to ('{options.To}')");
        }

        using var repo = new Repository(options.Repo);
        var leftTree = repo.FindTreeFor(options.From) ?? throw new CommitIdentifierNotFound(options.Repo, options.From);
        var rightTree = repo.FindTreeFor(options.To) ?? throw new CommitIdentifierNotFound(options.Repo, options.To);

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
            if (CanSkipChange(options, patchLines, ignoreLineExpressions))
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

    private static bool CanSkipChange(Options options, string[] patchLines, Regex[] ignoreLineExpressions)
    {
        var added = FindInterestingLines(patchLines, "+", ignoreLineExpressions);
        var removed = FindInterestingLines(patchLines, "-", ignoreLineExpressions);
        if (added.IsEmpty() && removed.IsEmpty())
        {
            return true;
        }

        if (options.IgnoreWhitespace &&
            AreSameWithoutWhitespace(added, removed))
        {
            return true;
        }

        return false;
    }

    private static int Bail(int result, string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(" (try --help for help)");
        return result;
    }
}

public class CommitIdentifierNotFound
    : Exception
{
    public CommitIdentifierNotFound(
        string repoPath,
        string identifier
    ): base ($"Unable to identify a branch, tag or commit in '{repoPath}' by '{identifier}'")
    {
    }
}