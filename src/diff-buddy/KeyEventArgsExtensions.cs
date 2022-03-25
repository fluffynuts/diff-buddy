using System.Linq;
using Terminal.Gui;

namespace diff_buddy;

public static class KeyEventArgsExtensions
{
    public static bool Is(this View.KeyEventEventArgs eventArgs, params Key[] keys)
    {
        var eventKey = eventArgs.KeyEvent.Key;
        return keys.Aggregate(true, (acc, cur) => acc && (eventKey & cur) == cur);
    }
}