namespace Dungeons2
{
    public readonly record struct Glyph(char Character, RawImage Image);

    public class FontType
    {
        public static FontType BaseFont { get; private set; } = new();
        public static FontType SmallFont { get; private set; } = new();
        public static FontType LargeFont { get; private set; } = new();

        public List<Glyph> Glyphs { get; set; } = [];
        public int Height => Glyphs[0].Image.Height;

        static FontType()
        {
            BaseFont.Glyphs = [.. GetNumberList("Base"),
                new(':', SpriteLoader.LoadSprite("Dungeons2.Fonts.Base.wide_colon.png"))];
            SmallFont.Glyphs = [.. GetNumberList("Small"),
                new('+', SpriteLoader.LoadSprite("Dungeons2.Fonts.Small.plus.png")),
                new('-', SpriteLoader.LoadSprite("Dungeons2.Fonts.Small.minus.png")),
                new(':', SpriteLoader.LoadSprite("Dungeons2.Fonts.Small.colon.png"))];
            LargeFont.Glyphs = [.. GetNumberList("Large"),
                new(',', SpriteLoader.LoadSprite("Dungeons2.Fonts.Large.comma.png"))];
        }

        static IEnumerable<Glyph> GetNumberList(string prefix)
        {
            return from i in Enumerable.Range(0, 10)
                   select new Glyph((char)('0' + i), SpriteLoader.LoadSprite($"Dungeons2.Fonts.{prefix}.{i}.png"));
        }
    }
}
