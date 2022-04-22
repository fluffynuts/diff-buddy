using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PeanutButter.EasyArgs;
using PeanutButter.Utils;
using Terminal.Gui;
using TextCopy;
using Attribute = Terminal.Gui.Attribute;

namespace diff_buddy;

public static class Review
{
    public static int RunWith(Options options)
    {
        options.Review = false;
        var originalOffset = options.Offset;
        var originalLimit = options.Limit;
        options.Offset = 0;
        options.Limit = int.MaxValue;
        if (options.Repo == ".")
        {
            options.Repo = Environment.CurrentDirectory;
        }

        options.SetCurrentBranchAsToBranchIfNotSet();

        var reviewState = new ReviewState(options);

        var entries = CountChanges(options);
        if (entries < 0)
        {
            return entries;
        }

        var exe = FindExeForCurrentProcess();

        var start = originalOffset + 1;
        var limit = originalLimit == int.MaxValue
            ? originalLimit
            : originalLimit + start;
        var end = Math.Min(entries, limit);

        RunReviewsPerFile(options, start, end, exe, reviewState);

        HandleReviewCompleted(reviewState);
        return 0;
    }

    private static int CountChanges(Options opts)
    {
        var options = opts.DeepClone();
        var exe = FindExeForCurrentProcess();

        options.CountOnly = true;
        Console.WriteLine("Please wait... counting relevant changes...");
        using var io = ProcessIO.Start(exe, options.GenerateArgs().AsQuotedArgs());
        var stdout = io.StandardOutput.ToArray();
        var result = stdout.FirstOrDefault();
        if (result is not null && int.TryParse(result, out var entries))
        {
            return entries;
        }
        

        Console.WriteLine($"Unable to count changes in {options.Repo}");
        Console.WriteLine($"stdout: {stdout.JoinWith("\n")}");
        var stderr = io.StandardError.ToArray();
        Console.WriteLine($"stderr: {stderr.JoinWith("\n")}");
        return -1;
    }

    private static string _exe;

    private static string FindExeForCurrentProcess()
    {
        if (_exe is not null)
        {
            return _exe;
        }

        var cmd = Environment.CommandLine;
        var splitCmd = new Queue<string>(cmd.Split(' '));
        var exe = splitCmd.Dequeue().Replace(".dll", ".exe");
        while (splitCmd.Count > 0 && exe.Count(c => c == '"') % 2 != 0)
        {
            exe = $"{exe} {splitCmd.Dequeue()}";
        }

        return _exe = exe.Trim('"');
    }

    private static void RunReviewsPerFile(
        Options options,
        int start,
        int end,
        string exe,
        ReviewState reviewState
    )
    {
        var seekToLastFile = reviewState.CanResume && reviewState.LastFile is not null;
        var foundLastFile = false;
        if (LookingForLastFile())
        {
            Console.Write($"Seeking to last position: {reviewState.LastFile} ");
        }

        for (var i = start; i < end; i++)
        {
            options.At = i;

            using var io = ProcessIO.Start(
                exe, options.GenerateArgs().AsQuotedArgs()
            );
            var lines = io.StandardOutput.ToArray();
            var fileName = GrokFileNameFrom(lines.First());

            if (LookingForLastFile())
            {
                foundLastFile = fileName == reviewState.LastFile;
                if (!foundLastFile)
                {
                    Console.Write(".");
                    continue;
                }
            }

            Console.Clear();

            var reviewStateItem = reviewState.FindStateFor(fileName);
            var reviewResult = LaunchReviewUiWith(
                reviewStateItem,
                lines
            );
            if (reviewResult == ReviewResults.GoBack)
            {
                i -= 2;
            }

            if (reviewResult == ReviewResults.Exit)
            {
                break;
            }
        }

        bool LookingForLastFile()
        {
            return seekToLastFile && !foundLastFile;
        }
    }

