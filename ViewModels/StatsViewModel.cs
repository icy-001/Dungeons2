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
            static bool filter(FloorData floor) => !floor.IsPartial && floor.PartySize == 1 && floor.Bonus == 1 && floor.Size == "Large" && floor.FloorNumber <= 11;

            var eligibleFloors = DungeonStore.Instance.FloorHistory.Where(filter).ToList();
            var matchingTicks = eligibleFloors
                .SelectMany(floor => floor.RoomTicks)
                .Select(split => split.Ticks)
                .Where(ticks => ticks > 0)
                .ToList();

            Histograms.Clear();
            ScatterPlots.Clear();
            Histograms.Add(CreateHistogram("Room ticks", matchingTicks));
            Histograms.Add(CreateHistogram("Key count", eligibleFloors.Select(floor => floor.KeyCount)));
            Histograms.Add(CreateHistogram("Critical key count", eligibleFloors.Select(floor => floor.CriticalKeyCount)));
            Histograms.Add(CreateHistogram("Bonus key count", eligibleFloors.Select(floor => floor.KeyCount - floor.CriticalKeyCount)));
            Histograms.Add(CreateHistogram("Critical rooms", eligibleFloors.Select(floor => floor.CriticalRoomCount)));
            Histograms.Add(CreateHistogram("Depth", eligibleFloors.Select(floor => floor.Depth)));
            Histograms.Add(CreateHistogram("Dead ends", eligibleFloors.Select(floor => floor.DeadEnds)));
            Histograms.Add(CreateHistogram("Dewks", eligibleFloors.Select(floor => floor.DeadEndCountExtended.Dewks)));
            Histograms.Add(CreateHistogram("Denks", eligibleFloors.Select(floor => floor.DeadEndCountExtended.Denks)));

            ScatterPlots.Add(CreateScatterPlot("Critical rooms vs room count", eligibleFloors.Select(floor => new ScatterPoint(floor.RoomCount, floor.CriticalRoomCount)).ToList()));
            ScatterPlots.Add(CreateScatterPlot("Critical key count vs critical rooms", eligibleFloors.Select(floor => new ScatterPoint(floor.CriticalRoomCount, floor.CriticalKeyCount)).ToList()));
            ScatterPlots.Add(CreateScatterPlot("Bonus key count vs bonus room count", eligibleFloors.Select(floor => new ScatterPoint(floor.RoomCount, floor.KeyCount - floor.CriticalKeyCount)).ToList()));

            Summary = eligibleFloors.Count == 0
                ? "No fully completed solo large frozen floors have been recorded yet."
                : $"Showing {matchingTicks.Count} room ticks from {eligibleFloors.Count} fully completed solo large frozen floors.";
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
