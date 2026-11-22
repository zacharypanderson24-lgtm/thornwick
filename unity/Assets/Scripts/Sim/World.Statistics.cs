using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrownAndHearth.Sim
{
    public sealed class SettlementStatistics
    {
        readonly double[] _resources;
        public int Day { get; }
        public int Population { get; }

        internal SettlementStatistics(int day, int population, double[] resources)
        {
            Day = day;
            Population = population;
            _resources = resources;
        }

        public double Stockpile(Res resource) => _resources[(int)resource];
    }

    public partial class World
    {
        static readonly Res[] RecordedResources = (Res[])Enum.GetValues(typeof(Res));
        readonly List<SettlementStatistics> _statistics = new List<SettlementStatistics>();
        public IReadOnlyList<SettlementStatistics> Statistics => _statistics;

        public SettlementStatistics CurrentStatistics() => CaptureStatistics(Day);

        SettlementStatistics CaptureStatistics(int day) =>
            new SettlementStatistics(day, Peasants + Soldiers, RecordedResources.Select(resource => ResAmt[resource]).ToArray());

        static JArray EncodeStatistics(SettlementStatistics sample)
        {
            var row = new JArray(sample.Day, sample.Population);
            foreach (var resource in RecordedResources) row.Add(sample.Stockpile(resource));
            return row;
        }

        void LoadStatistics(JArray rows)
        {
            if (rows == null) return;
            int previous = 0;
            foreach (var item in rows)
            {
                if (!(item is JArray row) || (row.Count != 2 + RecordedResources.Length &&
                    row.Count != 2 + (int)Res.Wool && row.Count != 2 + (int)Res.Charcoal && row.Count != 2 + (int)Res.Metal))
                    throw new InvalidDataException("Invalid statistics entry.");
                int day = row[0].Value<int>();
                int population = row[1].Value<int>();
                var resources = new double[RecordedResources.Length];
                for (int i = 2; i < row.Count; i++) resources[i - 2] = row[i].Value<double>();
                if (day <= previous || day >= Day || population < 0 ||
                    resources.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
                    throw new InvalidDataException("Invalid statistics entry.");
                _statistics.Add(new SettlementStatistics(day, population, resources));
                previous = day;
            }
        }
    }
}
