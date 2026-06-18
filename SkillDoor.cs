namespace Dungeons2
{
    public readonly struct SkillDoor(string name, int ticks, RawImage image)
    {
        public string Name { get; } = name;
        public int Ticks { get; } = ticks;
        public RawImage Image { get; } = image;
    }
}
