using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    public partial class FloorData : ObservableObject
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false
        };

        public FloorData()
        {
            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        public int Id { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public int PartySize { get; set; }
        public string Teammates { get; set; } = string.Empty;
        public int FloorNumber { get; set; } = 1;
        public TimeSpan Time { get; set; }
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
        public RoomSplit[] RoomTicks { get; set; } = [];
        public bool IsPartial { get; set; }


        [JsonIgnore]
        public WriteableBitmap? MapImage { get; set; }
        [JsonIgnore]
        public int CriticalRoomCount => Map?.ComputeCriticalStats().Count ?? 0;
        [JsonIgnore]
        public int CriticalKeyCount => Map?.ComputeCriticalStats().KeyCount ?? 0;
        [JsonIgnore]
        public ExtendedDeadEnds DeadEndCountExtended => Map?.DeadEndCountExtended() ?? new();
        [JsonIgnore]
        public int Depth => Map?.ComputeDepth() ?? 0;
        [JsonIgnore]
        public double Rpm => Time.TotalMinutes == 0 ? 0.0 : (double)RoomCount / Time.TotalMinutes;

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Settings.ShowKeydoors)
                or nameof(Settings.ShowKeyLocations)
                or nameof(Settings.HideTrivialKeyblocks)
                or nameof(Settings.ShowRoomTicks))
            {
                OnPropertyChanged(nameof(AnnotatedMapImage));
            }
        }

        [JsonIgnore]
        public RenderTargetBitmap AnnotatedMapImage
        {
            get
            {
                RenderTargetBitmap res = new(MapHudReader.MapWidth, MapHudReader.MapHeight, 96, 96, PixelFormats.Pbgra32);
                if (Map is null)
                    return res;
                const int offX = 12, offY = 12;
                DrawingVisual visual = new();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.Black, null, new(0, 0, MapHudReader.MapWidth, MapHudReader.MapHeight));
                    AnnotatedMapRenderer.DrawMap(dc, offX, offY, Map);
                    AnnotatedMapRenderer.DrawRoomTicks(dc, offX, offY, RoomTicks);
                }
                res.Render(visual);
                return res;
            }
        }

        [JsonIgnore]
        public string PartySizeLabel => PartySize switch
        {
            1 => "Solo",
            2 => "Duo",
            3 => "Trio",
            4 => "4-man",
            5 => "5-man",
            _ => "Unknown"
        };

        [JsonIgnore]
        public bool IsSolo => PartySize == 1;

        public string ToTabSeparatedString() => $"{Id}\t{Timestamp:G}\t{Teammates}\t{FloorNumber}\t{Size}\t{Time}\t{Bonus}\t{MobsKilledMod}\t{Boost}\t{TotalMod}\t{TotalXP}\t{RoomCount}\t{DeadEnds}\t{SkillDoors}\t{SkillDoorTicks}\t{PossiblePuzzleRooms}\t{Notes}\n";

        public string ToJsonString() => JsonSerializer.Serialize(this, JsonOptions);
    }
}
