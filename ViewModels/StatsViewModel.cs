using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Dungeons2.ViewModels
{
    public partial class StatsViewModel : ObservableObject
    {
        public ObservableCollection<FloorMetricHistogram> Histograms { get; } = [];
        public ObservableCollection<ScatterPlot> ScatterPlots { get; } = [];

        [ObservableProperty]
        public partial string Summary { get; set; } = "No eligible floors recorded yet.";

        public StatsViewModel()
        {
            DungeonStore.Instance.FloorHistory.CollectionChanged += (_, _) => RefreshHistograms();
            RefreshHistograms();
        }

        private void RefreshHistograms()
        {
            static bool frozenFilter(FloorData floor) => !floor.IsPartial && floor.PartySize == 1 && floor.Bonus == 1 && floor.Size == "Large" && floor.FloorNumber <= 11;
            static bool ab1Filter(FloorData floor) => !floor.IsPartial && floor.PartySize == 1 && floor.Bonus == 1 && floor.Size == "Large" && floor.FloorNumber >= 12 && floor.FloorNumber <= 17;

            var frozenFloors = DungeonStore.Instance.FloorHistory.Where(frozenFilter).ToList();
            var ab1Floors = DungeonStore.Instance.FloorHistory.Where(ab1Filter).ToList();
            var frozenTicks = frozenFloors
                .SelectMany(floor => floor.RoomTicks)
                .Select(split => split.Ticks)
                .Where(ticks => ticks > 0)
                .ToList();
            var ab1Ticks = ab1Floors
                .SelectMany(floor => floor.RoomTicks)
                .Select(split => split.Ticks)
                .Where(ticks => ticks > 0)
                .ToList();

            Histograms.Clear();
            ScatterPlots.Clear();
            Histograms.Add(CreateHistogram("Frozen room ticks", frozenTicks));
            Histograms.Add(CreateHistogram("Ab1 room ticks", ab1Ticks));
            Histograms.Add(CreateHistogram("Key count", frozenFloors.Select(floor => floor.KeyCount)));
            Histograms.Add(CreateHistogram("Critical key count", frozenFloors.Select(floor => floor.CriticalKeyCount)));
            Histograms.Add(CreateHistogram("Bonus key count", frozenFloors.Select(floor => floor.KeyCount - floor.CriticalKeyCount)));
            Histograms.Add(CreateHistogram("Critical rooms", frozenFloors.Select(floor => floor.CriticalRoomCount)));
            Histograms.Add(CreateHistogram("Depth", frozenFloors.Select(floor => floor.Depth)));
            Histograms.Add(CreateHistogram("Dead ends", frozenFloors.Select(floor => floor.DeadEnds)));
            Histograms.Add(CreateHistogram("Dewks", frozenFloors.Select(floor => floor.DeadEndCountExtended.Dewks)));
            Histograms.Add(CreateHistogram("Denks", frozenFloors.Select(floor => floor.DeadEndCountExtended.Denks)));

            ScatterPlots.Add(CreateScatterPlot("Critical rooms vs room count", frozenFloors.Select(floor => new ScatterPoint(floor.RoomCount, floor.CriticalRoomCount)).ToList()));
            ScatterPlots.Add(CreateScatterPlot("Critical key count vs critical rooms", frozenFloors.Select(floor => new ScatterPoint(floor.CriticalRoomCount, floor.CriticalKeyCount)).ToList()));
            ScatterPlots.Add(CreateScatterPlot("Bonus key count vs bonus room count", frozenFloors.Select(floor => new ScatterPoint(floor.RoomCount, floor.KeyCount - floor.CriticalKeyCount)).ToList()));

            Summary = frozenFloors.Count == 0
                ? "No fully completed solo large frozen floors have been recorded yet."
                : $"Showing {frozenTicks.Count} room ticks from {frozenFloors.Count} fully completed solo large frozen floors.";
        }

        private static FloorMetricHistogram CreateHistogram(string title, IEnumerable<int> values)
        {
            var histogram = new FloorMetricHistogram(title);

            var orderedValues = values.ToList();
            if (orderedValues.Count == 0)
                return histogram;

            histogram.Average = orderedValues.Average();
            var variance = orderedValues.Count <= 1
                ? 0
                : orderedValues.Sum(value => Math.Pow(value - histogram.Average, 2)) / orderedValues.Count;
            histogram.StandardDeviation = Math.Sqrt(variance);

            int lowerBound = orderedValues.Min();
            int upperBound = orderedValues.Max();

            if (lowerBound == upperBound)
            {
                int count = orderedValues.Count(value => value == lowerBound);
                histogram.HistogramBars.Add(new HistogramBar(lowerBound, count, count));
                return histogram;
            }

            var bucketCounts = new List<(int Value, int Count)>();
            for (int value = lowerBound; value <= upperBound; value++)
            {
                int count = orderedValues.Count(item => item == value);
                bucketCounts.Add((value, count));
            }

            int maxBucketCount = bucketCounts.Count == 0 ? 0 : bucketCounts.Max(bucket => bucket.Count);
            foreach (var (value, count) in bucketCounts)
            {
                histogram.HistogramBars.Add(new HistogramBar(value, count, maxBucketCount));
            }

            return histogram;
        }

        private static ScatterPlot CreateScatterPlot(string title, IReadOnlyList<ScatterPoint> points)
        {
            var plot = new ScatterPlot(title);
            if (points.Count == 0)
                return plot;

            double minX = points.Min(point => point.X);
            double maxX = points.Max(point => point.X);
            double minY = points.Min(point => point.Y);
            double maxY = points.Max(point => point.Y);

            const double leftMargin = 40;
            const double rightMargin = 20;
            const double topMargin = 20;
            const double bottomMargin = 20;
            double plotWidth = 480 - leftMargin - rightMargin;
            double plotHeight = 280 - topMargin - bottomMargin;

            var pointFrequencies = points
                .GroupBy(point => (point.X, point.Y))
                .ToDictionary(group => group.Key, group => group.Count());

            int maxFrequency = pointFrequencies.Values.DefaultIfEmpty(0).Max();

            foreach (var point in points)
            {
                double normalizedX = maxX == minX ? 0.5 : (point.X - minX) / (maxX - minX);
                double normalizedY = maxY == minY ? 0.5 : (point.Y - minY) / (maxY - minY);
                point.CanvasLeft = leftMargin + normalizedX * plotWidth;
                point.CanvasTop = topMargin + (1 - normalizedY) * plotHeight;
                point.Intensity = maxFrequency <= 1 ? 0.75 : 0.25 + 0.75 * pointFrequencies[(point.X, point.Y)] / maxFrequency;
                plot.Points.Add(point);
            }

            return plot;
        }
    }

    public class FloorMetricHistogram(string title)
    {
        public string Title { get; } = title;
        public ObservableCollection<HistogramBar> HistogramBars { get; } = [];
        public double Average { get; set; }
        public double StandardDeviation { get; set; }
        public string Summary => HistogramBars.Count == 0 ? "No data" : $"{HistogramBars.Count} buckets";
    }

    public class HistogramBar(int x, int count, int maxCount)
    {
        public int X { get; } = x;
        public int Count { get; } = count;
        public int MaxCount { get; } = maxCount;

        public string Label => X.ToString();
        public double BarHeight => MaxCount <= 0 ? 0 : 220.0 * Count / MaxCount;
        public string CountLabel => Count.ToString();
    }

    public class ScatterPlot(string title)
    {
        public string Title { get; } = title;
        public ObservableCollection<ScatterPoint> Points { get; } = [];
    }

    public class ScatterPoint(double x, double y)
    {
        public double X { get; } = x;
        public double Y { get; } = y;
        public double CanvasLeft { get; set; }
        public double CanvasTop { get; set; }
        public double Intensity { get; set; } = 0.75;
    }
}
