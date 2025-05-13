using Android.Content;
using Thread = Java.Lang.Thread;

namespace keepass2android;

/// <summary>
/// Allows to run tasks in the background. The UI is not blocked by the task. Tasks continue to run in the BackgroundSyncService if the app goes to background while tasks are active.
/// </summary>
public class BackgroundOperationRunner
{
    //singleton instance
    private static BackgroundOperationRunner _instance = null;

    public static BackgroundOperationRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new BackgroundOperationRunner();
            }

            return _instance;
        }
    }

    public ProgressUiAsStatusLoggerAdapter StatusLogger => _statusLogger;

    private BackgroundOperationRunner()
    {
        //private constructor
    }

    private readonly Queue<OperationWithFinishHandler> _taskQueue = new Queue<OperationWithFinishHandler>();
    private readonly object _taskQueueLock = new object();
    private Java.Lang.Thread? _thread = null;
    private OperationWithFinishHandler? _currentlyRunningTask = null;
    private ProgressUiAsStatusLoggerAdapter _statusLogger = null;

    public void Run(IKp2aApp app, OperationWithFinishHandler operation)
    {
        lock (Instance._taskQueueLock)
        {
            _taskQueue.Enqueue(operation);
            SetNewActiveContext(app);

            // Start thread to run the task (unless it's already running)
            if (_thread == null)
            {
                _statusLogger.StartLogging("", false);
                _thread = new Java.Lang.Thread(() =>
                {
                    while (true)
                    {
                            
                        lock (_taskQueueLock)
                        {
                            if (!_taskQueue.Any())
                            {
                                _thread = null;
                                _statusLogger.EndLogging();
                                break;
                            }
                            else
                            {
                                _currentlyRunningTask = _taskQueue.Dequeue();
                            }
                        }

                        var originalFinishedHandler = _currentlyRunningTask.operationFinishedHandler;
                        _currentlyRunningTask.operationFinishedHandler = new ActionOnOperationFinished(app, (
                            (success, message, context) =>
                            {
                                _currentlyRunningTask = null;
                            }), originalFinishedHandler);
                        _currentlyRunningTask.Run();
                        while (_currentlyRunningTask != null)
                        {
                            Thread.Sleep(100);
                        }
                    }

                });
                _thread.Start();
            }
                
        }
               
    }

    public void SetNewActiveContext(IKp2aApp app)
    {
        Context? context = app.ActiveContext;
        lock (_taskQueueLock)
        {
            if (context == null && _thread != null)
            {
                //this will register the service as new active context
                app.StartBackgroundSyncService();
                return;
            }

            var progressUi = (context as IProgressUiProvider)?.ProgressUi;
            if (_statusLogger == null)
            {
                _statusLogger = new ProgressUiAsStatusLoggerAdapter(progressUi, app);
            }
            else
            {
                _statusLogger.SetNewProgressUi(progressUi);
            }
					
            foreach (var task in _taskQueue.Concat(_currentlyRunningTask == null ? 
                         new List<OperationWithFinishHandler>() : 
                         new List<OperationWithFinishHandler>() { _currentlyRunningTask })
                    )
            {
                task.SetStatusLogger(_statusLogger);
            }
                
        }


    }
}