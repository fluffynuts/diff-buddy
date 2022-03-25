using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace diff_buddy;

public class DiffTextView : TextView
{
    private static Terminal.Gui.Attribute _red = Driver.MakeAttribute(Color.Red, Color.Black);
    private static Terminal.Gui.Attribute _green = Driver.MakeAttribute(Color.Green, Color.Black);
    private static Terminal.Gui.Attribute _white = Driver.MakeAttribute(Color.White, Color.Black);

    public string Comments { get; set; }

    protected override void ColorNormal(List<Rune> line, int idx)
    {
        var attrib = ColorStrategies.First(pair => pair.Item1(line)).Item2;
        Driver.SetAttribute(attrib);
    }

    private static readonly ValueTuple<Func<List<Rune>, bool>, Terminal.Gui.Attribute>[] ColorStrategies =
    {
        (l => l.Count == 0, _white),
        (l => l[0] == '-', _red),
        (l => l[0] == '+', _green),
        (l => true, _white)
    };
}