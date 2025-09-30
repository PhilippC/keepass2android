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
                                break;
                            }
                            else
                            {
                                _currentlyRunningTask = _taskQueue.Dequeue();
                            }
                        }

                        if (_currentlyRunningTask.Value.RunBlocking)
                        {
                            app.UiThreadHandler.Post(
                                () =>
                                {
                                    TrySetupProgressDialog();
                                });
                        }

                        var originalFinishedHandler = _currentlyRunningTask.Value.Operation.operationFinishedHandler;
                        _currentlyRunningTask.Value.Operation.operationFinishedHandler = new ActionOnOperationFinished(app, (
                            (success, message, context) =>
                            {
                                if (_currentlyRunningTask?.RunBlocking == true)
                                {
                                    _app.UiThreadHandler.Post(() =>
                                    {
                                        _progressDialog?.Dismiss();
                                    }
                                        );
                                }
                                _currentlyRunningTask = null;

                            }), originalFinishedHandler);
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
                    }

                });
                _thread.Start();
            }
            


        }
               
    }


    private bool TrySetupProgressDialog()
    {
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
                pd.Dismiss();
            });
        }

        // Show process dialog
        _progressDialog = _app.CreateProgressDialog(_app.ActiveContext);
        if (_progressDialog == null)
        {
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
                app.UiThreadHandler.Post(() =>
                {
                    TrySetupProgressDialog();
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