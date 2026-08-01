using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Dungeons2.ViewModels
{
    public partial class StatsViewModel : ObservableObject
    {
        public ObservableCollection<HistogramBar> HistogramBars { get; } = [];

        public int MatchingFloorCount { get; private set; }

        public int MatchingRoomCount { get; private set; }

        public double AverageTicks { get; private set; }

        public double StandardDeviationTicks { get; private set; }

        public string Summary => MatchingFloorCount == 0
            ? "No solo large floors with 100% bonus rooms opened have been recorded yet."
            : $"Showing {MatchingRoomCount} rooms from {MatchingFloorCount} solo large floors with 100% bonus rooms opened.";

        public StatsViewModel()
        {
            DungeonStore.Instance.FloorHistory.CollectionChanged += FloorHistory_CollectionChanged;
            RefreshHistogram();
        }

        private void FloorHistory_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshHistogram();

        private void RefreshHistogram()
        {
            var matchingTicks = DungeonStore.Instance.FloorHistory
                .Where(floor => floor.PartySize == 1 && floor.Bonus == 1 && floor.Size == "Large").SelectMany(floor => floor.RoomTicks).Select(split => split.Ticks).Where(ticks => ticks > 0).ToList();

            MatchingFloorCount = DungeonStore.Instance.FloorHistory.Count(floor => floor.PartySize == 1 && floor.Bonus == 1 && floor.Size == "Large");
            MatchingRoomCount = matchingTicks.Count;
            HistogramBars.Clear();

            if (matchingTicks.Count == 0)
            {
                AverageTicks = 0;
                StandardDeviationTicks = 0;
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(AverageTicks));
                OnPropertyChanged(nameof(StandardDeviationTicks));
                return;
            }

            AverageTicks = matchingTicks.Average();
            var variance = matchingTicks.Sum(tick => Math.Pow(tick - AverageTicks, 2)) / matchingTicks.Count;
            StandardDeviationTicks = Math.Sqrt(variance);

            const int minTick = 1;
            const int maxTick = 100;
            const int bucketSize = 1;
            const int bucketCount = (maxTick - minTick) / bucketSize + 1;

            int[] counts = new int[bucketCount];
            foreach (int value in matchingTicks)
            {
                if (value < minTick || value > maxTick)
                    continue;

                int index = value - minTick;
                counts[index]++;
            }

            int maxCount = counts.Max();
            for (int i = 0; i < bucketCount; i++)
            {
                int bucketStart = minTick + i;
                int bucketEnd = bucketStart;
                int count = counts[i];
                HistogramBars.Add(new HistogramBar(
                    bucketStart.ToString(),
                    bucketEnd.ToString(),
                    count,
                    maxCount));
            }

            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(AverageTicks));
            OnPropertyChanged(nameof(StandardDeviationTicks));
        }
    }

    public class HistogramBar(string lowerBound, string upperBound, int count, int maxCount)
    {
        public string LowerBound { get; } = lowerBound;
        public string UpperBound { get; } = upperBound;
        public int Count { get; } = count;
        public int MaxCount { get; } = maxCount;

        public string Label => LowerBound;
        public double BarHeight => MaxCount <= 0 ? 0 : 220.0 * Count / MaxCount;
        public string CountLabel => Count.ToString();
    }
}
