using System;
using diff_buddy;
using Pastel;
using PeanutButter.EasyArgs;

if (Console.IsOutputRedirected)
{
    ConsoleExtensions.Disable();
}

var options = args.ParseTo<Options>();

// sorry if you have quotes in your paths for real
// but if this isn't there, then `npm start` with
// tab-completion to a repo folder fails (at least,
// on win32)
options.Repo = options.Repo?
    .Trim('"', '\\') // trim out edging slashes & quotes
    .TrimEnd('/'); // just in case this ends up here on !win32 (leading slash is a-ok)

if (options.PageSize is not null)
{
    Environment.SetEnvironmentVariable("PAGE_SIZE", $"{options.PageSize}");
}

return options.Review
    ? Review.RunWith(options)
    : RunOnce.With(options, false);