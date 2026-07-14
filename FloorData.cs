using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    public class FloorData
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false
        };

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
        public int KeyCount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public Map? Map { get; set; }
        [JsonIgnore]
        public WriteableBitmap? MapImage { get; set; }
        public RoomSplit[] RoomTicks { get; set; } = [];

        public string ToTabSeparatedString() => $"{Id}\t{Timestamp:G}\t{Teammates}\t{FloorNumber}\t{Size}\t{Time}\t{Bonus}\t{MobsKilledMod}\t{Boost}\t{TotalMod}\t{TotalXP}\t{RoomCount}\t{DeadEnds}\t{SkillDoors}\t{SkillDoorTicks}\t{PossiblePuzzleRooms}\t{Notes}\n";

        public string ToJsonString() => JsonSerializer.Serialize(this, JsonOptions);
    }
}
