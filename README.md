Diff Buddy!
---

This is a small utility to help with diffs on really large pull requests. To use:
1. Clone the repo you're looking at (add and fetch all remotes, if necessary)
2. Run diff-buddy - if you run from within the repo, it will default to diff against master
3. Filter down results iteratively at the command-line with subsequent runs until
    you end up seeing a useful changeset
4. Run in review mode with your prior commandline and `--review` to comment on files. 
    When a review is complete, your comments can be copied to the clipboard to add to the pull request you're 
    reviewing. Reviews can be resumed - just quit and say you're not done with the review. Next time,
    diff-buddy will attempt to pick up where you left off.

Quick-start
---
1. `npm ci`
2. Publish for your platform: `npm run publish-win32`
3. cd into the `bin` folder, or add it to your path
4. start using diff-buddy (:

Options
---

diff-buddy has help which can be invoked from the command-line with `--help`:

```
diff-buddy {args}

-a, --at [number]               Show the diff at the provided index only
-f, --from-branch [text]        Starting branch for the diff (master)
-h, --help                      shows this help
-I, --ignore-files [text]       Regular expressions for files to ignore completely
-i, --ignore-lines [text]       Regular expressions for lines to ignore when deciding on interesting files
    --ignore-whitespace         Ignore whitespace changes, even those spanning multiple lines, when deciding on interesting files
-l, --limit [number]            Limit the number of files to list (2147483647)
-o, --offset [number]           Start printing from this offset (0)
-r, --repo [text]               (.)
-S, --show-indexes              Show the numeric index for each file
    --show-operations           Show the operation which this file has undergone: (A)dded, (D)eleted, (M)odified, (R)enamed
-s, --show-patches              Whether or not to show patches for files
-t, --to-branch [text]          Later branch to diff with

Negate any flag argument with --no-{option}
```

(note that flags can be negated with `--no-`, eg `--no-show-indexes`)

Building filters
---

The whole point of diff-buddy is to reduce the number of files you need to look at to understand
a large pull request. Sometimes, a pull-request has many files which have changed but in an
insignificant way, eg because of a namespace rename or whitespace formatting.

The process, then, is as follows, for slimming down what would be interesting when
comparing some satellite branch in a local repo to master:
1. Check out the satellite branch of interest
2. Make sure you've got the latest: `git pull --rebase`
3. Start with a plain old `diff-buddy.exe` run in the repository to get a feel for the size of the diff
4. Scan for files which aren't of interest - perhaps you don't care about test files:
    - `diff-buddy --ignore-files Tests.*`
    - note that --ignore-files and --ignore-lines take regex strings: when working on unix shells like
        bash and zsh, you may need to surround these with single-quotes to prevent the shell from
        expanding them
5. Enable patches and start looking at smaller clumps of files:
    - `diff-buddy --ignore-files Tests.* --show-patches --limit 10 --offset 0`
    - increase the offset by 10 at a time and look for uninteresting patterns to exclude
6. Exclude files with uninteresting changes:
    - `diff-buddy --ignore-files Tests.* --ignore-lines ^using --show-patches --limit 10 --offset 10`
        - this ignores files where the only changes are to `using` statements, still
            only outputting 10 files, after an offset of 10
7. Add to the ignores for a few iterations until you can do
    - `diff-buddy --ignore-files {...} --ignore-lines {...}`
    - and end up with a small enough set of files that you are willing to review full patches for
8. Now run with your prior commandline and `--review` to see each file, one-by-one
    
