namespace Dungeons2
{
    [Flags]
    public enum Directions : byte
    {
        None,
        W = 1,
        E = 2,
        S = 4,
        N = 8
    }
}
