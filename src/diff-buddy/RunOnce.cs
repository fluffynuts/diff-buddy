using System;
using System.Collections.Generic;
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
        var allChanges = repo.Diff.Compare<Patch>(leftTree, rightTree, new CompareOptions()).ToList();
        var couldIgnoreParentMerges = LooksLikeACommitSha(options.From);
        if (couldIgnoreParentMerges)
        {
            if (options.IgnoreParentMerges)
            {
                var mainBranch = options.ParentBranch ?? DetermineMainBranchOf(repo);
                if (mainBranch is not null)
                {
                    Console.Error.WriteLine("Looking for pure parent-merge deltas to ignore...");
                    var mainTree = repo.FindTreeFor(mainBranch);
                    var fullDelta = repo.Diff.Compare<Patch>(mainTree, rightTree).ToArray();
                    RemovePatchEntriesNotInTheFullDelta(allChanges, fullDelta);
                }
            }
        }

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

        if (couldIgnoreParentMerges && !options.IgnoreParentMerges)
        {
            Console.Error.WriteLine(
                $"Hint: specify --ignore-parent-merges to ignore whole-file deltas brought in purely by merging in {options.ParentBranch}"
            );
        }

        return 0;
    }

    private static void RemovePatchEntriesNotInTheFullDelta(
        List<PatchEntryChanges> partialDelta,
        PatchEntryChanges[] fullDelta
    )
    {
        // if a path which exists in the partial delta isn't found in the full delta, then
        // it's something that was introduced by a merge in from the parent branch
        // - ideally, we would want to filter out all changes brought in from parent
        //   merges, but anything other than "the entire file came in" would mean delving
        //   down to the patch level. This should catch any file added, removed or modified
        //   in the parent branch only
        var fullDeltaPaths = fullDelta.Select(o => o.Path).ToHashSet(StringComparer.Ordinal);
        var toRemove = new List<PatchEntryChanges>();
        foreach (var item in partialDelta)
        {
            if (!fullDeltaPaths.Contains(item.Path))
            {
                toRemove.Add(item);
            }
        }

        foreach (var item in toRemove)
        {
            partialDelta.Remove(item);
        }
    }

    private static string DetermineMainBranchOf(Repository repo)
    {
        var haveMaster = repo.Branches.Any(b => b.UpstreamBranchCanonicalName == "refs/heads/master");
        if (haveMaster)
        {
            return "master";
        }

        var haveMain = repo.Branches.Any(b => b.UpstreamBranchCanonicalName == "refs/heads/main");
        if (haveMain)
        {
            return "main";
        }

        // dunno
        return null;
    }

    private static bool LooksLikeACommitSha(string identifier)
    {
        return ShaMatcher.IsMatch(identifier);
    }

    private static readonly Regex ShaMatcher = new Regex("^[a-f0-9]{40}$");

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
    ) : base($"Unable to identify a branch, tag or commit in '{repoPath}' by '{identifier}'")
    {
    }
}