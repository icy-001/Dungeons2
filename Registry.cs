using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics;

namespace Dungeons2
{
    public static class Registry
    {
        public const int KeybagKeyWidth = 18, KeybagKeyHeight = 16;
        public const int MapKeyWidth = 24, MapKeyHeight = 24;
        public const int HighlightedKeyWidth = 29, HighlightedKeyHeight = 29;
        public const int KeybagPadding = 2;
        public static readonly string[] Colors = ["o", "s", "y", "gr", "b", "p", "c", "go"];
        public static readonly string[] Shapes = ["t", "d", "r", "p", "co", "cr", "w", "s"];

        public static readonly uint[][] MapKeys = new uint[64][], KeybagKeys = new uint[64][], HighlightedKeys = new uint[64][];
        public static readonly BitmapSource[] RoomBitmaps = new BitmapSource[16];
        public static readonly uint[][] RoomSignatures = new uint[16][];
        public const int MapMarkerWidth = 15, MapMarkerHeight = 15;
        public static RawImage MapMarker { get; private set; } = new();
        public const int WinterfaceMarkerWidth = 56, WinterfaceMarkerHeight = 10;
        public static RawImage WinterfaceMarker { get; private set; } = new();
        public static RawImage WinterfaceReady { get; private set; } = new();

        public static readonly uint[][] PlayerDots = new uint[5][];
        public const int PlayerDotWidth = 9, PlayerDotHeight = 9;
        public static readonly PointInt32[] PlayerDotOffsets =
        [
            new(7, 2), new(21, 2), new(14, 9), new(7, 16), new(21, 16)
        ];

        private static readonly (string, int)[] skillDoorData = [
            ("Crafting", 2),
            ("Runecrafting", 4),
            ("Smithing", 4),
            ("Woodcutting", 4),
            ("Prayer", 5),
            ("Mining", 6),
            ("Magic", 7),
            ("Firemaking", 8),
            ("Strength", 8),
            ("Construction", 2),
            ("Herblore", 3),
            ("Invention", 3),
            ("Thieving", 3),
            ("Summoning", 4),
            ("Archaeology", 5),
            ("Farming", 5),
            ("Divination", 5),
            ("Agility", 7),
        ];

        public static readonly SkillDoor[] SkillDoors = new SkillDoor[skillDoorData.Length];

        static bool isInitialized;

        public static void Initialize()
        {
            if (isInitialized)
                return;

            uint[][] keys = SpriteLoader.LoadSpriteSheet("Dungeons2.Images.keys.png", 8, 8, 36, 32);
            Debug.Assert(keys.Length == 64);

            for (int i = 0; i < 64; ++i)
            {
                KeybagKeys[i] = ImageScaler.BilinearScale(keys[i], 36, 32, KeybagKeyWidth, KeybagKeyHeight);
                MapKeys[i] = ImageScaler.BilinearScale(keys[i], 36, 32, MapKeyWidth, MapKeyHeight, 1);
            }

            uint[][] rooms = SpriteLoader.LoadSpriteSheet("Dungeons2.Images.rooms.png", 1, 16, 32, 32);
            for (int i = 0; i < 16; ++i)
            {
                RoomBitmaps[i] = BitmapSource.Create(32, 32, 96, 96, PixelFormats.Bgra32, null, rooms[i], 32 * 4);
                RoomSignatures[i] = ComputeSignature(new(rooms[i], 32, 32));
            }

            MapMarker = SpriteLoader.LoadSprite("Dungeons2.Images.map_marker.png");
            WinterfaceMarker = SpriteLoader.LoadSprite("Dungeons2.Images.winterface_marker.png");
            WinterfaceReady = SpriteLoader.LoadSprite("Dungeons2.Images.winterface_ready.png");

            SpriteLoader.LoadSpriteSheet("Dungeons2.Images.player_dots.png", 1, 5, PlayerDotWidth, PlayerDotHeight).CopyTo(PlayerDots, 0);
            SpriteLoader.LoadSpriteSheet("Dungeons2.Images.highlighted_keys.png", 8, 8, HighlightedKeyWidth, HighlightedKeyHeight).CopyTo(HighlightedKeys, 0);
            uint[][] skillIcons = SpriteLoader.LoadSpriteSheet("Dungeons2.Images.skills.png", 1, 18, HighlightedKeyWidth, HighlightedKeyHeight);
            for (int i = 0; i < skillDoorData.Length; ++i)
                SkillDoors[i] = new(skillDoorData[i].Item1, skillDoorData[i].Item2, new(skillIcons[i], HighlightedKeyWidth, HighlightedKeyHeight));

            isInitialized = true;
        }

        public static string GetKeyName(int index) => index < 0 ? "none" : Colors[index / 8] + Shapes[index % 8];

        public static RawImageView GetKeybagKey(int index) => new(KeybagKeys[index], KeybagKeyWidth, KeybagKeyHeight);

        public static RawImageView GetMapKey(int index) => new(MapKeys[index], MapKeyWidth, MapKeyHeight);

        public static RawImageView GetHighlightedMapKey(int index) => new(HighlightedKeys[index], HighlightedKeyWidth, HighlightedKeyHeight);

        public static RawImage GetSkillIcon(int index) => SkillDoors[index].Image;

        public static RawImageView GetPlayerDot(int index) => new(PlayerDots[index], PlayerDotWidth, PlayerDotHeight);

        public static uint[] ComputeSignature(RawImageView image, int offX = 0, int offY = 0)
        {
            return
            [
                image[offX + 6, offY + 7],
                image[offX + 7, offY + 7],
                image[offX + 6, offY + 8],
                image[offX + 7, offY + 8]
            ];
        }
    }
}