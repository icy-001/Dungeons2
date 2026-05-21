namespace Dungeons2
{
    public class FontType
    {
        public static FontType BaseFont { get; private set; } = new();
        public static FontType SmallFont { get; private set; } = new();
        public static FontType LargeFont { get; private set; } = new();

        public List<RawImage> NumberImageList { get; set; } = [];
        public int Height => NumberImageList[0].Height;

        public static void InitializeFonts()
        {
            BaseFont.NumberImageList = [.. GetNumberList("Base")];
            SmallFont.NumberImageList = [.. GetNumberList("Small"),
                SpriteLoader.LoadSprite($"Dungeons2.Fonts.Small.plus.png"),
                SpriteLoader.LoadSprite($"Dungeons2.Fonts.Small.minus.png"),
                SpriteLoader.LoadSprite($"Dungeons2.Fonts.Small.colon.png")];
            LargeFont.NumberImageList = [.. GetNumberList("Large"),
                SpriteLoader.LoadSprite($"Dungeons2.Fonts.Large.comma.png")];
        }

        static IEnumerable<RawImage> GetNumberList(string prefix)
        {
            return from i in Enumerable.Range(0, 10)
                   select SpriteLoader.LoadSprite($"Dungeons2.Fonts.{prefix}.{i}.png");
        }
    }
}
