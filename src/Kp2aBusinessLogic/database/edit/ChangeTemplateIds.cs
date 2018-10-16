using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Security;

namespace keepass2android.database.edit
{
    public class ChangeTemplateIds: RunnableOnFinish
    {
        private readonly IKp2aApp _app;
        private readonly Database _db;
        private string _etmTemplateUuid { get { return "_etm_template_uuid"; } }
        public static string TemplateIdStringKey
        {
            get { return "KP2A_TemplateId"; }
        }

        public ChangeTemplateIds(Activity activeActivity, IKp2aApp app, Database db, OnFinish finish) : base(activeActivity, finish)
        {
            _app = app;
            _db = db;
        }

        public override void Run()
        {
            StatusLogger.UpdateMessage(UiStringKey.UpdatingTemplateIds);
            Dictionary<string, string> uuidMap = new Dictionary<string, string>();
            foreach (var templateEntry in AddTemplateEntries.TemplateEntries)
            {
                PwEntry entry;
                if (_db.Entries.TryGetValue(templateEntry.Uuid, out entry))
                {
                    PwUuid oldUuid = entry.Uuid;
                    entry.Uuid = new PwUuid(true);
                    uuidMap[oldUuid.ToHexString()] = entry.Uuid.ToHexString();
                    entry.Strings.Set(TemplateIdStringKey,new ProtectedString(false, oldUuid.ToHexString()));
                    _db.Entries.Remove(oldUuid);
                    _db.Entries[entry.Uuid] = entry;
                }
            }
            foreach (var entry in _db.Entries.Values)
            {
                string templateUuid = entry.Strings.ReadSafe(_etmTemplateUuid);
                if (templateUuid != null)
                {
                    string newTemplateUuid;
                    if (uuidMap.TryGetValue(templateUuid, out newTemplateUuid))
                    {
                        entry.Strings.Set(_etmTemplateUuid, new ProtectedString(false, newTemplateUuid));
                    }
                }
            }

            if (uuidMap.Any())
            {
                SaveDb save = new SaveDb( ActiveActivity, _app, _db, OnFinishToRun);
                save.SetStatusLogger(StatusLogger);
                save.Run();
            }
            else
            {
                OnFinishToRun?.Run();
            }
            
        }
    }
}