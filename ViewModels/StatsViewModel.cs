using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace Dungeons2.ViewModels
{
    public partial class StatsViewModel : ObservableObject
    {
        public ObservableCollection<FloorMetricHistogram> Histograms { get; } = [];
        public ObservableCollection<FloorTimeCdfPlot> FloorTimeCdfPlots { get; } = [];
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
            static bool furnishedFilter(FloorData floor) => !floor.IsPartial && floor.PartySize == 1 && floor.Bonus == 1 && floor.Size == "Large" && floor.FloorNumber >= 18 && floor.FloorNumber <= 29;
            static bool ab2Filter(FloorData floor) => !floor.IsPartial && floor.PartySize == 1 && floor.Bonus == 1 && floor.Size == "Large" && floor.FloorNumber >= 30 && floor.FloorNumber <= 35;
            static bool occultFilter(FloorData floor) => !floor.IsPartial && floor.PartySize == 1 && floor.Bonus == 1 && floor.Size == "Large" && floor.FloorNumber >= 36 && floor.FloorNumber <= 47;
            static bool warpedFilter(FloorData floor) => !floor.IsPartial && floor.PartySize == 1 && floor.Bonus == 1 && floor.Size == "Large" && floor.FloorNumber >= 48 && floor.FloorNumber <= 60;

            var frozenFloors = DungeonStore.Instance.FloorHistory.Where(frozenFilter).ToList();
            var ab1Floors = DungeonStore.Instance.FloorHistory.Where(ab1Filter).ToList();
            var furnishedFloors = DungeonStore.Instance.FloorHistory.Where(furnishedFilter).ToList();
            var ab2Floors = DungeonStore.Instance.FloorHistory.Where(ab2Filter).ToList();
            var occultFloors = DungeonStore.Instance.FloorHistory.Where(occultFilter).ToList();
            var warpedFloors = DungeonStore.Instance.FloorHistory.Where(warpedFilter).ToList();

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
            var furnishedTicks = furnishedFloors
                .SelectMany(floor => floor.RoomTicks)
                .Select(split => split.Ticks)
                .Where(ticks => ticks > 0)
                .ToList();
            var ab2Ticks = ab2Floors
                .SelectMany(floor => floor.RoomTicks)
                .Select(split => split.Ticks)
                .Where(ticks => ticks > 0)
                .ToList();
            var occultTicks = occultFloors
                .SelectMany(floor => floor.RoomTicks)
                .Select(split => split.Ticks)
                .Where(ticks => ticks > 0)
                .ToList();
            var warpedTicks = warpedFloors
                .SelectMany(floor => floor.RoomTicks)
                .Select(split => split.Ticks)
                .Where(ticks => ticks > 0)
                .ToList();

            Histograms.Clear();
            FloorTimeCdfPlots.Clear();
            ScatterPlots.Clear();
            Histograms.Add(CreateHistogram("Frozen room ticks", frozenTicks, frozenFloors.Count));
            Histograms.Add(CreateHistogram("Abandoned 1 room ticks", ab1Ticks, ab1Floors.Count));
            Histograms.Add(CreateHistogram("Furnished room ticks", furnishedTicks, furnishedFloors.Count));
            Histograms.Add(CreateHistogram("Abandoned 2 room ticks", ab2Ticks, ab2Floors.Count));
            Histograms.Add(CreateHistogram("Occult room ticks", occultTicks, occultFloors.Count));
            Histograms.Add(CreateHistogram("Warped room ticks", warpedTicks, warpedFloors.Count));
            Histograms.Add(CreateHistogram("Key count", frozenFloors.Select(floor => floor.KeyCount)));
            Histograms.Add(CreateHistogram("Critical key count", frozenFloors.Select(floor => floor.CriticalKeyCount)));
            Histograms.Add(CreateHistogram("Bonus key count", frozenFloors.Select(floor => floor.KeyCount - floor.CriticalKeyCount)));
            Histograms.Add(CreateHistogram("Critical rooms", frozenFloors.Select(floor => floor.CriticalRoomCount)));
            Histograms.Add(CreateHistogram("Depth", frozenFloors.Select(floor => floor.Depth)));
            Histograms.Add(CreateHistogram("Dead ends", frozenFloors.Select(floor => floor.DeadEnds)));
            Histograms.Add(CreateHistogram("Dewks", frozenFloors.Select(floor => floor.DeadEndCountExtended.Dewks)));
            Histograms.Add(CreateHistogram("Denks", frozenFloors.Select(floor => floor.DeadEndCountExtended.Denks)));

            FloorTimeCdfPlots.Add(CreateFloorTimeCdf("Frozen floor time CDF", frozenTicks));
            FloorTimeCdfPlots.Add(CreateFloorTimeCdf("Abandoned 1 floor time CDF", ab1Ticks));
            FloorTimeCdfPlots.Add(CreateFloorTimeCdf("Furnished floor time CDF", furnishedTicks));
            FloorTimeCdfPlots.Add(CreateFloorTimeCdf("Abandoned 2 floor time CDF", ab2Ticks));
            FloorTimeCdfPlots.Add(CreateFloorTimeCdf("Occult floor time CDF", occultTicks));
            FloorTimeCdfPlots.Add(CreateFloorTimeCdf("Warped floor time CDF", warpedTicks));

            ScatterPlots.Add(CreateScatterPlot("Critical rooms vs room count", frozenFloors.Select(floor => new ScatterPoint(floor.RoomCount, floor.CriticalRoomCount)).ToList()));
            ScatterPlots.Add(CreateScatterPlot("Critical key count vs critical rooms", frozenFloors.Select(floor => new ScatterPoint(floor.CriticalRoomCount, floor.CriticalKeyCount)).ToList()));
            ScatterPlots.Add(CreateScatterPlot("Bonus key count vs bonus room count", frozenFloors.Select(floor => new ScatterPoint(floor.RoomCount, floor.KeyCount - floor.CriticalKeyCount)).ToList()));

            int totalTicks = frozenTicks.Count + ab1Ticks.Count + furnishedTicks.Count + ab2Ticks.Count + occultTicks.Count + warpedTicks.Count;
            int totalFloors = frozenFloors.Count + ab1Floors.Count + furnishedFloors.Count + ab2Floors.Count + occultFloors.Count + warpedFloors.Count;

            Summary = totalFloors == 0
                ? "No fully completed solo large floors have been recorded yet."
                : $"Showing {totalTicks} room ticks from {totalFloors} fully completed solo large floors across all themes.";
        }

        private static FloorMetricHistogram CreateHistogram(string title, IEnumerable<int> values, int? floorCount = null)
        {
            var orderedValues = values.ToList();
            var histogram = new FloorMetricHistogram(title, orderedValues.Count, floorCount);
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

        private static FloorTimeCdfPlot CreateFloorTimeCdf(string title, IReadOnlyCollection<int> roomTicks)
        {
            var plot = new FloorTimeCdfPlot(title, roomTicks.Count);
            if (roomTicks.Count == 0)
                return plot;

            var roomTickPmf = roomTicks
                .GroupBy(ticks => ticks)
                .OrderBy(group => group.Key)
                .Select(group => (Ticks: group.Key, Probability: (double)group.Count() / roomTicks.Count))
                .ToList();

            double[] roomCountProbabilities =
            [
                1.0 / 45, 1.0 / 45, 1.0 / 45, 1.0 / 45,
                37.0 / 315, 37.0 / 315, 37.0 / 315, 37.0 / 315,
                37.0 / 315, 37.0 / 315, 37.0 / 315,
                1.0 / 45, 1.0 / 45, 1.0 / 45, 1.0 / 45
            ];

            int maximumRoomTicks = roomTickPmf[^1].Ticks;
            double[] sumDistribution = [1.0];
            double[] floorTimeDistribution = new double[64 * maximumRoomTicks + 1];

            for (int roomCount = 1; roomCount <= 64; roomCount++)
            {
                var nextDistribution = new double[sumDistribution.Length + maximumRoomTicks];
                for (int totalTicks = 0; totalTicks < sumDistribution.Length; totalTicks++)
                {
                    double sumProbability = sumDistribution[totalTicks];
                    if (sumProbability == 0)
                        continue;

                    foreach (var (ticks, probability) in roomTickPmf)
                        nextDistribution[totalTicks + ticks] += sumProbability * probability;
                }

                sumDistribution = nextDistribution;
                if (roomCount < 50)
                    continue;

                double roomCountProbability = roomCountProbabilities[roomCount - 50];
                for (int totalTicks = 0; totalTicks < sumDistribution.Length; totalTicks++)
                    floorTimeDistribution[totalTicks] += roomCountProbability * sumDistribution[totalTicks];
            }

            plot.SetDistribution(floorTimeDistribution);
            return plot;
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

    public class FloorMetricHistogram(string title, int sampleCount, int? floorCount = null)
    {
        public string Title { get; } = title;
        public string? ThemeCounts => floorCount.HasValue ? $"Rooms: {sampleCount}   Floors: {floorCount.Value}" : null;
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

    public partial class FloorTimeCdfPlot(string title, int roomSampleCount) : ObservableObject
    {
        private const double ChartWidth = 520;
        private const double ChartHeight = 300;
        private const double LeftMargin = 52;
        private const double RightMargin = 16;
        private const double TopMargin = 16;
        private const double BottomMargin = 38;
        private const int MinimumDisplayedTicks = 450; // 4:30 at 0.6 seconds per tick.
        private const int MaximumDisplayedTicks = 900; // 9:00 at 0.6 seconds per tick.
        private double[] _cumulativeProbabilities = [];

        public string Title { get; } = title;
        public string Detail => roomSampleCount == 0
            ? "No room-tick data"
            : $"Modeled from {roomSampleCount} room samples; room count 50–64";
        public PointCollection CdfPoints { get; } = [];
        public string MinimumTimeLabel => "4:30";
        public string MaximumTimeLabel => "9:00";
        public bool HasDistribution => _cumulativeProbabilities.Length > 0;

        [ObservableProperty]
        public partial double HoverMarkerLeft { get; set; }

        [ObservableProperty]
        public partial double HoverMarkerTop { get; set; }

        [ObservableProperty]
        public partial double HoverCalloutLeft { get; set; }

        [ObservableProperty]
        public partial double HoverCalloutTop { get; set; }

        [ObservableProperty]
        public partial Visibility HoverMarkerVisibility { get; set; } = Visibility.Collapsed;

        [ObservableProperty]
        public partial string HoverTooltip { get; set; } = string.Empty;

        public void SetDistribution(IReadOnlyList<double> probabilities)
        {
            double probabilityTotal = probabilities.Sum();
            if (probabilityTotal == 0)
                return;

            _cumulativeProbabilities = new double[probabilities.Count];
            double runningProbability = 0;
            for (int totalTicks = 0; totalTicks < probabilities.Count; totalTicks++)
            {
                runningProbability += probabilities[totalTicks] / probabilityTotal;
                _cumulativeProbabilities[totalTicks] = runningProbability;
            }

            double plotWidth = ChartWidth - LeftMargin - RightMargin;
            double plotHeight = ChartHeight - TopMargin - BottomMargin;
            double cumulativeProbability = probabilities
                .Take(Math.Min(MinimumDisplayedTicks + 1, probabilities.Count))
                .Sum() / probabilityTotal;

            CdfPoints.Add(new Point(LeftMargin, TopMargin + (1 - cumulativeProbability) * plotHeight));

            int lastAvailableTick = Math.Min(MaximumDisplayedTicks, probabilities.Count - 1);
            for (int totalTicks = MinimumDisplayedTicks + 1; totalTicks <= lastAvailableTick; totalTicks++)
            {
                double probability = probabilities[totalTicks];
                if (probability == 0)
                    continue;

                double normalizedX = (double)(totalTicks - MinimumDisplayedTicks) /
                    (MaximumDisplayedTicks - MinimumDisplayedTicks);
                double x = LeftMargin + normalizedX * plotWidth;
                CdfPoints.Add(new Point(x, TopMargin + (1 - cumulativeProbability) * plotHeight));
                cumulativeProbability += probability / probabilityTotal;
                CdfPoints.Add(new Point(x, TopMargin + (1 - cumulativeProbability) * plotHeight));
            }

            CdfPoints.Add(new Point(ChartWidth - RightMargin, TopMargin + (1 - cumulativeProbability) * plotHeight));
        }

        public void UpdateHover(double canvasX)
        {
            if (!HasDistribution)
            {
                ClearHover();
                return;
            }

            double plotRight = ChartWidth - RightMargin;
            double x = Math.Clamp(canvasX, LeftMargin, plotRight);
            double normalizedX = (x - LeftMargin) / (plotRight - LeftMargin);
            double exactTicks = MinimumDisplayedTicks + normalizedX *
                (MaximumDisplayedTicks - MinimumDisplayedTicks);
            int cdfIndex = Math.Min((int)Math.Floor(exactTicks), _cumulativeProbabilities.Length - 1);
            double probability = cdfIndex < 0 ? 0 : _cumulativeProbabilities[cdfIndex];
            double plotHeight = ChartHeight - TopMargin - BottomMargin;
            double seconds = exactTicks * 0.6;

            HoverMarkerLeft = x - 4.5;
            HoverMarkerTop = TopMargin + (1 - probability) * plotHeight - 4.5;
            HoverCalloutLeft = x <= ChartWidth - RightMargin - 145 ? x + 12 : x - 142;
            HoverCalloutTop = HoverMarkerTop >= 62 ? HoverMarkerTop - 56 : HoverMarkerTop + 15;
            HoverTooltip = $"Time: {(int)(seconds / 60)}:{seconds % 60:00.0}\nProbability: {probability:P2}";
            HoverMarkerVisibility = Visibility.Visible;
        }

        public void ClearHover()
        {
            HoverMarkerVisibility = Visibility.Collapsed;
        }
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
