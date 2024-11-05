using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Serialization;
using Object = Java.Lang.Object;

namespace keepass2android
{
    /// <summary>
    /// Edit mode implementation for AutoOpen entries
    /// </summary>
    public class AutoOpenEdit : EditModeBase
    {
        private const string strVisible = "Visible";
        private const string strEnabled = "Enabled";
        private const string strUiKeyFile = "_ui_KeyFile";
        private const string strUiDatabaseFile = "_ui_DatabaseFile";
        private const string strUiIfDevice = "_ui_IfDevice_";

        public AutoOpenEdit(PwEntry entry)
        {
            
        }

        public override bool IsVisible(string fieldKey)
        {
            if (fieldKey == PwDefs.TitleField
                || fieldKey == PwDefs.PasswordField
                || fieldKey == strVisible
                || fieldKey == strEnabled
                || fieldKey.StartsWith("_ui_"))
            {
                return true;
            }
            return false;
        }

        public override IEnumerable<string> SortExtraFieldKeys(IEnumerable<string> keys)
        {
            return keys.OrderBy(s =>
            {
                if (s == strUiDatabaseFile) return 1;
                if (s == strEnabled) return 2;
                
                if (s == strUiKeyFile) return 10000;
                if (s == strVisible) return   10001;
                return 10;

            }).ThenBy(s => s);
            
        }

        public override bool ShowAddAttachments
        {
            get { return false; }
        }

        public override bool ShowAddExtras
        {
            get { return false; }
        }

        public override string GetTitle(string key)
        {
            if (key == strVisible)
                return LocaleManager.LocalizedAppContext.GetString(Resource.String.Visible_title);
            if (key == strEnabled)
                return LocaleManager.LocalizedAppContext.GetString(Resource.String.child_db_Enabled_title);
            if (key == strUiKeyFile)
                return LocaleManager.LocalizedAppContext.GetString(Resource.String.keyfile_heading);
            if (key == strUiDatabaseFile)
                return LocaleManager.LocalizedAppContext.GetString(Resource.String.database_file_heading);
            if (key.StartsWith(strUiIfDevice))
            {
                return LocaleManager.LocalizedAppContext.GetString(Resource.String.if_device_text,new Object[]{key.Substring(strUiIfDevice.Length)});
            }
            return key;
        }

        public override string GetFieldType(string key)
        {
            if ((key == strEnabled)
                || key == strVisible
                || key.StartsWith(strUiIfDevice))
                return "bool";

            if ((key == strUiDatabaseFile)
                || (key == strUiKeyFile))
                return "file";

            return "";
        }

        public override void InitializeEntry(PwEntry entry)
        {
            base.InitializeEntry(entry);
            if (!entry.Strings.Exists(strVisible))
            {
                entry.Strings.Set(strVisible, new ProtectedString(false, "true"));
            }
            if (!entry.Strings.Exists(strEnabled))
            {
                entry.Strings.Set(strEnabled, new ProtectedString(false, "true"));
            }
            var autoExecItem = KeeAutoExecExt.MakeAutoExecItem(App.Kp2a.CurrentDb.KpDatabase, entry, 0);
            IOConnectionInfo ioc;
            if (!KeeAutoExecExt.TryGetDatabaseIoc(autoExecItem, out ioc))
                ioc = IOConnectionInfo.FromPath(entry.Strings.ReadSafe(PwDefs.UrlField));
            string path = ioc.Path;
            try
            {
                var filestorage = App.Kp2a.GetFileStorage(ioc);
                if (filestorage != null)
                {
                    path = filestorage.IocToPath(ioc);
                }
            }
            catch (NoFileStorageFoundException)
            {
                
            }
            

            entry.Strings.Set(strUiDatabaseFile, new ProtectedString(false, path));
            entry.Strings.Set(strUiKeyFile,new ProtectedString(false,entry.Strings.ReadSafe(PwDefs.UserNameField)));

            var devices =
                KeeAutoExecExt.GetIfDevice(KeeAutoExecExt.MakeAutoExecItem(App.Kp2a.CurrentDb.KpDatabase, entry, 0));
            //make sure user can enable/disable on this device explicitly:
            if (!devices.ContainsKey(KeeAutoExecExt.ThisDeviceId))
                devices[KeeAutoExecExt.ThisDeviceId] = false;
            foreach (var ifDevice in devices)
            {
                entry.Strings.Set(strUiIfDevice + ifDevice.Key, new ProtectedString(false, ifDevice.Value.ToString()));
            }
        }

        public override void PrepareForSaving(PwEntry entry)
        {
            entry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, entry.Strings.ReadSafe(strUiDatabaseFile)));
            entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, entry.Strings.ReadSafe(strUiKeyFile)));
            entry.Strings.Remove(strUiDatabaseFile);
            entry.Strings.Remove(strUiKeyFile);

            Dictionary<string, bool> devices = new Dictionary<string, bool>();
            foreach (string key in entry.Strings.GetKeys())
            {
                if (key.StartsWith(strUiIfDevice))
                {
                    string device = key.Substring(strUiIfDevice.Length);
                    devices[device] = entry.Strings.ReadSafe(key).Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            entry.Strings.Set(KeeAutoExecExt._ifDevice,new ProtectedString(false,KeeAutoExecExt.BuildIfDevice(devices)));
            foreach (string device in devices.Keys)
            {
                entry.Strings.Remove(strUiIfDevice + device);
            }

            base.PrepareForSaving(entry);


        }
    }
}