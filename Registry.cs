using System.Diagnostics;

namespace Dungeons2
{
    public static class Registry
    {
        public const int KeybagKeyWidth = 18, KeybagKeyHeight = 16;
        public const int MapKeyWidth = 24, MapKeyHeight = 24;
        public const int KeybagPadding = 2;
        public static readonly string[] Colors = ["o", "s", "y", "gr", "b", "p", "c", "go"];
        public static readonly string[] Shapes = ["t", "d", "r", "p", "co", "cr", "w", "s"];

        public static readonly uint[][] MapKeys = new uint[64][], KeybagKeys = new uint[64][];
        public static readonly uint[][] RoomSignatures = new uint[16][];
        public const int MapMarkerWidth = 15, MapMarkerHeight = 15;
        public static RawImage MapMarker { get; private set; } = new();
        public const int WinterfaceMarkerWidth = 56, WinterfaceMarkerHeight = 10;
        public static RawImage WinterfaceMarker { get; private set; } = new();
        public static RawImage WinterfaceReady { get; private set; } = new();

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
                RoomSignatures[i] = ComputeSignature(new(rooms[i], 32, 32));

            MapMarker = SpriteLoader.LoadSprite("Dungeons2.Images.map_marker.png");
            WinterfaceMarker = SpriteLoader.LoadSprite("Dungeons2.Images.winterface_marker.png");
            WinterfaceReady = SpriteLoader.LoadSprite("Dungeons2.Images.winterface_ready.png");

            isInitialized = true;
        }

        public static string GetKeyName(int index)
        {
            return Colors[index / 8] + Shapes[index % 8];
        }

        public static RawImageView GetKeybagKey(int index)
        {
            return new(KeybagKeys[index], KeybagKeyWidth, KeybagKeyHeight);
        }

        public static RawImageView GetMapKey(int index)
        {
            return new(MapKeys[index], MapKeyWidth, MapKeyHeight);
        }

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