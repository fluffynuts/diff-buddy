using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using PeanutButter.Utils;

namespace diff_buddy;

public class ReviewStateItemData
{
    public string FileName { get; set; }

    public string Comments
    {
        get => _comments ?? "";
        set => _comments = value ?? "";
    }

    private string _comments;
}

public class ReviewStateItem : ReviewStateItemData
{
    private readonly ReviewState _parent;

    public ReviewStateItem(
        ReviewState parent,
        string fileName
    )
    {
        _parent = parent;
        FileName = fileName;
    }

    public ReviewStateItem(
        ReviewState parent,
        ReviewStateItemData template
    )
    {
        _parent = parent;
        FileName = template.FileName;
        Comments = template.Comments;
    }

    public void Persist()
    {
        _parent.Persist();
    }
}

public class ReviewState
{
    public string LastFile { get; private set; }
    public bool CanResume { get; }
    public string StateFile { get; }

    private readonly Options _options;
    private readonly List<ReviewStateItem> _reviewStateItems = new();
    
    public string ReviewCommentsFile =>
        StateFile.RegexReplace("\\.json$", ".review.txt");

    public ReviewState(Options options)
    {
        _options = options;
        var home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile
        );
        var repoPathParts = options.Repo.Split(new[] { "\\", "/" }, StringSplitOptions.None)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();
        var folderName = repoPathParts.Last();
        var filename = $@"diff-buddy-state-({
            folderName
        })-({
            options.FromBranch
        })-({
            options.ToBranch
        }).json";
        StateFile = Path.Combine(home, filename);
        if (!File.Exists(StateFile))
        {
            return;
        }

        var contents = File.ReadAllText(StateFile);
        var state = JsonSerializer.Deserialize<PersistedReviewState>(contents);
        if (state is null)
        {
            throw new InvalidOperationException(
                $"Unable to rehydrate state from {StateFile}"
            );
        }

        ValidateRehydratedState(options, state);

        _reviewStateItems.AddRange(
            state.Items.Select(o => new ReviewStateItem(this, o))
        );

        LastFile = state.LastFile;
        CanResume =
            _reviewStateItems.Any() &&
            state.Limit == _options.Limit &&
            state.Offset == _options.Offset &&
            state.IgnoreFiles == _options.IgnoreFiles;
        
        // we'll make a new one anyway - don't leave bad state lying about
        ClearCommentsFile();
    }

    private void ValidateRehydratedState(
        Options options,
        PersistedReviewState state
    )
    {
        if (state.Repo != options.Repo ||
            state.FromBranch != options.FromBranch ||
            state.ToBranch != options.ToBranch ||
            state.Repo != options.Repo)
        {
            throw new InvalidOperationException(
                $@"State at {StateFile} does not appear to be for the current conditions:
Wanted:
    Repo:           {options.Repo}
    From branch:    {options.FromBranch}
    To branch:      {options.ToBranch}
Found in {StateFile}:
    Repo:           {state.Repo}
    From branch:    {state.FromBranch}
    To branch:      {state.ToBranch}
".Trim()
            );
        }
    }

    public void Persist()
    {
        var state = new PersistedReviewState()
        {
            FromBranch = _options.FromBranch,
            ToBranch = _options.ToBranch,
            LastFile = LastFile,
            Items = _reviewStateItems.Cast<ReviewStateItemData>().ToArray(),
            IgnoreFiles = _options.IgnoreFiles,
            Repo = _options.Repo,
            Limit = _options.Limit,
            Offset = _options.Offset,
        };
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions()
        {
            WriteIndented = true
        });
        File.WriteAllText(StateFile, json);
    }

    public ReviewStateItem FindStateFor(string fileName)
    {
        LastFile = fileName;
        return _reviewStateItems.FindOrAdd(
            o => o.FileName == fileName,
            () => new ReviewStateItem(this, fileName)
        );
    }

    public string GenerateCommentary()
    {
        using var repo = new Repository(_options.Repo);
        var commitSha = repo.Head.Tip.Sha;
        return _reviewStateItems.Aggregate(
            new List<string>(),
            (acc, cur) =>
            {
                var comment = cur.Comments.Trim();
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    if (acc.Any())
                    {
                        acc.Add("");
                    }

                    acc.Add(GenerateLinkFor(cur, commitSha));
                    acc.Add(comment);
                }

                return acc;
            }).JoinWith(Environment.NewLine);
    }

    private string GenerateLinkFor(
        ReviewStateItem cur,
        string commitSha
    )
    {
        return $"[{cur.FileName}](../blob/{commitSha}/{cur.FileName})";
    }

    public void ClearAll()
    {
        ClearCommentsFile();
        ClearJsonState();
    }

    private void ClearJsonState()
    {
        DeleteFileIfExists(StateFile);
    }

    private void DeleteFileIfExists(string at)
    {
        if (!File.Exists(at))
        {
            return;
        }

        try
        {
            File.Delete(at);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to delete file: {at}: {ex.Message}");
        }
    }

    private void ClearCommentsFile()
    {
        DeleteFileIfExists(ReviewCommentsFile);
    }
}