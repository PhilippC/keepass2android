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
            // Perform synchronization using the preferred OperationRunner pattern
            OperationRunner.Instance.Run(App.Kp2a, new KeeShareSyncOperation(App.Kp2a, App.Kp2a.CurrentDb.KpDatabase, true, true,
                new ActionOnOperationFinished(App.Kp2a, (success, message, context) =>
                {
                    JobFinished(@params, !success);
                })));

            return true; // Operation is running in background
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
