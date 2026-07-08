namespace Dungeons2
{
    public record FloorData(
        int Id = 0,
        DateTimeOffset Timestamp = new(),
        string Teammates = "",
        int FloorNumber = 1,
        string Size = "Small",
        string Time = "",
        double Bonus = 0,
        int MobsKilledMod = 0,
        int Boost = 0,
        int TotalMod = 0,
        int TotalXP = 0,
        int RoomCount = 0,
        int DeadEnds = 0,
        int SkillDoors = 0,
        int SkillDoorTicks = 0,
        int PossiblePuzzleRooms = 0,
        string Notes = "")
    {
        public string ToTabSeparatedString() => $"{Id}\t{Timestamp:G}\t{Teammates}\t{FloorNumber}\t{Size}\t{Time}\t{Bonus}\t{MobsKilledMod}\t{Boost}\t{TotalMod}\t{TotalXP}\t{RoomCount}\t{DeadEnds}\t{SkillDoors}\t{SkillDoorTicks}\t{PossiblePuzzleRooms}\t{Notes}\n";
    }
}
