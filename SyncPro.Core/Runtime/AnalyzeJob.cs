namespace SyncPro.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using SyncPro.Adapters;

    public class AnalyzeJob : JobBase
    {
        public EventHandler<AnalyzeJobProgressInfo> ProgressChanged;

        public AnalyzeRelationshipResult AnalyzeResult { get; }

        public AnalyzeJob(SyncRelationship relationship)
            : base(relationship)
        {
            this.AnalyzeResult = new AnalyzeRelationshipResult();
            this.ProgressChanged += (sender, info) => { };
        }

        /// <summary>
        /// Entry point the the analyze logic
        /// </summary>
        protected override async Task ExecuteTask()
        {
            List<Task> updateTasks = new List<Task>();
            List<AnalyzeJobWorker> workers = new List<AnalyzeJobWorker>();

            // For each adapter (where changes can originate from), start a task to analyze the change
            // originating from that adapter that would apply to every other adapter. This will allow 
            // multiple adapters to be examined in parallel.
            foreach (AdapterBase adapter in this.Relationship.Adapters.Where(a => a.Configuration.IsOriginator))
            {
                foreach (AdapterBase destAdapter in this.Relationship.Adapters.Where(a => a != adapter))
                {
                    AnalyzeJobWorker worker =
                        new AnalyzeJobWorker(
                            this.Relationship,
                            adapter,
                            destAdapter,
                            this.AnalyzeResult,
                            this.Resync,
                            this.SkipHashCheck,
                            this.CancellationToken);

                    worker.ProgressChanged += (sender, info) => this.ProgressChanged(sender, info);

                    workers.Add(worker);
                }
            }

            foreach (AnalyzeJobWorker worker in workers)
            {
                updateTasks.Add(worker.AnalyzeChangesAsync());
            }

            // Wait until all of the tasks are complete
            await Task.WhenAll(updateTasks).ContinueWith(task =>
            {
                if (updateTasks.All(t => t.IsCompleted))
                {
                    this.AnalyzeResult.IsComplete = true;
                }

                this.ProgressChanged?.Invoke(
                    this, 
                    new AnalyzeJobProgressInfo(
                        "Analysis Complete",
                        1.0,
                        0));
            });

            // The analyze logic only determines the number of files that are new or have changed. Once
            // analysis completes, calculate the number of files that are unchanged.
            foreach (AnalyzeAdapterResult adapterResult in this.AnalyzeResult.AdapterResults.Values)
            {
                this.AnalyzeResult.UnchangedFileCount += adapterResult.UnchangedFileCount;
                this.AnalyzeResult.UnchangedFolderCount += adapterResult.UnchangedFolderCount;
                this.AnalyzeResult.UnchangedFileBytes += adapterResult.UnchangedFileBytes;
            }
        }
    }

    public enum HashType
    {
        None,
        SHA1,
        MD5
    }
}