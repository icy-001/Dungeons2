using System.Globalization;
using System.Text.RegularExpressions;

namespace Dungeons2.Services;

internal readonly record struct GoogleSheetsStartCell(int Column, int Row)
{
    public string Address => ColumnName(Column) + Row.ToString(CultureInfo.InvariantCulture);
    public string DataRange => Address + ":" + ColumnName(Column);

    public static bool TryParse(string? text, out GoogleSheetsStartCell cell)
    {
        cell = default;
        var match = Regex.Match(text?.Trim() ?? "", @"^\$?([A-Za-z]{1,3})\$?([1-9][0-9]{0,7})$", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out int row) || row > 10_000_000) return false;
        int column = 0;
        foreach (char c in match.Groups[1].Value.ToUpperInvariant()) column = column * 26 + c - 'A' + 1;
        if (column > 18278) return false;
        if ((long)row * Math.Max(26, column) > 10_000_000) return false;
        cell = new(column, row);
        return true;
    }

    public static GoogleSheetsStartCell Parse(string? text) => TryParse(text, out var cell) ? cell
        : throw new InvalidOperationException("Enter a single starting cell such as A2 or C5. Do not include a tab name or range.");

    internal static string ColumnName(int column)
    {
        string name = "";
        for (; column > 0; column = (column - 1) / 26) name = (char)('A' + (column - 1) % 26) + name;
        return name;
    }
}
