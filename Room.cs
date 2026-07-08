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

    public record struct Room(
        bool Opened = false,
        Directions Doors = Directions.None,
        Directions Parent = Directions.None,
        int Lock = -1,
        int Key = -1,
        int SkillDoor = -1,
        bool IsBase = false,
        bool IsBoss = false)
    {
        public Room() : this(Lock: -1, Key: -1, SkillDoor: -1) { }

        public readonly bool IsDeadEnd => Doors != Directions.None && Doors == Parent && !IsBase && !IsBoss;

        /// <summary>
        /// Merges this room with another and returns true if there was a change. Does not change any attributes if the room is in the Opened state. This is useful for collecting stats on keydoors and skilldoors.
        /// </summary>
        /// <param name="other">The other room.</param>
        /// <returns>True if there was a change; otherwise, false.</returns>
        public bool Merge(Room other)
        {
            if (Opened)
                return false;
            bool changed = false;
            if (Opened != other.Opened)
            {
                Opened = other.Opened;
                changed = true;
            }
            if (Doors != other.Doors)
            {
                Doors = other.Doors;
                changed = true;
            }
            if (Parent != other.Parent)
            {
                Parent = other.Parent;
                changed = true;
            }
            if (Lock == -1 && Lock != other.Lock)
            {
                Lock = other.Lock;
                changed = true;
            }
            if (Key == -1 && Key != other.Key)
            {
                Key = other.Key;
                changed = true;
            }
            if (SkillDoor == -1 && SkillDoor != other.SkillDoor)
            {
                SkillDoor = other.SkillDoor;
                changed = true;
            }
            if (IsBase != other.IsBase)
            {
                IsBase |= other.IsBase;
                changed = true;
            }
            if (IsBoss != other.IsBoss)
            {
                IsBoss |= other.IsBoss;
                changed = true;
            }
            return changed;
        }
    }
}
