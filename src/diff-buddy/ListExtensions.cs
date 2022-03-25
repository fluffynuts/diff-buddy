using System;
using System.Collections.Generic;
using System.Linq;

namespace diff_buddy;

public static class ListExtensions
{
    public static T FindOrAdd<T>(
        this IList<T> list,
        Func<T, bool> matcher,
        Func<T> factory
    )
    {
        var result = list.FirstOrDefault(matcher);
        if (result is null)
        {
            result = factory();
            list.Add(result);
        }

        return result;
    }
}