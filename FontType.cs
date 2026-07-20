namespace Dungeons2
{
    public readonly record struct FontCharacter(char Character, RawImage Image);

    public class FontType
    {
        public static FontType BaseFont { get; private set; } = new();
        public static FontType SmallFont { get; private set; } = new();
        public static FontType LargeFont { get; private set; } = new();

        public List<FontCharacter> Characters { get; set; } = [];
        public int Height => Characters[0].Image.Height;

        static FontType()
        {
            BaseFont.Characters = [.. GetNumberList("Base"),
                new(':', SpriteLoader.LoadSprite("Dungeons2.Fonts.Base.wide_colon.png"))];
            SmallFont.Characters = [.. GetNumberList("Small"),
                new('+', SpriteLoader.LoadSprite("Dungeons2.Fonts.Small.plus.png")),
                new('-', SpriteLoader.LoadSprite("Dungeons2.Fonts.Small.minus.png")),
                new(':', SpriteLoader.LoadSprite("Dungeons2.Fonts.Small.colon.png"))];
            LargeFont.Characters = [.. GetNumberList("Large"),
                new(',', SpriteLoader.LoadSprite("Dungeons2.Fonts.Large.comma.png"))];
        }

        static IEnumerable<FontCharacter> GetNumberList(string prefix)
        {
            return from i in Enumerable.Range(0, 10)
                   select new FontCharacter((char)('0' + i), SpriteLoader.LoadSprite($"Dungeons2.Fonts.{prefix}.{i}.png"));
        }
    }
}
