using System.IO;
using System.Text.Json;

namespace Dungeons2.Services;

internal static class FloorHistoryLoader
{
    public static void Append(string path, FloorData floor)
    {
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        if (stream.Length > 0)
        {
            stream.Seek(-1, SeekOrigin.End);
            if (stream.ReadByte() != '\n') stream.WriteByte((byte)'\n');
        }
        using var writer = new StreamWriter(stream);
        writer.WriteLine(floor.ToJsonString());
    }

    public static IEnumerable<FloorData> Load(string path)
    {
        var floors = new List<FloorData>();
        if (!File.Exists(path)) return floors;
        FileSafety.Try("Reading floor history", () =>
        {
            int lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var floor = JsonSerializer.Deserialize<FloorData>(line);
                    if (floor is null) throw new JsonException("Null floor record.");
                    floor.RoomTicks ??= [];
                    if (floor.Map is { } map && (map.Rows != 8 || map.Cols != 8 || !map.HasValidData()))
                        throw new JsonException("Invalid map data.");
                    floors.Add(floor);
                }
                catch (Exception e) when (e is JsonException or ArgumentException or NotSupportedException)
                {
                    LogService.Instance.LogError($"Skipped floor history line {lineNumber}: {e.Message}");
                }
            }
        });
        return floors;
    }
}
