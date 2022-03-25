using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using PeanutButter.EasyArgs;
using PeanutButter.Utils;
using Terminal.Gui;
using TextCopy;
using Attribute = Terminal.Gui.Attribute;

namespace diff_buddy;

public static class Review
{
    private static int CountChanges(Options opts)
    {
        var options = opts.DeepClone();
        var exe = FindExeForCurrentProcess();

        options.CountOnly = true;
        Console.WriteLine("Please wait... counting relevant changes...");
        using var io = ProcessIO.Start(exe, options.GenerateArgs().AsQuotedArgs());
        var result = io.StandardOutput.FirstOrDefault();
        if (result is not null && int.TryParse(result, out var entries))
        {
            return entries;
        }

        Console.WriteLine($"Unable to count changes in {options.Repo}");
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
        var seekToLastFile = reviewState.CanResume && reviewState.LastFile is not null;
        var foundLastFile = false;
        if (LookingForLastFile())
        {
            Console.WriteLine($"Seeking to last position: {reviewState.LastFile}");
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

        HandleReviewCompleted();
        return 0;

        bool LookingForLastFile()
        {
            return seekToLastFile && !foundLastFile;
        }

        void HandleReviewCompleted()
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

    private static ReviewResults LaunchReviewUiWith(
        ReviewStateItem reviewStateItem,
        string[] lines
    )
    {
        Application.Init();
        Application.QuitKey = Key.CtrlMask | Key.AltMask | Key.ShiftMask | Key.Q;
        var top = Application.Top;
        var win = new Window("Changes: ");
        win.ColorScheme = new ColorScheme()
        {
            Normal = Attribute.Make(Color.White, Color.Black),
            Disabled = Attribute.Make(Color.Gray, Color.Black),
            Focus = Attribute.Make(Color.White, Color.Black),
            HotFocus = Attribute.Make(Color.BrightYellow, Color.Black),
            HotNormal = Attribute.Make(Color.White, Color.Black)
        };
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

        var diffView = new DiffTextView()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = string.Join("\n", lines),
            ReadOnly = true
        };

        leftFrame.Add(diffView);

        var commentArea = new TextView()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = reviewStateItem.Comments,
            WordWrap = true,
            CursorPosition = PositionFor(reviewStateItem.Comments)
        };
        rightFrame.Add(commentArea);

        top.Add(win);
        var result = ReviewResults.GoNext;
        win.KeyPress += args =>
        {
            var handled = args.Is(Key.AltMask, Key.CursorRight);

            if (args.Is(Key.AltMask, Key.CursorLeft))
            {
                handled = true;
                result = ReviewResults.GoBack;
            }


            if (args.Is(Key.CtrlMask, Key.Q) ||
                args.Is(Key.AltMask, Key.Q))
            {
                result = ReviewResults.Exit;
                handled = true;
            }

            if (handled)
            {
                args.Handled = true;
                reviewStateItem.Comments = commentArea.Text.ToString();
                reviewStateItem.Persist();
                Application.RequestStop();
            }
        };
        
        commentArea.SetFocus();
        Application.Run(win);
        Application.Shutdown();

        return result;
    }

    private static Point PositionFor(string comments)
    {
        var lines = comments.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var lastLine = lines.Length - 1;
        var lastChar = lines[lastLine].Length - 1;
        return new Point(lastLine, lastChar + 1);
    }
}

public enum ReviewResults
{
    None,
    GoBack,
    GoNext,
    Exit,
}

public static class KeyEventArgsExtensions
{
    public static bool Is(this View.KeyEventEventArgs eventArgs, params Key[] keys)
    {
        var eventKey = eventArgs.KeyEvent.Key;
        return keys.Aggregate(true, (acc, cur) => acc && (eventKey & cur) == cur);
    }
}