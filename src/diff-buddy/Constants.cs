using System;
using System.Collections.Generic;
using LibGit2Sharp;

namespace diff_buddy;

public static class Constants
{
    public static readonly Dictionary<ChangeKind, string> StatusLookup = new()
    {
        [ChangeKind.Added] = "A    ",
        [ChangeKind.Deleted] = " D   ",
        [ChangeKind.Modified] = "  M  ",
        [ChangeKind.Renamed] = "   R ",
    };

    public static readonly Dictionary<ChangeKind, Action<string>> LinePrinters = new()
    {
        [ChangeKind.Added] = s => Console.WriteLine(s.BrightGreen()),
        [ChangeKind.Deleted] = s => Console.WriteLine(s.BrightRed()),
        [ChangeKind.Modified] = s => Console.WriteLine(s.BrightYellow()),
        [ChangeKind.Renamed] = s => Console.WriteLine(s.Grey())
    };
}