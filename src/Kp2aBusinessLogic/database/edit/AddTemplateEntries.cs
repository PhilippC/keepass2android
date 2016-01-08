/*
This file is part of Keepass2Android, Copyright 2016 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

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

using System;
using System.Collections.Generic;
using System.Linq;
using Android.Content;
using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace keepass2android
{
	public class AddTemplateEntries : RunnableOnFinish {

		class TemplateEntry
		{
			public UiStringKey Title { get; set; }
			public PwIcon Icon { get; set; }

			public PwUuid Uuid { get; set; }

			public List<ITemplateField> Fields
			{
				get;
				set;
			}


			public interface ITemplateField
			{
				void AddToEntry(IKp2aApp app, PwEntry entry, int position);
			}

			internal enum FieldType
			{
				Inline, ProtectedInline
			}
			internal enum SpecialFieldKey
			{
				ExpDate, OverrideUrl, Tags
			}

			public class CustomField : ITemplateField
			{
				public UiStringKey FieldName { get; set; }

				public FieldType Type { get; set; }


				public void AddToEntry(IKp2aApp app, PwEntry entry, int position)
				{
					Dictionary<FieldType, string> fn = new Dictionary<FieldType, string>()
					{
						{ FieldType.ProtectedInline, "Protected Inline"},
						{ FieldType.Inline, "Inline"}
					};

					string fieldKey = app.GetResourceString(FieldName);
					entry.Strings.Set("_etm_position_"+fieldKey, new ProtectedString(false, position.ToString()));	
					entry.Strings.Set("_etm_title_"+fieldKey, new ProtectedString(false, fieldKey));	
					entry.Strings.Set("_etm_type_"+fieldKey, new ProtectedString(false, fn[Type]));	
				}
			}

			public class StandardField : ITemplateField
			{
				public string FieldName { get; set; }
				public void AddToEntry(IKp2aApp app, PwEntry entry, int position)
				{
					string fieldKey = FieldName;
					entry.Strings.Set("_etm_position_"+fieldKey, new ProtectedString(false, position.ToString()));	
					entry.Strings.Set("_etm_title_"+fieldKey, new ProtectedString(false, fieldKey));	
					entry.Strings.Set("_etm_type_"+fieldKey, new ProtectedString(false, FieldName == PwDefs.PasswordField ? "Protected Inline" : "Inline"));	
				}
			}

			public class SpecialField : ITemplateField
			{
				public SpecialFieldKey FieldName { get; set; }


				public const string TagsKey = "@tags";
				public const string OverrideUrlKey = "@override";
				public const string ExpDateKey = "@exp_date";

				public void AddToEntry(IKp2aApp app, PwEntry entry, int position)
				{
					string fieldKey = "";
					string type = "Inline";
					switch (FieldName)
					{
						case SpecialFieldKey.ExpDate:
							fieldKey = ExpDateKey;
							type = "Date Time";
							break;
						case SpecialFieldKey.OverrideUrl:
							fieldKey = OverrideUrlKey;
							break;
						case SpecialFieldKey.Tags:
							fieldKey = TagsKey;
							break;
					}
					entry.Strings.Set("_etm_position_" + fieldKey, new ProtectedString(false, position.ToString()));
					entry.Strings.Set("_etm_title_" + fieldKey, new ProtectedString(false, fieldKey));
					entry.Strings.Set("_etm_type_" + fieldKey, new ProtectedString(false, type));
				}
			}
		}

		protected Database Db
		{
			get { return _app.GetDb(); }
		}

		private readonly IKp2aApp _app;
		private readonly Context _ctx;
		
		public AddTemplateEntries(Context ctx, IKp2aApp app, OnFinish finish)
			: base(finish)
		{
			_ctx = ctx;
			_app = app;
			
			//_onFinishToRun = new AfterAdd(this, OnFinishToRun);
		}

		static readonly List<TemplateEntry> TemplateEntries = new List<TemplateEntry>()
			{
				new TemplateEntry()
				{
					Title = UiStringKey.TemplateTitle_IdCard,
					Icon = PwIcon.Identity,
					Uuid = new PwUuid(MemUtil.HexStringToByteArray("A7B525BD0CECC84EB9F0CEDC0B49B5B8")),
					Fields = new List<TemplateEntry.ITemplateField>()
					{
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_Number,
							Type = TemplateEntry.FieldType.Inline
						},
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_IdCard_Name,
							Type = TemplateEntry.FieldType.Inline
						},
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_IdCard_PlaceOfIssue,
							Type = TemplateEntry.FieldType.Inline
						},
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_IdCard_IssueDate,
							Type = TemplateEntry.FieldType.Inline
						},
						new TemplateEntry.SpecialField()
						{
							FieldName = TemplateEntry.SpecialFieldKey.ExpDate
						}
					}
				},
				new TemplateEntry()
				{
					Title = UiStringKey.TemplateTitle_EMail,
					Icon = PwIcon.EMail,
					Uuid = new PwUuid(MemUtil.HexStringToByteArray("0B84EC3029E330478CD99B670942295B")),
					Fields = new List<TemplateEntry.ITemplateField>()
					{
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_EMail_EMail,
							Type = TemplateEntry.FieldType.Inline
						},
						new TemplateEntry.StandardField()
						{
							FieldName = PwDefs.UrlField
						},
						new TemplateEntry.StandardField()
						{
							FieldName = PwDefs.PasswordField
						}

					}
				},
				new TemplateEntry()
				{
					Title = UiStringKey.TemplateTitle_WLan,
					Icon = PwIcon.IRCommunication,
					Uuid = new PwUuid(MemUtil.HexStringToByteArray("46B56A7E90407545B646E8DC488A5FA2")),
					Fields = new List<TemplateEntry.ITemplateField>()
					{
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_WLan_SSID,
							Type = TemplateEntry.FieldType.Inline
						},
						new TemplateEntry.StandardField()
						{
							FieldName = PwDefs.PasswordField
						}
					}
				},
				
				new TemplateEntry()
				{
					Title = UiStringKey.TemplateTitle_Notes,
					Icon = PwIcon.Notepad,
					Uuid = new PwUuid(MemUtil.HexStringToByteArray("10F8C25C26AE9B49A47FDA7CDACACEE2")),
					Fields = new List<TemplateEntry.ITemplateField>()
					{
						new TemplateEntry.StandardField()
						{
							FieldName = PwDefs.NotesField
						}
					}
				},
				
				new TemplateEntry()
				{
					Title = UiStringKey.TemplateTitle_CreditCard,
					Icon = PwIcon.Homebanking,
					Uuid = new PwUuid(MemUtil.HexStringToByteArray("49DD48DBFF149445B3392CE90EA75309")),
					Fields = new List<TemplateEntry.ITemplateField>()
					{
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_Number,
							Type = TemplateEntry.FieldType.Inline
						},
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_CreditCard_CVV,
							Type = TemplateEntry.FieldType.ProtectedInline
						},
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_CreditCard_PIN,
							Type = TemplateEntry.FieldType.ProtectedInline
						},
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_CreditCard_Owner,
							Type = TemplateEntry.FieldType.Inline
						},
						new TemplateEntry.SpecialField() { FieldName = TemplateEntry.SpecialFieldKey.ExpDate}
					}
				},
				new TemplateEntry()
				{
					Title = UiStringKey.TemplateTitle_Membership,
					Icon = PwIcon.UserKey,
					Uuid = new PwUuid(MemUtil.HexStringToByteArray("DD5C627BC66C28498FDEC70740D29168")),
					Fields = new List<TemplateEntry.ITemplateField>()
					{
						new TemplateEntry.CustomField()
						{
							FieldName = UiStringKey.TemplateField_Number,
							Type = TemplateEntry.FieldType.Inline
						},
						new TemplateEntry.StandardField()
						{
							FieldName = PwDefs.UrlField
						},
						new TemplateEntry.SpecialField()
						{
							FieldName = TemplateEntry.SpecialFieldKey.ExpDate
						}
}}
				
			};

		public static bool ContainsAllTemplates(IKp2aApp app)
		{
			return TemplateEntries.All(t => app.GetDb().Entries.ContainsKey(t.Uuid));
		}

		public override void Run() {	
			StatusLogger.UpdateMessage(UiStringKey.AddingEntry);

			if (TemplateEntries.GroupBy(e => e.Uuid).Any(g => g.Count() > 1))
			{
				throw new Exception("invalid UUIDs in template list!");
			}

			PwGroup templateGroup;
			if (!_app.GetDb().Groups.TryGetValue(_app.GetDb().KpDatabase.EntryTemplatesGroup, out templateGroup))
			{
				//create template group
				templateGroup = new PwGroup(true, true, _app.GetResourceString(UiStringKey.TemplateGroupName), PwIcon.Folder);
				_app.GetDb().KpDatabase.RootGroup.AddGroup(templateGroup, true);
				_app.GetDb().KpDatabase.EntryTemplatesGroup = templateGroup.Uuid;
				_app.GetDb().KpDatabase.EntryTemplatesGroupChanged = DateTime.Now;
				_app.GetDb().Dirty.Add(_app.GetDb().KpDatabase.RootGroup);
				_app.GetDb().Groups[templateGroup.Uuid] = templateGroup;
			}
			List<PwEntry> addedEntries = new List<PwEntry>();

			foreach (var template in TemplateEntries)
			{
				if (_app.GetDb().Entries.ContainsKey(template.Uuid))
					continue;
				PwEntry entry = CreateEntry(template);
				templateGroup.AddEntry(entry, true);
				addedEntries.Add(entry);
				_app.GetDb().Entries[entry.Uuid] = entry;
			}

			if (addedEntries.Any())
			{
				_app.GetDb().Dirty.Add(templateGroup);

				// Commit to disk
				SaveDb save = new SaveDb(_ctx, _app, OnFinishToRun);
				save.SetStatusLogger(StatusLogger);
				save.Run();
			}
		}

		private PwEntry CreateEntry(TemplateEntry template)
		{
			PwEntry entry = new PwEntry(false, true);
			entry.Uuid = template.Uuid;
			entry.IconId = template.Icon;
			entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, _app.GetResourceString(template.Title)));
			entry.Strings.Set("_etm_template", new ProtectedString(false, "1"));
			int position = 0;
			foreach (var field in template.Fields)
			{
				field.AddToEntry(_app, entry, position);
				position++;
			}
			return entry;
		}

		private class AfterAdd : OnFinish {
			private readonly Database _db;
			private readonly List<PwEntry> _entries;

			public AfterAdd(Database db, List<PwEntry> entries, OnFinish finish):base(finish) {
				_db = db;
				_entries = entries;

			}
			


			public override void Run() {
				
				
				base.Run();
			}
		}
		
		
	}

}

