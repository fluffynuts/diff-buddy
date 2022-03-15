using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PeanutButter.EasyArgs;
using PeanutButter.Utils;

namespace diff_buddy;

public static class Review
{
    public static int RunWith(Options options)
    {
        options.Review = false;
        if (options.Repo == ".")
        {
            options.Repo = Environment.CurrentDirectory;
        }

        var cmd = Environment.CommandLine;
        var splitCmd = new Queue<string>(cmd.Split(' '));
        var exe = splitCmd.Dequeue().Replace(".dll", ".exe");
        while (splitCmd.Count > 0 && exe.Count(c => c == '"') % 2 != 0)
        {
            exe = $"{exe} {splitCmd.Dequeue()}";
        }

        exe = exe.Trim('"');

        options.CountOnly = true;
        var pages = -1;
        using (var io = ProcessIO.Start(exe, options.GenerateArgs().AsQuotedArgs()))
        {
            var result = io.StandardOutput.FirstOrDefault();
            if (result is null || !int.TryParse(result, out pages))
            {
                Console.WriteLine($"Unable to count changes in {options.Repo}");
                return 1;
            }
        }

        options.CountOnly = false;

        for (var i = 1; i < pages; i++)
        {
            Console.Clear();
            options.At = i;
            
            RunOnce.With(options, true);

            Console.Write($"[{i}/{pages}] Press any key to continue, q to quit...");
            var c = Console.ReadKey();
            if (c.KeyChar == 'q')
            {
                return 0;
            }
        }

        return 0;
    }
}