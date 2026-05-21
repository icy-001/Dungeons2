namespace Dungeons2
{
    [Flags]
    public enum Direction : byte
    {
        None,
        W = 1,
        E = 2,
        S = 4,
        N = 8
    }
    public struct Room() : IEquatable<Room>
    {
        public bool Opened;
        public Direction Direction;
        public int Key = -1;
        public bool IsBase, IsBoss;

        public readonly bool Equals(Room other)
        {
            return Opened == other.Opened &&
            Direction == other.Direction &&
            Key == other.Key && IsBase == other.IsBase && IsBoss == other.IsBoss;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is Room other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Opened, Direction, Key, IsBase, IsBoss);
        }

        public static bool operator ==(Room left, Room right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Room left, Room right)
        {
            return !(left == right);
        }
    }
}
