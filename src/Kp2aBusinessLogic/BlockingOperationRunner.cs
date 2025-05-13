/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using Android.App;
using Android.Content;
using Android.OS;
using Java.Lang;
using Java.Security;
using KeePassLib.Interfaces;
using System.Threading.Tasks;
using Enum = System.Enum;
using Thread = Java.Lang.Thread;

namespace keepass2android
{
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

        private BackgroundOperationRunner()
        {
            //private constructor
        }

		private readonly Queue<OperationWithFinishHandler> _taskQueue = new Queue<OperationWithFinishHandler>();
		private readonly object _taskQueueLock = new object();
		private Java.Lang.Thread? _thread = null;
        private ProgressUiAsStatusLoggerAdapter _statusLogger = null;

        public void Run(Context context, IKp2aApp app, OperationWithFinishHandler operation)
        {
            lock (Instance._taskQueueLock)
            {
                _taskQueue.Enqueue(operation);
                SetNewActiveContext(context, app);

                // Start thread to run the task (unless it's already running)
                if (_thread == null)
                {
                    _statusLogger.StartLogging("", false);
                    _thread = new Java.Lang.Thread(() =>
                    {
                        while (true)
                        {
                            OperationWithFinishHandler task;
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
                                    task = _taskQueue.Dequeue();
                                }
                            }
                            task.Run();
                        }

                    });
                    _thread.Start();
                }
                
            }
               
        }

        public void SetNewActiveContext(Context? context, IKp2aApp app)
        {
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
					
                foreach (var task in _taskQueue)
                {
					task.ActiveContext = context;
					task.SetStatusLogger(_statusLogger);
                }
                
            }


        }
    }

    public class ProgressUiAsStatusLoggerAdapter : IKp2aStatusLogger
    {
        private IProgressUi? _progressUi;
        private readonly IKp2aApp _app;

        private string _lastMessage = "";
        private string _lastSubMessage = "";
        private bool _isVisible = false;

        public ProgressUiAsStatusLoggerAdapter(IProgressUi progressUi, IKp2aApp app)
        {
            _progressUi = progressUi;
            _app = app;
        }

        public void SetNewProgressUi(IProgressUi progressUi)
        {
            _progressUi = progressUi;
            if (_isVisible)
            {
                progressUi?.Show();
                progressUi?.UpdateMessage(_lastMessage);
                progressUi?.UpdateSubMessage(_lastSubMessage);
            }
            else
            {
                progressUi?.Hide();
            }
        }

        public void StartLogging(string strOperation, bool bWriteOperationToLog)
        {
            _progressUi?.Show();
            _isVisible = true;
        }

        public void EndLogging()
        {
            _progressUi?.Hide();
            _isVisible = false;
        }

        public bool SetProgress(uint uPercent)
        {
            return true;
        }

        public bool SetText(string strNewText, LogStatusType lsType)
        {
            if (strNewText.StartsWith("KP2AKEY_"))
            {
                UiStringKey key;
                if (Enum.TryParse(strNewText.Substring("KP2AKEY_".Length), true, out key))
                {
                    UpdateMessage(_app.GetResourceString(key));
                    return true;
                }
            }
            UpdateMessage(strNewText);

            return true;
        }

        public void UpdateMessage(string message)
        {
            _progressUi?.UpdateMessage(message);
            _lastMessage = message;
        }

        public void UpdateSubMessage(string submessage)
        {
            _progressUi?.UpdateSubMessage(submessage);
            _lastSubMessage = submessage;
        }

        public bool ContinueWork()
        {
            return true;
        }

        public void UpdateMessage(UiStringKey stringKey)
        {
            if (_app != null)
                UpdateMessage(_app.GetResourceString(stringKey));
        }
    }


    /// <summary>
        /// Class to run a task while a progress dialog is shown
        /// </summary>
        public class BlockingOperationRunner
	{
        //for handling Activity recreation situations, we need access to the currently active task. It must hold that there is no more than one active task.
	    private static BlockingOperationRunner _currentTask = null;

	    public static void SetNewActiveActivity(Activity activeActivity)
	    {
	        if (_currentTask != null)
	        {
	            _currentTask.ActiveActivity = activeActivity;
	        }
	    }
	    public static void RemoveActiveActivity(Activity activity)
	    {
	        if ((_currentTask != null) && (_currentTask._activeActivity == activity))
	            _currentTask.ActiveActivity = null;

	    }

        public Activity ActiveActivity
	    {
	        get { return _activeActivity; }
	        private set
	        {
                if (_activeActivity != null && _activeActivity != _previouslyActiveActivity)
                {
                    _previouslyActiveActivity = _activeActivity;

                }
	            _activeActivity = value;
	            if (_task != null)
	                _task.ActiveContext = _activeActivity;
	            if (_activeActivity != null)
	            {
	                SetupProgressDialog(_app);
	                _progressDialog.Show();
                }
	        }
	    }

        public Activity PreviouslyActiveActivity
        {
            get { return _previouslyActiveActivity; }
           
        }

		private readonly Handler _handler;
		private readonly OperationWithFinishHandler _task;
		private IProgressDialog _progressDialog;
        private readonly IKp2aApp _app;
        private Java.Lang.Thread _thread;
	    private Activity _activeActivity, _previouslyActiveActivity;
	    private ProgressDialogStatusLogger _progressDialogStatusLogger;

	    public BlockingOperationRunner(IKp2aApp app, Activity activity, OperationWithFinishHandler task)
		{
		    _activeActivity = activity;
			_task = task;
			_handler = app.UiThreadHandler;
            _app = app;
			
			SetupProgressDialog(app);

		    // Set code to run when this is finished
            _task.operationFinishedHandler = new AfterTask(activity, task.operationFinishedHandler, _handler, this);
		    
		    _task.SetStatusLogger(_progressDialogStatusLogger);
			
			
		}

	    private void SetupProgressDialog(IKp2aApp app)
	    {
	        string currentMessage = "Initializing...";
	        string currentSubmessage = "";

	        if (_progressDialogStatusLogger != null)
	        {
	            currentMessage = _progressDialogStatusLogger.Message;
	            currentSubmessage = _progressDialogStatusLogger.SubMessage;
	        }

	        if (_progressDialog != null)
	        {
	            var pd = _progressDialog;
                app.UiThreadHandler.Post(() =>
                {
                    pd.Dismiss();
                });
	        }

            // Show process dialog
            _progressDialog = app.CreateProgressDialog(_activeActivity);
	        _progressDialog.SetTitle(_app.GetResourceString(UiStringKey.progress_title));
            _progressDialogStatusLogger = new ProgressDialogStatusLogger(_app, _handler, _progressDialog);
	        _progressDialogStatusLogger.UpdateMessage(currentMessage);
	        _progressDialogStatusLogger.UpdateSubMessage(currentSubmessage);
	    }

	    public void Run(bool allowOverwriteCurrentTask = false)
		{
		    if ((!allowOverwriteCurrentTask) && (_currentTask != null))
		        throw new System.Exception("Cannot start another BlockingOperationRunner while BlockingOperationRunner is already running! " + _task.GetType().Name + "/" + _currentTask._task.GetType().Name);
		    _currentTask = this;

            // Show process dialog
            _progressDialog.Show();
			
			
			// Start Thread to Run task
			_thread = new Java.Lang.Thread(_task.Run);
			_thread.Start();
		}

		public void JoinWorkerThread()
		{
			_thread.Join();
		}
		
		private class AfterTask : OnOperationFinishedHandler {
			readonly BlockingOperationRunner _blockingOperationRunner;

			public AfterTask (Activity activity, OnOperationFinishedHandler operationFinishedHandler, Handler handler, BlockingOperationRunner pt): base(activity, operationFinishedHandler, handler)
			{
				_blockingOperationRunner = pt;
			}

			public override void Run() {
				base.Run();

				if (Handler != null) //can be null in tests
				{
					// Remove the progress dialog
					Handler.Post(delegate
					{
					    _blockingOperationRunner._progressDialog.Dismiss();
					});
				}
				else
				{
				    _blockingOperationRunner._progressDialog.Dismiss();
				}
			    _currentTask = null;

			}
			
		}

	    
	}
}

