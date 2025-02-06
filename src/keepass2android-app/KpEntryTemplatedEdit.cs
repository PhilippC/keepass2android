using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace keepass2android
{
	class KpEntryTemplatedEdit : EditModeBase
	{
		internal class KeyOrderComparer : IComparer<string>
		{
			private readonly KpEntryTemplatedEdit _kpEntryTemplatedEdit;

			public KeyOrderComparer(KpEntryTemplatedEdit kpEntryTemplatedEdit)
			{
				_kpEntryTemplatedEdit = kpEntryTemplatedEdit;
			}

			public int Compare(string x, string y)
			{
				int orderX = _kpEntryTemplatedEdit.GetPosition(x);
				int orderY = _kpEntryTemplatedEdit.GetPosition(y);
				if (orderX == orderY)
					return String.Compare(x, y, StringComparison.CurrentCulture);
				else
					return orderX.CompareTo(orderY);
			}
		}

		private int GetPosition(string key)
		{
			int res;
			if (!Int32.TryParse(_templateEntry.Strings.ReadSafe("_etm_position_" + key), out res))
				return Int32.MaxValue;
			return res;
		}

		public const string EtmTemplateUuid = "_etm_template_uuid";
		private const string EtmTitle = "_etm_title_";
	    private readonly PwEntry _entry;
		private readonly PwEntry _templateEntry;

		public static bool IsTemplated(Database db, PwEntry entry)
		{
			if (entry.Strings.Exists(EtmTemplateUuid))
			{
				byte[] uuidBytes = MemUtil.HexStringToByteArray(entry.Strings.ReadSafe(EtmTemplateUuid));
				if (uuidBytes != null)
				{
						PwUuid templateUuid = new PwUuid(uuidBytes);
						return db.EntriesById.ContainsKey(templateUuid);
				}
			}
			return false;
		}

		public KpEntryTemplatedEdit(Database db, PwEntry entry)
		{
		    _entry = entry;
			PwUuid templateUuid = new PwUuid(MemUtil.HexStringToByteArray(entry.Strings.ReadSafe(EtmTemplateUuid)));
			_templateEntry = db.EntriesById[templateUuid];
		}

		public static void InitializeEntry(PwEntry entry, PwEntry templateEntry)
		{
			entry.Strings.Set("_etm_template_uuid", new ProtectedString(false, templateEntry.Uuid.ToHexString()));
			entry.IconId = templateEntry.IconId;
			entry.CustomIconUuid = templateEntry.CustomIconUuid;
			entry.AutoType = templateEntry.AutoType.CloneDeep();
			entry.Binaries = templateEntry.Binaries.CloneDeep();
			entry.BackgroundColor = templateEntry.BackgroundColor;
			entry.ForegroundColor = templateEntry.ForegroundColor;

			foreach (string name in templateEntry.Strings.GetKeys())
			{
				if (name.StartsWith(EtmTitle))
				{
					String fieldName = name.Substring(EtmTitle.Length);

					if (fieldName.StartsWith("@"))
					{
						if (fieldName == KeePass.TagsKey) entry.Tags = templateEntry.Tags;
						if (fieldName == KeePass.OverrideUrlKey) entry.OverrideUrl = templateEntry.OverrideUrl;
						if (fieldName == KeePass.ExpDateKey)
						{
							entry.Expires = templateEntry.Expires;
							if (entry.Expires)
								entry.ExpiryTime = templateEntry.ExpiryTime;
						}
						continue;
					}

					String type = templateEntry.Strings.ReadSafe("_etm_type_" + fieldName);

					if ((type == "Divider") || (type == "@confirm"))
						continue;

					bool protectedField = type.StartsWith("Protected");
					entry.Strings.Set(fieldName, new ProtectedString(protectedField, templateEntry.Strings.ReadSafe(fieldName)));

				}
			}
		}

		public override bool IsVisible(string fieldKey)
		{
			if (fieldKey == EtmTemplateUuid)
				return false;
			if (fieldKey == PwDefs.TitleField)
				return true;

			if ((fieldKey.StartsWith("@") || (PwDefs.IsStandardField(fieldKey))))
			{
				return !String.IsNullOrEmpty(GetFieldValue(fieldKey))
					|| _templateEntry.Strings.Exists(EtmTitle+fieldKey);
			}

			return true;
		}

		private string GetFieldValue(string fieldKey)
		{
			if (fieldKey == KeePass.ExpDateKey)
				return _entry.Expires ? _entry.ExpiryTime.ToString(CultureInfo.CurrentUICulture) : "";
			if (fieldKey == KeePass.OverrideUrlKey)
				return _entry.OverrideUrl;
			if (fieldKey == KeePass.TagsKey)
				return StrUtil.TagsToString(_entry.Tags, true);
			return _entry.Strings.ReadSafe(fieldKey);
		}

		public override IEnumerable<string> SortExtraFieldKeys(IEnumerable<string> keys)
		{
			var c = new KeyOrderComparer(this);
			return keys.OrderBy(s => s, c);
		}

        public override bool ShowAddAttachments
        {
            get
            {
                if (manualShowAddAttachments != null) return (bool)manualShowAddAttachments;
                return false;
            }
        }

        public override bool ShowAddExtras
		{
			get {
                if (manualShowAddExtras != null) return (bool)manualShowAddExtras;
                return false;
            }
		}

	    public override string GetTitle(string key)
	    {
	        return key;
	    }

	    public override string GetFieldType(string key)
	    {
            //TODO return "bool" for boolean fields
	        return "";
	    }

	    

	    public static bool IsTemplate(PwEntry entry)
		{
			if (entry == null) return false;
			return entry.Strings.Exists("_etm_template");
		}
	}
}