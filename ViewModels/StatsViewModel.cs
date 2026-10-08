using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace Dungeons2.ViewModels
{
    public partial class StatsViewModel : ObservableObject
    {
        private static readonly string[] ThemeNames = ["Frozen", "Abandoned 1", "Furnished", "Abandoned 2", "Occult", "Warped"];
        public ObservableCollection<FloorMetricHistogram> Histograms { get; } = [];
        public ObservableCollection<FloorTimeCdfPlot> FloorTimeCdfPlots { get; } = [];
        public ObservableCollection<ScatterPlot> ScatterPlots { get; } = [];

        public IReadOnlyList<string> Themes { get; } = Array.AsReadOnly(ThemeNames);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RoomTimeCharts))]
        public partial int SelectedThemeIndex { get; set; }

        [ObservableProperty]
        public partial int SelectedSectionIndex { get; set; }

        public IEnumerable<object> RoomTimeCharts =>
            SelectedThemeIndex >= 0 && SelectedThemeIndex < ThemeNames.Length &&
            SelectedThemeIndex < Histograms.Count && SelectedThemeIndex < FloorTimeCdfPlots.Count
                ? [Histograms[SelectedThemeIndex], FloorTimeCdfPlots[SelectedThemeIndex]]
                : [];

        public IEnumerable<object> MiscellaneousCharts => Histograms.Skip(ThemeNames.Length).Cast<object>().Concat(ScatterPlots);

        [ObservableProperty]
        public partial string Summary { get; set; } = "No eligible floors recorded yet.";

        public StatsViewModel()
        {
            DungeonStore.Instance.FloorHistory.CollectionChanged += OnFloorHistoryChanged;
            RefreshHistograms();
        }

        private static int GetThemeIndex(FloorData floor)
        {
            if (floor.IsPartial || floor.PartySize != 1 || floor.Bonus != 1 || floor.Size != "Large")
                return -1;

            return floor.FloorNumber switch
            {
                <= 11 => 0,
                <= 17 => 1,
                <= 29 => 2,
                <= 35 => 3,
                <= 47 => 4,
                <= 60 => 5,
                _ => -1
            };
        }

        private void OnFloorHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RefreshHistograms();
                return;
            }

            var affectedThemes = (e.OldItems?.Cast<FloorData>() ?? [])
                .Concat(e.NewItems?.Cast<FloorData>() ?? [])
                .Select(GetThemeIndex).Where(theme => theme >= 0).ToHashSet();
            if (affectedThemes.Count > 0)
                RefreshHistograms(affectedThemes);
        }

        private void RefreshHistograms(HashSet<int>? affectedThemes = null)
        {
            var floorsByTheme = ThemeNames.Select(_ => new List<FloorData>()).ToArray();
            foreach (var floor in DungeonStore.Instance.FloorHistory)
            {
                int theme = GetThemeIndex(floor);
                if (theme >= 0)
                    floorsByTheme[theme].Add(floor);
            }

            for (int theme = 0; theme < ThemeNames.Length; theme++)
            {
                if (affectedThemes is not null && !affectedThemes.Contains(theme))
                    continue;

                var floors = floorsByTheme[theme];
                var ticks = floors.SelectMany(floor => floor.RoomTicks)
                    .Select(split => split.Ticks).Where(value => value > 1).ToList();
                SetItem(Histograms, theme, CreateHistogram($"{ThemeNames[theme]} room ticks", ticks, floors.Count));
                SetItem(FloorTimeCdfPlots, theme, CreateFloorTimeCdf($"{ThemeNames[theme]} floor time CDF", ticks, AverageFloorTime(floors)));
            }

            RefreshMiscellaneousMetrics(floorsByTheme.SelectMany(floors => floors));

            int totalTicks = Histograms.Take(ThemeNames.Length).Sum(histogram => histogram.SampleCount);
            int totalFloors = Histograms.Take(ThemeNames.Length).Sum(histogram => histogram.FloorCount ?? 0);
            Summary = totalFloors == 0
                ? "No fully completed solo large floors have been recorded yet."
                : $"Showing {totalTicks} room ticks from {totalFloors} fully completed solo large floors across all themes.";
            if (affectedThemes is null || affectedThemes.Contains(SelectedThemeIndex))
                OnPropertyChanged(nameof(RoomTimeCharts));
            OnPropertyChanged(nameof(MiscellaneousCharts));
        }

        private static void SetItem<T>(ObservableCollection<T> items, int index, T item)
        {
            if (index < items.Count)
                items[index] = item;
            else
                items.Add(item);
        }

        private void RefreshMiscellaneousMetrics(IEnumerable<FloorData> floors)
        {
            // These properties traverse the map; compute them once per floor and
            // share the results between histograms and scatter plots.
            var metrics = floors.Select(floor =>
            {
                var critical = floor.Map?.ComputeCriticalStats() ?? (Count: 0, KeyCount: 0);
                return new
                {
                    floor.KeyCount,
                    floor.RoomCount,
                    floor.DeadEnds,
                    CriticalKeyCount = critical.KeyCount,
                    CriticalRoomCount = critical.Count,
                    floor.Depth,
                    floor.DeadEndCountExtended
                };
            }).ToList();
            int index = ThemeNames.Length;
            SetItem(Histograms, index++, CreateHistogram("Key count", metrics.Select(floor => floor.KeyCount)));
            SetItem(Histograms, index++, CreateHistogram("Critical key count", metrics.Select(floor => floor.CriticalKeyCount)));
            SetItem(Histograms, index++, CreateHistogram("Bonus key count", metrics.Select(floor => floor.KeyCount - floor.CriticalKeyCount)));
            SetItem(Histograms, index++, CreateHistogram("Critical rooms", metrics.Select(floor => floor.CriticalRoomCount)));
            SetItem(Histograms, index++, CreateHistogram("Depth", metrics.Where(floor => floor.Depth > 0).Select(floor => floor.Depth)));
            SetItem(Histograms, index++, CreateHistogram("Dead ends", metrics.Select(floor => floor.DeadEnds)));
            SetItem(Histograms, index++, CreateHistogram("Dewks", metrics.Select(floor => floor.DeadEndCountExtended.Dewks)));
            SetItem(Histograms, index, CreateHistogram("Denks", metrics.Select(floor => floor.DeadEndCountExtended.Denks)));

            ScatterPlots.Clear();
        }

        private static FloorMetricHistogram CreateHistogram(string title, IEnumerable<int> values, int? floorCount = null)
        {
            var counts = new Dictionary<int, int>();
            int sampleCount = 0;
            double sum = 0;
            foreach (int value in values)
            {
                counts[value] = counts.GetValueOrDefault(value) + 1;
                sampleCount++;
                sum += value;
            }

            var histogram = new FloorMetricHistogram(title, sampleCount, floorCount);
            if (sampleCount == 0)
                return histogram;

            histogram.Average = sum / sampleCount;
            var variance = counts.Sum(bucket => bucket.Value * Math.Pow(bucket.Key - histogram.Average, 2)) / sampleCount;
            histogram.StandardDeviation = Math.Sqrt(variance);

            int lowerBound = counts.Keys.Min();
            int upperBound = counts.Keys.Max();
            int maxBucketCount = counts.Values.Max();
            for (int value = lowerBound; value <= upperBound; value++)
            {
                histogram.HistogramBars.Add(new HistogramBar(value, counts.GetValueOrDefault(value), maxBucketCount));
            }

            return histogram;
        }

        private static TimeSpan? AverageFloorTime(IReadOnlyCollection<FloorData> floors) => floors.Count == 0
            ? null
            : TimeSpan.FromSeconds(floors.Average(floor => floor.Time.TotalSeconds));

        private static FloorTimeCdfPlot CreateFloorTimeCdf(
            string title,
            IReadOnlyCollection<int> roomTicks,
            TimeSpan? averageFloorTime)
        {
            var plot = new FloorTimeCdfPlot(title, roomTicks.Count, averageFloorTime);
            if (roomTicks.Count == 0)
                return plot;

            // All room ticks are positive, so a sum above the graph's upper bound
            // can never contribute to its CDF. Discard that tail during convolution,
            // retaining the original probabilities (do not renormalize the prefix).
            const int maximumTotalTicks = FloorTimeCdfPlot.MaximumDisplayedTicks;
            var counts = new int[maximumTotalTicks + 1];
            foreach (int ticks in roomTicks)
                if (ticks <= maximumTotalTicks)
                    counts[ticks]++;

            var sumDistribution = new double[maximumTotalTicks + 1];
            var nextDistribution = new double[maximumTotalTicks + 1];
            var floorTimeDistribution = new double[maximumTotalTicks + 1];
            sumDistribution[0] = 1;

            for (int roomCount = 1; roomCount <= 64; roomCount++)
            {
                Array.Clear(nextDistribution);
                for (int ticks = 1; ticks <= maximumTotalTicks; ticks++)
                {
                    if (counts[ticks] == 0)
                        continue;

                    double probability = (double)counts[ticks] / roomTicks.Count;
                    for (int totalTicks = ticks; totalTicks <= maximumTotalTicks; totalTicks++)
                        nextDistribution[totalTicks] += sumDistribution[totalTicks - ticks] * probability;
                }

                (sumDistribution, nextDistribution) = (nextDistribution, sumDistribution);
                if (roomCount < 50)
                    continue;

                double roomCountProbability = roomCount is >= 54 and <= 60 ? 37.0 / 315 : 1.0 / 45;
                for (int totalTicks = 0; totalTicks <= maximumTotalTicks; totalTicks++)
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
        public int SampleCount { get; } = sampleCount;
        public int? FloorCount { get; } = floorCount;
        public string? ThemeCounts => FloorCount.HasValue ? $"Rooms: {SampleCount}   Floors: {FloorCount.Value}" : null;
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

    public partial class FloorTimeCdfPlot(string title, int roomSampleCount, TimeSpan? averageFloorTime) : ObservableObject
    {
        private const double ChartWidth = 520;
        private const double ChartHeight = 300;
        private const double LeftMargin = 52;
        private const double RightMargin = 16;
        private const double TopMargin = 16;
        private const double BottomMargin = 38;
        private const int MinimumDisplayedTicks = 400; // 4:00 at 0.6 seconds per tick.
        internal const int MaximumDisplayedTicks = 900; // 9:00 at 0.6 seconds per tick.
        private double[] _cumulativeProbabilities = [];

        public string Title { get; } = title;
        public string Detail => $"{RoomSampleDetail}   Average recorded floor time: {FormatAverageFloorTime(averageFloorTime)}";
        public PointCollection CdfPoints { get; } = [];
        public string MinimumTimeLabel => "4:00";
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
            // Input is a prefix of an already normalized PMF. Its missing mass
            // belongs beyond the visible range, including when the prefix is all zero.
            CdfPoints.Clear();
            _cumulativeProbabilities = new double[Math.Min(probabilities.Count, MaximumDisplayedTicks + 1)];
            double runningProbability = 0;
            for (int totalTicks = 0; totalTicks < _cumulativeProbabilities.Length; totalTicks++)
            {
                runningProbability += probabilities[totalTicks];
                _cumulativeProbabilities[totalTicks] = Math.Clamp(runningProbability, 0, 1);
            }
            if (!HasDistribution)
                return;

            double plotWidth = ChartWidth - LeftMargin - RightMargin;
            double plotHeight = ChartHeight - TopMargin - BottomMargin;
            double cumulativeProbability = _cumulativeProbabilities[Math.Min(MinimumDisplayedTicks, _cumulativeProbabilities.Length - 1)];

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
                cumulativeProbability = _cumulativeProbabilities[totalTicks];
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
            HoverTooltip = $"Time: {(int)(seconds / 60)}:{seconds % 60:00.0}\nProbability: {probability:P3}";
            HoverMarkerVisibility = Visibility.Visible;
        }

        public void ClearHover()
        {
            HoverMarkerVisibility = Visibility.Collapsed;
        }

        private string RoomSampleDetail => roomSampleCount == 0
            ? "No room-tick data"
            : $"Modeled from {roomSampleCount} room samples; room count 50–64";

        private static string FormatAverageFloorTime(TimeSpan? time)
        {
            if (!time.HasValue)
                return "No data";

            int totalSeconds = (int)Math.Round(time.Value.TotalSeconds);
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
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
