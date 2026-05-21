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
        string Notes = "");
}