    private static void HandleReviewCompleted(ReviewState reviewState)
    {
        Console.Clear();
        if (!Ask("Are you done with this review?"))
        {
            Console.WriteLine($@"
Review state persisted to {reviewState.StateFile.BrightGreen()}

{"You will pick up from where you left off from next time you start a review".Rainbow()}
");
            return;
        }

        var comment = reviewState.GenerateCommentary();
        // store this review temporarily until the user says she's done
        File.WriteAllText(reviewState.ReviewCommentsFile, comment);
        ClipboardService.SetText(comment);
        Console.WriteLine($@"

{"Commentary copied to clipboard and also stored at".BrightCyan()}
  {reviewState.ReviewCommentsFile.BrightGreen()}
{@"Now you should browse to the pull request and paste in the commentary,
and afterwards come back here to confirm removal of review state files.".BrightBlue()}

");
        if (!Ask($"Delete all stored review state?"))
        {
            Console.WriteLine($@"

{"The following files are left on disk:".White()}
- {reviewState.ReviewCommentsFile.BrightRed()}
- {reviewState.StateFile.BrightRed()}

{"You should clean them up manually if required.".BrightPink()}

");
            return;
        }

        reviewState.ClearAll();
    }

    private static bool Ask(string question)
    {
        Console.Write($"{question.BrightYellow()} [{"y".Grey()}/{"N".BrightYellow()}] ");
        var answer = Console.ReadKey();
        var result = answer.Key is ConsoleKey.Y;
        Console.WriteLine("");
        return result;
    }

    private static string GrokFileNameFrom(string first)
    {
        // first line should be something like [123]   M   Foo/Bar/Quux.cs
        var match = FileNameMatcher.Match(first);
        if (!match.Success)
        {
            throw new ArgumentException($"Unable to determine file name from line: '{first}'");
        }

        return match.Groups[^1].Value;
    }

    private static readonly Regex FileNameMatcher = new Regex("(\\S+)\\s+(\\S+)\\s+(\\S+)");

    private static Window CreateWindow()
    {
        return new Window("Changes: ")
        {
            Height = Dim.Fill(1),
            ColorScheme = new ColorScheme()
            {
                Normal = Attribute.Make(Color.White, Color.Black),
                Disabled = Attribute.Make(Color.Gray, Color.Black),
                Focus = Attribute.Make(Color.White, Color.Black),
                HotFocus = Attribute.Make(Color.BrightYellow, Color.Black),
                HotNormal = Attribute.Make(Color.White, Color.Black)
            }
        };
    }

    private static (FrameView leftFrame, FrameView rightFrame) CreateFrames(
        Window win
    )
    {
        var leftFrame = new FrameView()
        {
            Width = Dim.Percent(65, true),
            Height = Dim.Fill()
        };
        var rightFrame = new FrameView()
        {
            X = Pos.Right(leftFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        win.Add(leftFrame, rightFrame);
        return (leftFrame, rightFrame);
    }

    private static void AddDiffView(
        View parent,
        string[] lines)
    {
        parent.Add(new DiffTextView()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = string.Join("\n", lines),
            ReadOnly = true
        });
    }

    private static ReviewResults LaunchReviewUiWith(
        ReviewStateItem reviewStateItem,
        string[] lines
    )
    {
        Application.Init();
        Application.QuitKey = Key.CtrlMask | Key.AltMask | Key.ShiftMask | Key.Q;
        var top = Application.Top;
        var win = CreateWindow();

        var (leftFrame, rightFrame) = CreateFrames(win);
        
        AddDiffView(leftFrame, lines);

        var commentArea = new TextView()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = reviewStateItem.Comments,
            WordWrap = true,
            AllowsTab = true,
            Multiline = true,
            TabWidth = 2
        };
        rightFrame.Add(commentArea);

        top.Add(win);

        CreateStatusBar(top, MovePrevious, MoveNext, Quit);
        var result = ReviewResults.GoNext;

        commentArea.SetFocus();
        Application.Run(top);
        Application.Shutdown();

        return result;

        void MoveNext()
        {
            result = ReviewResults.GoNext;
            RequestStop();
        }

        void MovePrevious()
        {
            result = ReviewResults.GoBack;
            RequestStop();
        }

        void Quit()
        {
            result = ReviewResults.Exit;
            RequestStop();
        }

        void RequestStop()
        {
            reviewStateItem.Comments = commentArea.Text.ToString();
            reviewStateItem.Persist();
            Application.RequestStop();
        }
    }

    private static void CreateStatusBar(
        View top,
        Action movePrevious,
        Action moveNext,
        Action quit
    )
    {
        var statusBar = new StatusBar(
            new[]
            {
                new StatusItem(Key.CtrlMask | Key.P, "Ctrl-P Previous", movePrevious),
                new StatusItem(Key.CtrlMask | Key.N, "Ctrl-N Next", moveNext),
                new StatusItem(Key.CtrlMask | Key.Q, "Ctrl-Q Quit", quit)
            });
        top.Add(statusBar);
    }
}