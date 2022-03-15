using System;
using diff_buddy;
using Pastel;
using PeanutButter.EasyArgs;

if (Console.IsOutputRedirected)
{
    ConsoleExtensions.Disable();
}

var options = args.ParseTo<Options>();

return options.Review
    ? Review.RunWith(options)
    : RunOnce.With(options, false);
