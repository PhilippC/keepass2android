using Android.App;
using Android.Content;
using Android.OS;
using System.Threading.Tasks;
using Thread = Java.Lang.Thread;

namespace keepass2android;

/// <summary>
/// Allows to run tasks in the background. The UI is not blocked by the task. Tasks continue to run in the BackgroundSyncService if the app goes to background while tasks are active.
/// </summary>
public class OperationRunner
{
    //singleton instance
    private static OperationRunner _instance = null;

    public static OperationRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new OperationRunner();
            }

            return _instance;
        }
    }

    void Initialize(IKp2aApp app)
    {

    }

    public struct OperationWithMetadata
    {
        public OperationWithMetadata()
        {
            Operation = null;
        }

        public OperationWithFinishHandler Operation { get; set; }
        public bool RunBlocking { get; set; } = false;
    }

    public ProgressUiAsStatusLoggerAdapter StatusLogger => _statusLogger;

    private OperationRunner()
    {
        //private constructor
    }

    private readonly Queue<OperationWithMetadata> _taskQueue = new Queue<OperationWithMetadata>();
    private readonly object _taskQueueLock = new object();
    private Java.Lang.Thread? _thread = null;
    private OperationWithMetadata? _currentlyRunningTask = null;
    private ProgressUiAsStatusLoggerAdapter _statusLogger = null;
    private IProgressDialog _progressDialog;
    private IKp2aApp _app;

    public void Run(IKp2aApp app, OperationWithFinishHandler operation, bool runBlocking = false)
    {
        Kp2aLog.Log("OPR: Run: " + operation.GetType().Name + ", runBlocking: " + runBlocking);
        lock (Instance._taskQueueLock)
        {
            _taskQueue.Enqueue(new OperationWithMetadata(){ Operation = operation, RunBlocking = runBlocking});
            operation.SetStatusLogger(_statusLogger);

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
                                Kp2aLog.Log("OPR: task queue empty. Stopping operation runner thread.");
                                break;
                            }
                            else
                            {
                                _currentlyRunningTask = _taskQueue.Dequeue();
                            }
                        }

                        if (_currentlyRunningTask.Value.RunBlocking)
                        {
                            Kp2aLog.Log("OPR: Run. Posting to set up progress dialog for blocking task: " + _currentlyRunningTask?.Operation?.GetType()?.Name ?? "null");
                            app.UiThreadHandler.Post(
                                () =>
                                {
                                    Kp2aLog.Log("OPR: Run. Starting Setting up progress dialog for blocking task: " + _currentlyRunningTask?.Operation?.GetType()?.Name ?? "null");
                                    TrySetupProgressDialog();
                                    Kp2aLog.Log("OPR: Run. Finished Setting up progress dialog for blocking task: " + _currentlyRunningTask?.Operation?.GetType()?.Name ?? "null");
                                });
                        }

                        var originalFinishedHandler = _currentlyRunningTask.Value.Operation.operationFinishedHandler;
                        _currentlyRunningTask.Value.Operation.operationFinishedHandler = new ActionOnOperationFinished(app, (
                            (success, message, context) =>
                            {
                                if (_currentlyRunningTask?.RunBlocking == true)
                                {
                                    Kp2aLog.Log("OPR: Run. Blocking task finished: " + _currentlyRunningTask?.Operation?.GetType()?.Name ?? "null");
                                    _app.UiThreadHandler.Post(() =>
                                    {
                                        Kp2aLog.Log("OPR: Starting Dismissing progress dialog");
                                        _progressDialog?.Dismiss();
                                        Kp2aLog.Log("OPR: Finished Dismissing progress dialog");
                                    }
                                        );
                                }
                                Kp2aLog.Log("OPR: Run. Finished handler called for task: " + _currentlyRunningTask?.Operation?.GetType()?.Name ?? "null");
                                _currentlyRunningTask = null;

                            }), originalFinishedHandler);
                        Kp2aLog.Log("OPR: starting to run " + _currentlyRunningTask?.Operation?.GetType()?.Name ?? "null");
                        _currentlyRunningTask.Value.Operation.Run();
                        
                        while (_currentlyRunningTask != null)
                        {
                            try
                            {
                                Thread.Sleep(100);
                            }
                            catch (Exception e)
                            {
                                Kp2aLog.Log("Thread interrupted.");
                            }
                        }
                        Kp2aLog.Log("OPR: waiting for next task in queue...");
                    }

                });
                _thread.Start();
            }
            else Kp2aLog.Log("OPR: thread already running, only enqueued " + operation.GetType().Name );


        }
               
    }


    private bool TrySetupProgressDialog()
    {
        Kp2aLog.Log("OPR: TrySetupProgressDialog");
        string currentMessage = "Initializing...";
        string currentSubmessage = "";

        if (_statusLogger != null)
        {
            currentMessage = _statusLogger.LastMessage;
            currentSubmessage = _statusLogger.LastSubMessage;
        }

        if (_progressDialog != null)
        {
            var pd = _progressDialog;
            _app.UiThreadHandler.Post(() =>
            {
                Kp2aLog.Log("OPR: Starting TrySetupProgressDialog: Dismissing existing progress dialog");
                pd.Dismiss();
                Kp2aLog.Log("OPR: Finished TrySetupProgressDialog: Dismissing existing progress dialog");
            });
        }

        // Show process dialog
        _progressDialog = _app.CreateProgressDialog(_app.ActiveContext);
        if (_progressDialog == null)
        {
            Kp2aLog.Log("OPR: OperationRunner.TrySetupProgressDialog: _progressDialog is null");
            return false;
        }
    

        var progressUi = new ProgressDialogUi(_app, _app.UiThreadHandler, _progressDialog);
        _statusLogger.SetNewProgressUi(progressUi);

        _statusLogger.StartLogging("", false);
        _statusLogger.UpdateMessage(currentMessage);
        _statusLogger.UpdateSubMessage(currentSubmessage);
        return true;
    }

    public void SetNewActiveContext(IKp2aApp app)
    {
        Kp2aLog.Log("OPR: SetNewActiveContext: " + app.ActiveContext?.GetType().Name);
        _app = app;
        Context? context = app.ActiveContext;
        bool isAppContext = context == null || (context.ApplicationContext == context);
        lock (_taskQueueLock)
        {
            if (isAppContext && _thread != null)
            {
                //this will register the service as new active context (see BackgroundSyncService.OnStartCommand())
                app.StartBackgroundSyncService();
                return;
            }

            if (_currentlyRunningTask?.RunBlocking == true && (context is Activity { IsFinishing: false, IsDestroyed:false}))
            {
                Kp2aLog.Log("OPR: SetNewActiveContext: running blocking task, setting up progress dialog");
                app.UiThreadHandler.Post(() =>
                {
                    Kp2aLog.Log("OPR: Starting posted TrySetupProgressDialog");
                    TrySetupProgressDialog();
                    Kp2aLog.Log("OPR: Finished posted TrySetupProgressDialog");
                });
            }
            else
            {
                var progressUi = (context as IProgressUiProvider)?.ProgressUi;
                if (_statusLogger == null)
                {
                    _statusLogger = new ProgressUiAsStatusLoggerAdapter(progressUi, app);
                }
                else
                {
                    _statusLogger.SetNewProgressUi(progressUi);
                }
            }

            foreach (var task in _taskQueue.Concat(_currentlyRunningTask == null ? 
                         new List<OperationWithMetadata>() : [_currentlyRunningTask.Value])
                    )
            {
                task.Operation.SetStatusLogger(_statusLogger);
            }
                
        }


    }

    public void CancelAll()
    {
        lock (_taskQueueLock)
        {
            if (_thread != null)
            {
                _thread.Interrupt();
                _thread = null;
                _statusLogger?.EndLogging();
            }

            _taskQueue.Clear();
            _currentlyRunningTask = null;
        }
    }
}