using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using keepass2android.Io;
using KeePassLib.Serialization;

namespace keepass2android
{
    public abstract class FileSaveProcessManager
    {

        protected readonly int _requestCode;
        protected readonly Activity _activity;

        public FileSaveProcessManager(int requestCode, Activity activity)
        {
            _requestCode = requestCode;
            _activity = activity;
        }

        public bool OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            

            if (requestCode == _requestCode)
            {
                if (resultCode == KeePass.ExitFileStorageSelectionOk)
                {
                    string protocolId = data.GetStringExtra("protocolId");
                    if (protocolId == "content")
                    {
                        Util.ShowBrowseDialog(_activity, _requestCode, true, true);
                    }
                    else
                    {
                        FileSelectHelper fileSelectHelper = new FileSelectHelper(_activity, true, true, _requestCode);
                        fileSelectHelper.OnOpen += (sender, ioc) =>
                        {
                            SaveFile(ioc);
                        };
                        App.Kp2a.GetFileStorage(protocolId).StartSelectFile(
                            new FileStorageSetupInitiatorActivity(_activity, (i, result, arg3) => OnActivityResult(i, result, arg3), s => fileSelectHelper.PerformManualFileSelect(s)),
                            true,
                            _requestCode,
                            protocolId);
                    }
                    return true;
                }

                if (resultCode == (Result)FileStorageResults.FileUsagePrepared)
                {
                    var ioc = new IOConnectionInfo();
                    Util.SetIoConnectionFromIntent(ioc, data);
                    SaveFile(ioc);
                    return true;
                }
                if (resultCode == (Result)FileStorageResults.FileChooserPrepared)
                {
                    IOConnectionInfo ioc = new IOConnectionInfo();
                    Util.SetIoConnectionFromIntent(ioc, data);
                    new FileSelectHelper(_activity, true, true, _requestCode).StartFileChooser(ioc.Path);
                    return true;
                }
                if (resultCode == Result.Ok)
                {
                    if (requestCode == _requestCode)
                    {

                        if (data.Data.Scheme == "content")
                        {
                            if ((int)Android.OS.Build.VERSION.SdkInt >= 19)
                            {
                                //try to take persistable permissions
                                try
                                {
                                    Kp2aLog.Log("TakePersistableUriPermission");
                                    var takeFlags = data.Flags
                                                    & (ActivityFlags.GrantReadUriPermission
                                                       | ActivityFlags.GrantWriteUriPermission);
                                    _activity.ContentResolver.TakePersistableUriPermission(data.Data, takeFlags);
                                }
                                catch (Exception e)
                                {
                                    Kp2aLog.Log(e.ToString());
                                }

                            }
                        }


                        string filename = Util.IntentToFilename(data, _activity);
                        if (filename == null)
                            filename = data.DataString;

                        bool fileExists = data.GetBooleanExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.result_file_exists", true);

                        if (fileExists)
                        {
                            SaveFile(new IOConnectionInfo { Path = FileSelectHelper.ConvertFilenameToIocPath(filename) });

                        }
                        else
                        {
                            var task = new CreateNewFilename(_activity, new ActionOnFinish(_activity, (success, messageOrFilename, activity) =>
                            {
                                if (!success)
                                {
                                    Toast.MakeText(activity, messageOrFilename, ToastLength.Long).Show();
                                    return;
                                }
                                SaveFile(new IOConnectionInfo { Path = FileSelectHelper.ConvertFilenameToIocPath(messageOrFilename) });


                            }), filename);

                            new ProgressTask(App.Kp2a, _activity, task).Run();
                        }

                        return true;


                    }

                }
                Clear();
                return true;
            }


            
            return false;
        }

        protected virtual void Clear()
        {
            
        }

        protected abstract void SaveFile(IOConnectionInfo ioc);

        public void StartProcess()
        {
            Intent intent = new Intent(_activity, typeof(FileStorageSelectionActivity));
            //intent.PutExtra(FileStorageSelectionActivity.AllowThirdPartyAppSend, true);
            _activity.StartActivityForResult(intent, _requestCode);
        }

        public virtual void OnSaveInstanceState(Bundle outState)
        {
            
        }
    }
}