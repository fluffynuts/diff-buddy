using System.IO;
using PeanutButter.EasyArgs.Attributes;

namespace diff_buddy;

// ReSharper disable once ClassNeverInstantiated.Global
public class Options
{
    [Description("Display a count of matching files only, and exit")]
    public bool CountOnly { get; set; }

    [Description("Enable file-by-file review mode")]
    public bool Review { get; set; }

    [Description("When reviewing, pick up from the last viewed file")]
    [Default(true)]
    public bool Continue { get; set; }

    [Description("Starting branch, tag or commit sha for the diff")]
    [Default("master")]
    public string From { get; set; }

    [Description("Later branch, tag or commit to diff with")]
    public string To { get; set; }

    [Description("Regular expressions for lines to ignore when deciding on interesting files")]
    public string[] IgnoreLines { get; set; }

    [Description("Regular expressions for files to ignore completely")]
    public string[] IgnoreFiles { get; set; }

    [Default(".")]
    [Description("The path to the local repository to inspect")]
    public string Repo { get; set; }

    [Description("Whether or not to show patches for files")]
    public bool ShowPatches { get; set; }

    [Description("Limit the number of files to list")]
    [Default(int.MaxValue)]
    public int Limit { get; set; }

    [Description("Start printing from this offset")]
    [Default(0)]
    public int Offset { get; set; }

    [Description("Show the diff at the provided index only")]
    public int At { get; set; }

    [Description("Ignore whitespace changes, even those spanning multiple lines, when deciding on interesting files")]
    public bool IgnoreWhitespace { get; set; }

    [Description("Show the numeric index for each file")]
    [Default(true)]
    public bool ShowIndexes { get; set; }

    [Description("Show the operation which this file has undergone: (A)dded, (D)eleted, (M)odified, (R)enamed")]
    [Default(true)]
    public bool ShowOperations { get; set; }

    [Description("The amount to scroll up and down by when pressing PgUp or PgDn in the comments entry (overrides env var PAGE_SIZE, if set)")]
    [Default(null)]
    public int? PageSize { get; set; }

    public bool TryResolveRepository()
    {
        var fullPath = Path.GetFullPath(Repo);
        bool searching;
        do
        {
            var test = Path.Combine(fullPath, ".git");
            if (Directory.Exists(test))
            {
                Repo = fullPath;
                return true;
            }

            fullPath = Path.GetDirectoryName(fullPath);
            searching = fullPath is not null;
        } while (searching);

        return false;
    }
}