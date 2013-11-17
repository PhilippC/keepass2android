using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Java.Lang;
using Exception = System.Exception;
using Object = Java.Lang.Object;

namespace keepass2android.Utils
{
	public class LoadingDialog<TParams, TProgress, TResult> : AsyncTask<TParams, TProgress, TResult> 
	{
		private readonly Context _context;
		private readonly string _message;
		private readonly bool _cancelable;
		readonly Func<Object[], Object> _doInBackground;
		readonly Action<Object> _onPostExecute;

		private ProgressDialog mDialog;
		/**
		 * Default is {@code 500}ms
		 */
		private int mDelayTime = 500;
		/**
		 * Flag to use along with {@link #mDelayTime}
		 */
		private bool mFinished = false;

		private Exception mLastException;

		
		public LoadingDialog(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
		}

		public LoadingDialog(Context context, string message, bool cancelable, Func<Object[], Object> doInBackground, 
			Action<Object> onPostExecute)
		{
			_context = context;
			_message = message;
			_cancelable = cancelable;
			_doInBackground = doInBackground;
			_onPostExecute = onPostExecute;
			Initialize();
		}

		private void Initialize()
		{
			mDialog = new ProgressDialog(_context);
			mDialog.SetMessage(_message);
			mDialog.Indeterminate = true;
			mDialog.SetCancelable(_cancelable);
			if (_cancelable)
			{
				mDialog.SetCanceledOnTouchOutside(true);
				mDialog.CancelEvent += (sender, args) => mDialog.Cancel();
			}
		}

		public LoadingDialog(Context context, bool cancelable, Func<Object[], Object> doInBackground, Action<Object> onPostExecute)
		{
			_message = context.GetString(Resource.String.loading);
			_context = context;
			_cancelable = cancelable;
			_doInBackground = doInBackground;
			_onPostExecute = onPostExecute;
			Initialize();
		}

		protected override void OnPreExecute()
		{
			new Handler().PostDelayed(() =>
				{
					if (!mFinished)
					{
						try
						{
							/*
							 * sometime the activity has been finished before we
							 * show this dialog, it will raise error
							 */
							mDialog.Show();
						}
						catch (Exception t)
						{
							Kp2aLog.Log(t.ToString());
						}
					}

				}
				, mDelayTime);
		}
		
  
		/**
		 * If you override this method, you must call {@code super.onCancelled()} at
		 * beginning of the method.
		 */
		protected override void OnCancelled() {
			DoFinish();
			base.OnCancelled();
		}// onCancelled()

		private void DoFinish() {
			mFinished = true;
			try {
				/*
				 * Sometime the activity has been finished before we dismiss this
				 * dialog, it will raise error.
				 */
				mDialog.Dismiss();
			} catch (Exception e)
			{
				Kp2aLog.Log(e.ToString());
			}
		}// doFinish()


		/**
		 * Sets last exception. This method is useful in case an exception raises
		 * inside {@link #doInBackground(Void...)}
		 * 
		 * @param t
		 *            {@link Throwable}
		 */
		protected void SetLastException(Exception e) {
			mLastException = e;
		}// setLastException()

		/**
		 * Gets last exception.
		 * 
		 * @return {@link Throwable}
		 */
		protected Exception GetLastException() {
			return mLastException;
		}// getLastException()


		protected override Object DoInBackground(params Object[] @params)
		{
			return _doInBackground(@params);
		}

		protected override TResult RunInBackground(params TParams[] @params)
		{
			throw new NotImplementedException();
		}

		protected override void OnPostExecute(Object result)
		{
			DoFinish();
			
			if (_onPostExecute != null)
				_onPostExecute(result);
		}

		
		
		

	}
}