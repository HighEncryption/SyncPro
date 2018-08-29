namespace SyncPro.Counters
{
    using System;
    using System.Collections.Generic;

    public class CounterInstance
    {
        private static readonly TimeSpan InstanceTTL = TimeSpan.FromMinutes(5);

        public long Id { get; }

        public string Name { get; }

        public Dictionary<long, CounterValueSet> Values { get; }

        public DateTime CacheExpiryDateTime { get; }

        public CounterInstance()
        {
            this.Values = new Dictionary<long, CounterValueSet>();
            this.CacheExpiryDateTime = DateTime.UtcNow.Add(InstanceTTL);
        }

        public CounterInstance(Data.CounterInstanceData counterInstance)
            : this()
        {
            this.Id = counterInstance.Id;
            this.Name = counterInstance.Name;
        }

        public static int GetCounterHashCode(
            string name,
            IDictionary<string, string> dimensions)
        {
            int hashCode = name.GetHashCode();
            foreach (KeyValuePair<string, string> pair in dimensions)
            {
                hashCode += pair.Key.GetHashCode() + pair.Value.GetHashCode();
            }

            return hashCode;
        }
    }
}