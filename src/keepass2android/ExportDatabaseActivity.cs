using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Util;
using Android.Widget;
using KeePass.DataExchange;
using KeePass.DataExchange.Formats;
using KeePassLib.Interfaces;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace keepass2android
{
    public class ExportDbProcessManager: FileSaveProcessManager
    {
        private readonly FileFormatProvider _ffp;

        public ExportDbProcessManager(int requestCode, Activity activity, FileFormatProvider ffp) : base(requestCode, activity)
        {
            _ffp = ffp;
        }

        protected override void SaveFile(IOConnectionInfo ioc)
        {
            var exportDb = new ExportDatabaseActivity.ExportDb(_activity, App.Kp2a, new ActionOnFinish(_activity, (success, message, activity) =>
                {
                    if (!success)
                        Toast.MakeText(activity, message, ToastLength.Long).Show();
                    else
                        Toast.MakeText(activity, _activity.GetString(Resource.String.export_database_successful), ToastLength.Long).Show();
                    activity.Finish();
                }
            ), _ffp, ioc);
            ProgressTask pt = new ProgressTask(App.Kp2a, _activity, exportDb);
            pt.Run();

        }
    }

	[Activity(Label = "@string/app_name",
	    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden,
        Theme = "@style/MyTheme_ActionBar")]
	[IntentFilter(new[] {"kp2a.action.ExportDatabaseActivity"}, Categories = new[] {Intent.CategoryDefault})]
	public class ExportDatabaseActivity : LockCloseActivity
	{
		FileFormatProvider[] _ffp = new FileFormatProvider[]
			{
				new KeePassKdb2x(),
				new KeePassXml2x(), 
				new KeePassCsv1x()
			};

		private int _fileFormatIndex;

	    private ExportDbProcessManager _exportDbProcessManager;

        protected override void OnCreate(Android.OS.Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			AlertDialog.Builder builder = new AlertDialog.Builder(this);
			builder.SetTitle(Resource.String.export_fileformats_title);
			builder.SetSingleChoiceItems(Resource.Array.export_fileformat_options, _fileFormatIndex,
				delegate(object sender, DialogClickEventArgs args) { _fileFormatIndex = args.Which; });
			builder.SetPositiveButton(Android.Resource.String.Ok, delegate
			{
			    _exportDbProcessManager = new ExportDbProcessManager(0, this, _ffp[_fileFormatIndex]);
			    _exportDbProcessManager.StartProcess();
			});
			builder.SetNegativeButton(Resource.String.cancel, delegate {
					Finish();
				});
			builder.Show();
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

		    if (_exportDbProcessManager?.OnActivityResult(requestCode, resultCode, data) == true)
		        return;

			Finish();

		}
        

		protected int RequestCodeDbFilename
		{
			get { return 0; }
		}

		public class ExportDb : RunnableOnFinish
		{
			private readonly IKp2aApp _app;
			private readonly FileFormatProvider _fileFormat;
			private IOConnectionInfo _targetIoc;

			public ExportDb(Activity activity, IKp2aApp app, OnFinish onFinish, FileFormatProvider fileFormat, IOConnectionInfo targetIoc) : base(activity, onFinish)
			{
				_app = app;
				this._fileFormat = fileFormat;
				_targetIoc = targetIoc;
			}

			public override void Run()
			{
				StatusLogger.UpdateMessage(UiStringKey.exporting_database);
				var pd = _app.CurrentDb.KpDatabase;
				PwExportInfo pwInfo = new PwExportInfo(pd.RootGroup, pd, true);
				
				try
				{
					var fileStorage = _app.GetFileStorage(_targetIoc);
					if (fileStorage is IOfflineSwitchable)
					{
						((IOfflineSwitchable) fileStorage).IsOffline = false;
					}
					using (var writeTransaction = fileStorage.OpenWriteTransaction(_targetIoc, _app.GetBooleanPreference(PreferenceKey.UseFileTransactions)))
					{
						Stream sOut = writeTransaction.OpenFile();
						_fileFormat.Export(pwInfo, sOut, new NullStatusLogger());
						
						if (sOut != null) sOut.Close();

						writeTransaction.CommitWrite();
						
					}
					if (fileStorage is IOfflineSwitchable)
					{
						((IOfflineSwitchable)fileStorage).IsOffline = App.Kp2a.OfflineMode;
					}
					
					Finish(true);

					
				}
				catch (Exception ex)
				{
					Finish(false, ex.Message);
				}


			}
		}

	}
}