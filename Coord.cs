namespace Dungeons2
{
    public readonly record struct Coord(int Row, int Col)
    {
        public static readonly Coord Nothing = new(-1, -1);
        public string ToChessString() => $"{(char)('a' + Col)}{8 - Row}";
    }
}
