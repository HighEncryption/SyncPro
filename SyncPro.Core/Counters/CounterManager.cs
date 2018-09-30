namespace SyncPro.Counters
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Data;
    using SyncPro.Runtime;

    public static class CounterManager
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

        private static readonly TimeSpan FlushDelay = TimeSpan.FromSeconds(5);

        // The queue of counter values that need to be processed
        private static BlockingCollection<CounterEmit> counterEmitQueue =
            new BlockingCollection<CounterEmit>();

        private static ConcurrentDictionary<string, CounterInstance> instanceByNameCache =
            new ConcurrentDictionary<string, CounterInstance>();

        private static Task processingTask;

        public static void Start()
        {
            string databasePath = CounterDatabase.GetDatabaseFilePath();
            string folderPath = Path.GetDirectoryName(databasePath);

            // Create the directory containing the counters database (if it does not exist).
            Pre.Assert(folderPath != null, "folderPath != null");
            Directory.CreateDirectory(folderPath);

            processingTask = Task.Run(async () => { await ProcessCounterEmitsMain(); });
        }

        public static void Stop()
        {
            counterEmitQueue.CompleteAdding();

            // Wait for the counters that have been added to the queue already to finish processing
            processingTask.Wait();
        }

        public static void LogCounter(
            string name,
            long value,
            params CounterDimension[] dimensions)
        {
            if (counterEmitQueue.IsAddingCompleted)
            {
                // A counter value was emitted after we were told to cancel. There isn't much we can
                // do to recover, and throwing an exception isn't helpful, so just suppress
                return;
            }

            SortedDictionary<string, string> dimsDictionary =
                new SortedDictionary<string, string>();

            foreach (CounterDimension dimension in dimensions)
            {
                dimsDictionary.Add(dimension.Key, dimension.Value);
            }

            LogCounterInternal(name, value, dimsDictionary);
        }

        public static void LogSyncJobCounter(
            string name,
            long value,
            params CounterDimension[] dimensions)
        {
            if (counterEmitQueue.IsAddingCompleted)
            {
                // A counter value was emitted after we were told to cancel. There isn't much we can
                // do to recover, and throwing an exception isn't helpful, so just suppress
                return;
            }

            SortedDictionary<string, string> dimsDictionary =
                new SortedDictionary<string, string>();

            foreach (CounterDimension dimension in dimensions)
            {
                dimsDictionary.Add(dimension.Key, dimension.Value);
            }

            SyncJobContext currentJobContext = SyncJobContext.Current;
            if (currentJobContext != null)
            {
                dimsDictionary.Add(
                    DimensionNames.RelationshipGuid, 
                    currentJobContext.RelationshipGuid.ToString());

                dimsDictionary.Add(
                    DimensionNames.SyncJobId, 
                    currentJobContext.JobId.ToString());
            }

            LogCounterInternal(name, value, dimsDictionary);
        }

        private static void LogCounterInternal(
            string name,
            long value,
            SortedDictionary<string, string> dimensions)

        {
            counterEmitQueue.Add(
                new CounterEmit()
                {
                    Name = name,
                    Value = value,
                    Timestamp = DateTime.UtcNow,
                    Dimensions = dimensions
                });
        }

        private static async Task ProcessCounterEmitsMain()
        {
            Stopwatch lastFlushTime = Stopwatch.StartNew();

            // Continue processing items from the collection until we are finished adding items
            // and all of the items have been processed.
            while (!counterEmitQueue.IsCompleted)
            {
                // Attempt to take an emit from the collection.
                // This is a blocking call, so we will be dedicating a thread to this. Perhaps this 
                // can be converted to an async call in the future.
                CounterEmit emit;
                if (counterEmitQueue.TryTake(out emit, 1000))
                {
                    // We were able to retrive an item.
                    ProcessCounterEmit(emit);
                }

                if (lastFlushTime.Elapsed > FlushDelay)
                {
                    FlushCountersToDatabase();
                }
            }
        }

        private static void FlushCountersToDatabase()
        {
            DateTime now = DateTime.UtcNow;
            long timestampThreadhold = Convert.ToInt64(Math.Floor(now.Subtract(Epoch).TotalSeconds)) - 2;

            using (var db = new CounterDatabase())
            {
                bool valuesAdded = false;
                foreach (string counterName in instanceByNameCache.Keys)
                {
                    CounterInstance instance = instanceByNameCache[counterName];

                    // Push any values older than the threshold into the db and remove from the cache
                    List<long> removeList = new List<long>();
                    foreach (long timestamp in instance.Values.Keys)
                    {
                        if (timestamp < timestampThreadhold)
                        {
                            CounterValueSet valueSet = instance.Values[timestamp];
                            db.Values.Add(
                                new CounterValueData()
                                {
                                    Count = valueSet.Count,
                                    CounterInstanceId = instance.Id,
                                    Flags = (int) CounterValueFlags.Aggregate1Second,
                                    Timestamp = timestamp,
                                    Value = valueSet.Sum
                                });

                            // Remove the value set from the instance, since we already pushed it to the DB
                            removeList.Add(timestamp);
                            valuesAdded = true;
                        }
                    }

                    foreach (long timestamp in removeList)
                    {
                        instance.Values.Remove(timestamp);
                    }

                    if (instance.CacheExpiryDateTime < now && !instance.Values.Any())
                    {
                        instanceByNameCache.TryRemove(counterName, out CounterInstance _);
                    }
                }

                if (valuesAdded)
                {
                    db.SaveChanges();
                }
            }
        }

        private static void ProcessCounterEmit(CounterEmit emit)
        {
            // This is an expensive call, so make sure that we only make it once
            string counterName = emit.GetCounterName();
            int hashCode = CounterInstance.GetCounterHashCode(emit.Name, emit.Dimensions);

            CounterInstance instance;
            if (!instanceByNameCache.TryGetValue(counterName, out instance))
            {
                // The counter is not in the cache, so we need to get/create is in the database
                using (var db = new CounterDatabase())
                {
                    if (!TryGetInstanceFromDatabase(emit, hashCode, db, out instance))
                    {
                        CounterInstanceData counterInstanceData =
                            new CounterInstanceData()
                            {
                                Name = emit.Name,
                                InstanceHashCode = hashCode
                            };

                        if (counterInstanceData.Dimensions == null)
                        {
                            counterInstanceData.Dimensions = new List<CounterDimensionData>();
                        }

                        foreach (KeyValuePair<string, string> emitDimension in emit.Dimensions)
                        {
                            counterInstanceData.Dimensions.Add(
                                new CounterDimensionData(emitDimension.Key, emitDimension.Value));
                        }

                        db.Instances.Add(counterInstanceData);
                        db.SaveChanges();

                        instance = new CounterInstance(counterInstanceData);
                    }

                    // Add the newly created/retrieved counter instance to the cache
                    // TODO: Where does the TTL go???
                    instanceByNameCache.TryAdd(counterName, instance);
                }
            }

            long ts = Convert.ToInt64(Math.Floor(emit.Timestamp.Subtract(Epoch).TotalSeconds));

            CounterValueSet valueSet;
            if (!instance.Values.TryGetValue(ts, out valueSet))
            {
                valueSet = new CounterValueSet(emit.Value, 1);
                instance.Values.Add(ts, valueSet);
                return;
            }

            valueSet.Sum += emit.Value;
            valueSet.Count++;
        }

        private static bool TryGetInstanceFromDatabase(
            CounterEmit emit, 
            int emitHashCode,
            CounterDatabase db,
            out CounterInstance instance)
        {
            var instances = db.Instances
                .Where(i => i.InstanceHashCode == emitHashCode)
                .Include(i => i.Dimensions);

            foreach (CounterInstanceData counterInstance in instances)
            {
                if (emit.IsMatch(counterInstance))
                {
                    instance = new CounterInstance(counterInstance);
                    return true;
                }
            }

            instance = null;
            return false;
        }
    }
}