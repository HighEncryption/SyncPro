namespace SyncPro.Counters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using SyncPro.Data;

    public static class DimensionNames
    {
        public const string RelationshipGuid = "RelationshipGuid";
        public const string SyncJobId = "SyncJobId";
    }

    public struct CounterDimension
    {
        public string Key { get; }
        public string Value { get; }

        public CounterDimension(string key, string value)
        {
            this.Key = key;
            this.Value = value;
        }
    }

    public class CounterEmit
    {
        public string Name { get; set; }

        public long Value { get; set; }

        public DateTime Timestamp { get; set; }

        public SortedDictionary<string, string> Dimensions { get; set; }

        public string GetCounterName()
        {
            if (Dimensions == null || !Dimensions.Any())
            {
                return Name;
            }

            StringBuilder sb = new StringBuilder(Name);

            foreach (KeyValuePair<string, string> dimension in Dimensions)
            {
                sb.AppendFormat("|{0}:{1}", dimension.Key, dimension.Value);
            }

            return sb.ToString();
        }

        public bool IsMatch(Data.CounterInstanceData instance)
        {
            if (instance.Name != this.Name)
            {
                return false;
            }

            if (instance.Dimensions.Count != this.Dimensions.Count)
            {
                return false;
            }

            foreach (CounterDimensionData counterDimension in instance.Dimensions)
            {
                if (!this.Dimensions.TryGetValue(counterDimension.Name, out string dimValue))
                {
                    return false;
                }

                if (dimValue != counterDimension.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public struct CounterValueSet
    {
        public long Sum { get; set; }

        public uint Count { get; set; }

        public CounterValueSet(long sum, uint count)
        {
            this.Sum = sum;
            this.Count = count;
        }
    }
}
