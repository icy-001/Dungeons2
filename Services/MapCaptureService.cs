using System.IO;
using System.Text;

namespace Dungeons2.Services
{
    public class MapCaptureService
    {
        public static MapCaptureService Instance { get; } = new();

        public event Action<FloorData>? OnFloorCompleted;
        public event Action? OnNewFloor;
        public event Action? OnMapChanged;

        private readonly CancellationTokenSource _cts = new();
        private DateTimeOffset _lastProcessedFloorTime = DateTimeOffset.MinValue;

        public IntPtr TargetWindowHandle
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    GameCapture.SetTarget(new((ulong)value));

                    DungeonStore.KeyMask = 0;
                    DungeonStore.IsActive = false;
                }
            }
        }

        static GameCapture GameCapture => GameCapture.Instance;
        static DungeonStore DungeonStore => DungeonStore.Instance;

        public async void Start()
        {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(50));

            try
            {
                while (await timer.WaitForNextTickAsync(_cts.Token))
                    DoCapture();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                File.AppendAllText("errors.log", $"[{DateTimeOffset.Now:G}] {e.Message}\n");
                throw;
            }
        }

        void DoCapture()
        {
            if (!GameCapture.Instance.IsActive)
                return;
            using var keybagFrame = GameCapture.CaptureMapKeybag();
            if (keybagFrame.Width == 0 || keybagFrame.Height == 0)
                return;

            var mapKeybagImage = RawImageView.FromFrame(keybagFrame);
            MapHudReader reader = new(mapKeybagImage);

            bool hasMap = DungeonStore.IsMapFound = reader.HasMap();
            DungeonStore.IsKeybagFound = reader.HasHud();
            if (!hasMap)
            {
                bool winterfaceSuccess = false;
                // Check winterface
                using var winterfaceFrame = GameCapture.CaptureWinterface();
                WinterfaceReader winterfaceReader = new(RawImageView.FromFrame(winterfaceFrame));
                if (winterfaceReader.HasWinterface() && DungeonStore.Instance.LastActiveTime != _lastProcessedFloorTime)
                {
                    if (DungeonStore.IsActive)
                    {
                        // Just ended floor
                        DungeonStore.KeyMask = 0;
                        DungeonStore.IsActive = false;
                        int floorEndTicks = (int)(DungeonStore.RoomStopwatch.Elapsed.TotalMilliseconds / 600);
                        DungeonStore.AddRoomTick(new(-1, -1, floorEndTicks));
                        LogService.Instance.Log($"Ending floor took {floorEndTicks} ticks ({floorEndTicks * 0.6:F1} s)");
                    }
                    if (!string.IsNullOrEmpty(winterfaceReader.ReadField(Field.FinalXP)))
                    {
                        _lastProcessedFloorTime = DungeonStore.Instance.LastActiveTime;
                        ProcessFinishedFloor(winterfaceReader);
                        winterfaceSuccess = true;
                    }
                }
                if (DateTimeOffset.Now - DungeonStore.LastActiveTime > TimeSpan.FromSeconds(6))
                {
                    // Probably out of floor (e.g. left party)
                    if (DungeonStore.IsActive && !winterfaceSuccess)
                    {
                        LogService.Instance.Log("Out of floor or map turned off; resetting.");
                        DungeonStore.KeyMask = 0;
                        DungeonStore.IsActive = false;
                    }
                }
                return;
            }
            if (!DungeonStore.IsActive)
                NewFloor();
            DungeonStore.LastActiveTime = DateTimeOffset.Now;
            var (newMap, newRoomCount) = reader.ReadMap();
            var (mapChanged, newRoomLocations) = DungeonStore.Map.Merge(newMap);

            ulong newKeymask = reader.ReadKeybag();
            ulong keymaskDelta = newKeymask & ~DungeonStore.KeyMask;
            bool changed = mapChanged || keymaskDelta != 0;
            if (newRoomCount > DungeonStore.RoomCount)
            {
                if (DungeonStore.RoomCount == 0)
                {
                    if (newRoomCount == 1)
                        DungeonStore.AddRoomTick(new(DungeonStore.Map.Base.Row, DungeonStore.Map.Base.Col, 0));
                    else
                        DungeonStore.IsPartial = true;
                }
                DungeonStore.RoomCount = newRoomCount;
                int ticks = (int)Math.Round(DungeonStore.RoomStopwatch.Elapsed.TotalMilliseconds / 600);
                if (newRoomCount > 1)
                    foreach (var (i, j) in newRoomLocations)
                    {
                        DungeonStore.AddRoomTick(new(i, j, ticks));
                        LogService.Instance.Log($"Room {DungeonStore.RoomTicks.Count} took {ticks} ticks ({ticks * 0.6:F1} s)");
                        ticks = 0;  // Rooms opened simultaneously are considered to have taken 0 ticks after the first one.
                    }
                DungeonStore.RoomStopwatch.Restart();
            }
            // Since the 2026/7/6 update we can now track key locations in solos accurately.
            if (keymaskDelta != 0 && DungeonStore.Map.PlayerCount == 1 && !DungeonStore.IsPartial)
            {
                var o = DungeonStore.Map.PlayerLocations[0];
                if (o.HasValue)
                    for (int k = 0; k < 64; ++k)
                        if ((keymaskDelta & 1UL << k) != 0)
                        {
                            LogService.Instance.Log($"Found key {Registry.Instance.GetKeyName(k)} in {o.Value.ToChessString()}");
                            DungeonStore.Map[o.Value.Row, o.Value.Col].Key = k;
                        }
            }
            if (changed)
            {
                DungeonStore.KeyMask |= newKeymask;
                DungeonStore.UpdateAnnotatedMapImage();
                OnMapChanged?.Invoke();
            }
            DungeonStore.UpdateMapImage(mapKeybagImage);
        }

        private void NewFloor()
        {
            DungeonStore.IsActive = true;
            DungeonStore.IsPartial = false;
            DungeonStore.KeyMask = 0;
            DungeonStore.RoomCount = 0;
            DungeonStore.FloorStartTime = DateTimeOffset.Now - TimeSpan.FromMilliseconds(1150); // Empirical
            DungeonStore.Map = new Map(8, 8);
            DungeonStore.RoomTicks.Clear();
            DungeonStore.RoomStopwatch.Restart();
            OnNewFloor?.Invoke();
            LogService.Instance.Log($"Start of floor {++DungeonStore.FloorCounter}");
        }

        private void ProcessFinishedFloor(WinterfaceReader reader)
        {
            var (skillCount, skillTicks) = DungeonStore.Map.SkillDoorStats();
            StringBuilder notesSB = new();
            for (int i = 0; i < DungeonStore.RoomTicks.Count; ++i)
            {
                var (_, _, ticks) = DungeonStore.RoomTicks[i];
                if (ticks >= 20)
                {
                    if (notesSB.Length > 0)
                        _ = notesSB.Append(", ");
                    _ = notesSB.Append($"r{i + 1}: {ticks}t");
                }
            }

            var floorData = reader.Read();
            floorData.Id = DungeonStore.FloorCounter;
            floorData.Timestamp = DungeonStore.LastActiveTime;
            floorData.RoomCount = DungeonStore.RoomCount;
            floorData.DeadEnds = DungeonStore.Map.DeadEndCount();
            floorData.SkillDoors = skillCount;
            floorData.SkillDoorTicks = skillTicks;
            floorData.PossiblePuzzleRooms = DungeonStore.Map.PossiblePuzzleCount();
            floorData.KeyCount = DungeonStore.Map.KeyCount();
            floorData.Notes = notesSB.ToString();
            floorData.Map = DungeonStore.Map.Clone();
            floorData.MapImage = DungeonStore.MapImage.Clone();
            floorData.MapImage.Freeze();
            floorData.RoomTicks = [.. DungeonStore.RoomTicks];
            if (floorData.PartySize == 0)
                floorData.PartySize = DungeonStore.Map.PlayerCount;

            LogService.Instance.Log($"Floor {DungeonStore.FloorCounter} time={floorData.Time}");

            try
            {
                // Write floor log, and save map/winterface images.
                File.AppendAllText("floors.txt", floorData.ToTabSeparatedString());
                File.AppendAllText("floors.jsonl", floorData.ToJsonString() + Environment.NewLine);
                ImageWriter.SaveToPng(DungeonStore.MapImage, $"{Settings.Instance.MapSaveLocation}\\{floorData.Timestamp:yyyy-MM-dd HH-mm-ss}.png");
                RawImage raw = new(reader.Image.Pixels.ToArray(), reader.Image.Width, reader.Image.Height);
                ImageWriter.SaveCroppedToPng(raw, $"{Settings.Instance.WinterfaceSaveLocation}\\{floorData.Timestamp:yyyy-MM-dd HH-mm-ss}.png", 0, 0, WinterfaceReader.WinterfaceWidth, WinterfaceReader.WinterfaceHeight);
            }
            catch (IOException e)
            {
                LogService.Instance.Log("Error writing floor log to file or saving map/winterface images: " + e.Message);
            }
            // Notify the ViewModel
            OnFloorCompleted?.Invoke(floorData);
        }
    }
}