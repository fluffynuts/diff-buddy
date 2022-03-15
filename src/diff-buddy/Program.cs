using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using diff_buddy;
using LibGit2Sharp;
using Pastel;
using PeanutButter.EasyArgs;
using PeanutButter.Utils;
using static diff_buddy.Constants;
using static diff_buddy.Functions;

if (Console.IsOutputRedirected)
{
    ConsoleExtensions.Disable();
}


var options = args.ParseTo<Options>();

if (options.Review)
{
    var optionsCopy = options.DeepClone();
    optionsCopy.Review = false;
    if (optionsCopy.Repo == ".")
    {
        optionsCopy.Repo = Environment.CurrentDirectory;
    }

    var cmd = Environment.CommandLine;
    var splitCmd = new Queue<string>(cmd.Split(' '));
    var exe = splitCmd.Dequeue().Replace(".dll", ".exe");
    while (splitCmd.Count > 0 && exe.Count(c => c == '"') % 2 != 0)
    {
        exe = $"{exe} {splitCmd.Dequeue()}";
    }

    exe = exe.Trim('"');

    optionsCopy.CountOnly = true;
    var pages = -1;
    using (var io = ProcessIO.Start(exe, optionsCopy.GenerateArgs().AsQuotedArgs()))
    {
        var result = io.StandardOutput.FirstOrDefault();
        if (result is null || !int.TryParse(result, out pages))
        {
            Console.WriteLine($"Unable to count changes in {options.Repo}");
            return 1;
        }
    }
    optionsCopy.CountOnly = false;

    for (var i = 1; i < pages; i++)
    {
        Console.Clear();
        optionsCopy.At = i;
        var childArgs = optionsCopy.GenerateArgs();
        var quotedArgs = new[] { exe }
            .Concat(childArgs)
            .ToArray()
            .AsQuotedArgs();
        var child = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = string.Join(" ", quotedArgs)
            }
        };
        child.Start();
        child.WaitForExit();

        Console.Write($"[{i}/{pages}] Press any key to continue, q to quit...");
        var c = Console.ReadKey();
        if (c.KeyChar == 'q')
        {
            return 0;
        }
    }

    return 0;
}

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
    PrintEntry(options, seen, indexSize, change);
    ShowPatchIfRequired(options, patchLines);

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