using System.Drawing;

namespace Dungeons2
{
    /// <summary>
    /// A struct that reads map and HUD from a <href>RawImageView</href>.
    /// </summary>
    /// <param name="img">
    /// Consists of 2 parts.
    /// - Map is 280 x 280 starting at (0, 0). 
    /// - HUD is is 117 x 154 starting at (0, 280). The keybag portion of the HUD starts at (5, 332).
    /// </param>
    public ref struct MapHudReader(RawImageView img)
    {
        public const int MapWidth = 280, MapHeight = 280;
        public const int KeybagOffsetX = 5, KeybagOffsetY = 52;
        public const int HudStartY = MapHeight;
        public const int KeybagPadding = 2;
        public const double MatchThreshold = 200;
        public static readonly Color MapCornerColorMin = Color.FromArgb(100, 87, 65);
        public static readonly Color MapCornerColorMax = Color.FromArgb(117, 104, 83);
        public static readonly Color MapTopRightCornerColor = Color.FromArgb(122, 52, 44);
        public static readonly Color KeybagBorderColor = Color.FromArgb(93, 46, 21);
        public static readonly uint BaseColor = (uint)Color.FromArgb(150, 145, 105).ToArgb();
        public static readonly uint BossColor = (uint)Color.FromArgb(132, 57, 46).ToArgb();

        public readonly RawImageView Image = img;
        static Registry Registry => Registry.Instance;

        // Color utils
        public static bool ColorBetween(Color color, Color min, Color max)
        {
            return color.R >= min.R && color.R <= max.R &&
                color.G >= min.G && color.G <= max.G &&
                color.B >= min.B && color.B <= max.B;
        }

        public readonly bool HasMap() =>
            // Check for X. On Large, this is (265, 0). On medium and small, this is (137, 0).
            ColorBetween(Color.FromArgb((int)Image[0, 0]), MapCornerColorMin, MapCornerColorMax) &&
            (Image.IsExactMatch(Registry.MapMarker, 265, 0) || Image.IsExactMatch(Registry.MapMarker, 137, 0));

        public readonly bool HasHud() => Image.IsExactMatch(Registry.HudMarker, 0, HudStartY);

        public readonly ulong ReadKeybag()
        {
            ulong mask = 0;
            for (int i = 0; i < 4; ++i)
                for (int j = 0; j < 5; ++j)
                {
                    bool found = false;
                    int x = KeybagOffsetX + 2 + 22 * j, y = HudStartY + KeybagOffsetY + 3 + 20 * i;
                    for (int k = 0; k < 64; ++k)
                    {
                        double dist = Image.MatchDistance(Registry.GetKeybagKey(k), x, y, MatchThreshold);
                        if (dist < MatchThreshold)
                        {
                            mask |= 1UL << k;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        return mask;
                }
            return mask;
        }

        public readonly (Map, int) ReadMap()
        {
            Map res = new();
            int roomcount = 0;
            for (int x = 0; x < res.Rooms.GetLength(0); x++)
                for (int y = 0; y < res.Rooms.GetLength(1); y++)
                    res.Rooms[x, y] = new();
            for (int i = 0; i < 8; ++i)
                for (int j = 0; j < 8; ++j)
                {
                    ref Room currentRoom = ref res.Rooms[i, j];

                    int x = 12 + 32 * j, y = 12 + 32 * i;
                    uint[] sig = Registry.ComputeSignature(Image, x, y);

                    for (int k = 0; k < Registry.RoomSignatures.Length; ++k)
                    {
                        if (Enumerable.SequenceEqual(sig, Registry.RoomSignatures[k]))
                        {
                            currentRoom.Doors = (Directions)k;
                            currentRoom.Opened = true;
                            ++roomcount;

                            if (Image[x + 22, y + 13] == BaseColor)
                            {
                                currentRoom.IsBase = true;
                                res.Base = new(i, j);
                            }
                            else if (Image[x + 21, y + 11] == BossColor)
                            {
                                currentRoom.IsBoss = true;
                                res.Boss = new(i, j);
                            }
                            break;
                        }
                    }

                    if (!currentRoom.Opened)
                    {
                        for (int k = 0; k < 64; ++k)
                        {
                            double dist = Image.MatchDistance(Registry.GetMapKey(k), x + 3, y + 6, MatchThreshold);
                            if (dist < MatchThreshold)
                            {
                                currentRoom.Lock = k;
                                break;
                            }

                            dist = Image.MatchDistance(Registry.GetHighlightedMapKey(k), x + 1, y + 2, MatchThreshold);
                            if (dist < MatchThreshold)
                            {
                                currentRoom.Lock = k;
                                break;
                            }
                        }

                        if (currentRoom.Lock == -1)
                        {
                            for (int k = 0; k < Registry.SkillDoors.Length; ++k)
                            {
                                double dist = Image.MatchDistance(Registry.GetSkillIcon(k), x + 1, y + 2, MatchThreshold);
                                if (dist < MatchThreshold)
                                {
                                    currentRoom.SkillDoor = k;
                                    break;
                                }
                            }
                        }
                    }

                    for (int p = 0; p < 5; ++p)
                        if (Image.IsExactMatch(Registry.GetPlayerDot(p), x + Registry.PlayerDotOffsets[p].X, y + Registry.PlayerDotOffsets[p].Y))
                            res.PlayerLocations[p] = new(i, j);
                }

            res.AssignParents();
            return (res, roomcount);
        }
    }
}
