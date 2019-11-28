namespace SyncPro.Cmdlets.Commands
{
    using System;
    using System.Management.Automation;
    using System.Threading;

    using SyncPro.Runtime;

    [Cmdlet(VerbsLifecycle.Start, "SyncProAnalyzeJob")]
    public class StartAnalyzeJob : PSCmdlet
    {
        [Parameter]
        [Alias("Rid")]
        public Guid RelationshipId { get; set; }

        [Parameter]
        public SwitchParameter Resync { get; set; }

        [Parameter]
        public SwitchParameter SkipHashCheck { get; set; }

        protected override void ProcessRecord()
        {
            SyncRelationship relationship = CmdletCommon.GetSyncRelationship(this.RelationshipId);

            if (relationship.ActiveAnalyzeJob != null)
            {
                throw new ItemNotFoundException("There is already an active analyze job for this relationship");
            }

            var newJob = relationship.BeginAnalyzeJob(false);
            newJob.Resync = this.Resync.ToBool();
            newJob.SkipHashCheck = this.SkipHashCheck.ToBool();

            relationship.ActiveAnalyzeJob = newJob;

            newJob.Start();

            var psRun = new PSAnalyzeJob(relationship.ActiveAnalyzeJob);

            this.WriteObject(psRun);
        }
    }

    [Cmdlet(VerbsCommon.Watch, "SyncProAnalyzeJob")]
    public class WatchAnalyzeJob : PSCmdlet
    {
        [Parameter]
        [Alias("Rid")]
        public Guid RelationshipId { get; set; }

        protected override void ProcessRecord()
        {
            AnalyzeJob job = null;

            if (this.RelationshipId != Guid.Empty)
            {
                SyncRelationship relationship = CmdletCommon.GetSyncRelationship(this.RelationshipId);

                if (relationship.ActiveAnalyzeJob != null)
                {
                    job = relationship.ActiveAnalyzeJob;
                }
            }

            if (job == null)
            {
                return;
            }

            object syncLock = new object();

            ProgressRecord progressRecord = null;
            bool progressChanged = false;

            job.ProgressChanged += (sender, info) =>
            {
                lock (syncLock)
                {
                    progressRecord =
                        new ProgressRecord(
                            1,
                            "Analyzing changes",
                            info.Activity)
                        {
                            PercentComplete = info.ProgressValue != null
                                ? Convert.ToInt32(info.ProgressValue.Value * 100)
                                : 0
                        };

                    progressChanged = true;
                }
            };


            while (!job.HasFinished)
            {
                lock (syncLock)
                {
                    if (progressChanged)
                    {
                        this.WriteProgress(progressRecord);
                        progressChanged = false;
                    }

                    Thread.Sleep(100);
                }
            }

            //ManualResetEventSlim waitEvent = new ManualResetEventSlim(false);

            //job.Finished += (sender, args) => { waitEvent.Set(); };

            //job.ProgressChanged += (sender, info) =>
            //{
            //    this.WriteProgress(
            //        new ProgressRecord(
            //            1,
            //            "Analyzing changes",
            //            info.Activity)
            //        {
            //            PercentComplete =  info.ProgressValue != null 
            //                ? Convert.ToInt32(info.ProgressValue.Value * 100) 
            //                : 0
            //        });
            //};

            //waitEvent.Wait();

            //ProgressRecord myprogress = new ProgressRecord(1, "Testing", "Progress:");

            //for (int i = 0; i < 100; i++)
            //{
            //    myprogress.PercentComplete = i;
            //    System.Threading.Thread.Sleep(100);
            //    WriteProgress(myprogress);
            //}

            //WriteObject("Done.");


        }
    }
}