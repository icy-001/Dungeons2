namespace Dungeons2
{
    public class FloorData
    {
        public int Id { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public int TeamSize { get; set; } = 1;
        public string Teammates { get; set; } = string.Empty;
        public int FloorNumber { get; set; } = 1;
        public TimeSpan Time { get; set; } = TimeSpan.Zero;
        public string Size { get; set; } = "Small";
        public double Bonus { get; set; }
        public int MobsKilledMod { get; set; }
        public int Boost { get; set; }
        public int TotalMod { get; set; }
        public int TotalXP { get; set; }
        public int RoomCount { get; set; }
        public int DeadEnds { get; set; }
        public int SkillDoors { get; set; }
        public int SkillDoorTicks { get; set; }
        public int PossiblePuzzleRooms { get; set; }
        public string Notes { get; set; } = string.Empty;

        public string ToTabSeparatedString() => $"{Id}\t{Timestamp:G}\t{Teammates}\t{FloorNumber}\t{Size}\t{Time}\t{Bonus}\t{MobsKilledMod}\t{Boost}\t{TotalMod}\t{TotalXP}\t{RoomCount}\t{DeadEnds}\t{SkillDoors}\t{SkillDoorTicks}\t{PossiblePuzzleRooms}\t{Notes}\n";
    }
}
