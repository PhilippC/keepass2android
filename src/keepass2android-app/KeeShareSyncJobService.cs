using System;
using System.Threading.Tasks;
using Android.App;
using Android.App.Job;
using Android.Content;
using Android.OS;
using keepass2android.KeeShare;

namespace keepass2android
{
    [Service(Name = "keepass2android.KeeShareSyncJobService", Permission = "android.permission.BIND_JOB_SERVICE")]
    public class KeeShareSyncJobService : JobService
    {
        public const int JobId = 3130; // PR number as ID :)

        public override bool OnStartJob(JobParameters @params)
        {
            // Run sync in background thread
            Task.Run(async () =>
            {
                bool success = false;
                try
                {
                    if (App.Kp2a.CurrentDb != null && App.Kp2a.CurrentDb.KpDatabase != null)
                    {
                        Kp2aLog.Log("KeeShareSyncJob: Starting background sync...");
                        
                        // We need to disable UI interactions for background sync
                        var results = KeeShareImporter.CheckAndImport(App.Kp2a.CurrentDb, null, null);
                        
                        // Also trigger export if needed (though export is usually on-save)
                        KeeShareExporter.CheckAndExport(App.Kp2a.CurrentDb.KpDatabase, null);

                        Kp2aLog.Log($"KeeShareSyncJob: Completed. Processed {results.Count} groups.");
                        success = true;
                    }
                    else
                    {
                        Kp2aLog.Log("KeeShareSyncJob: Database not open, skipping sync.");
                    }
                }
                catch (Exception ex)
                {
                    Kp2aLog.Log("KeeShareSyncJob: Error during sync: " + ex.Message);
                }
                finally
                {
                    JobFinished(@params, !success); // Reschedule if failed
                }
            });

            return true; // Work is ongoing
        }

        public override bool OnStopJob(JobParameters @params)
        {
            Kp2aLog.Log("KeeShareSyncJob: Stopped by system.");
            return true; // Retry later
        }

        public static void ScheduleJob(Context context)
        {
            var componentName = new ComponentName(context, "keepass2android.KeeShareSyncJobService");
            var jobInfo = new JobInfo.Builder(JobId, componentName)
                .SetRequiredNetworkType(NetworkType.Connected)
                .SetPeriodic(15 * 60 * 1000) // 15 minutes
                .SetPersisted(true)
                .Build();

            var scheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
            var result = scheduler.Schedule(jobInfo);
            
            if (result == JobScheduler.ResultSuccess)
                Kp2aLog.Log("KeeShareSyncJob: Scheduled successfully.");
            else
                Kp2aLog.Log("KeeShareSyncJob: Failed to schedule.");
        }
    }
}
