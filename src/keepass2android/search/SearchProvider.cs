/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */
using System;
using System.Globalization;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Database;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;

using KeePassLib;
using KeePassLib.Utility;
using System.Threading;
using System.Collections.Generic;

namespace keepass2android.search
{
	[ContentProvider(new [] { SearchProvider.Authority}, Exported=false )]
	public class SearchProvider : ContentProvider
	{
		private enum UriMatches
		{
			NoMatch = UriMatcher.NoMatch,
			GetIcon,
			GetSuggestions
		}
		public const String Authority = "keepass2android." + AppNames.PackagePart + ".SearchProvider";
		
		private const string GetIconPathQuery = "get_icon";
		private const string IconIdParameter = "IconId";
		private const string CustomIconUuidParameter = "CustomIconUuid";

		private Database _db;

		private static UriMatcher UriMatcher = BuildUriMatcher();

		static UriMatcher BuildUriMatcher()
		{
			var matcher = new UriMatcher(UriMatcher.NoMatch);

			// to get definitions...
			matcher.AddURI(Authority, GetIconPathQuery, (int)UriMatches.GetIcon);
			matcher.AddURI(Authority, SearchManager.SuggestUriPathQuery, (int)UriMatches.GetSuggestions);

			return matcher;
		}

		public override bool OnCreate()
		{
			_db = App.Kp2a.GetDb();
			return true;
		}

		public override Android.Database.ICursor Query(Android.Net.Uri uri, string[] projection, string selection, string[] selectionArgs, string sortOrder)
		{
			if (App.Kp2a.DatabaseIsUnlocked) // Can't show suggestions if the database is locked!
			{
				switch ((UriMatches)UriMatcher.Match(uri))
				{
					case UriMatches.GetSuggestions:
						var searchString = selectionArgs[0];
						if (!String.IsNullOrEmpty(searchString))
						{
							try
							{
								var resultsContexts = new Dictionary<PwUuid, KeyValuePair<string, string>>();
								var result = _db.Search(new SearchParameters { SearchString = searchString }, resultsContexts );
								return new GroupCursor(result, resultsContexts);
							}
							catch (Exception e)
							{
								Kp2aLog.Log("Failed to search for suggestions: " + e.Message);
							}
						}
						break;
					case UriMatches.GetIcon:
						return null; // This will be handled by OpenAssetFile

					default:
						return null;
						//throw new ArgumentException("Unknown Uri: " + uri, "uri");
				}
			}

			return null;
		}

		public override ParcelFileDescriptor OpenFile(Android.Net.Uri uri, string mode)
		{
			switch ((UriMatches)UriMatcher.Match(uri))
			{
				case UriMatches.GetIcon:
					var iconId = (PwIcon)Enum.Parse(typeof(PwIcon), uri.GetQueryParameter(IconIdParameter));
					var customIconUuid = new PwUuid(MemUtil.HexStringToByteArray(uri.GetQueryParameter(CustomIconUuidParameter)));

					var iconDrawable = _db.DrawableFactory.GetIconDrawable(App.Context.Resources, _db.KpDatabase, iconId, customIconUuid) as BitmapDrawable;
					if (iconDrawable != null)
					{
						var pipe = ParcelFileDescriptor.CreatePipe();
						var outStream = new OutputStreamInvoker(new ParcelFileDescriptor.AutoCloseOutputStream(pipe[1]));

						ThreadPool.QueueUserWorkItem(state =>
							{
								iconDrawable.Bitmap.Compress(Bitmap.CompressFormat.Png, 100, outStream);
								outStream.Close();
							});
						
						return pipe[0];
					}

					// Couldn't get an icon for some reason.
					return null;
				default:
					throw new ArgumentException("Unknown Uri: " + uri, "uri");
			}
		}

		public override string GetType(Android.Net.Uri uri)
		{
			switch ((UriMatches)UriMatcher.Match(uri))
			{
				case UriMatches.GetSuggestions:
					return SearchManager.SuggestMimeType;
				case UriMatches.GetIcon:
					return "image/png";

				default:
					throw new ArgumentException("Unknown Uri: " + uri, "uri");
			}
		}

		#region Unimplemented
		public override int Delete(Android.Net.Uri uri, string selection, string[] selectionArgs)
		{
			throw new NotImplementedException();
		}
		public override Android.Net.Uri Insert(Android.Net.Uri uri, ContentValues values)
		{
			throw new NotImplementedException();
		}
		public override int Update(Android.Net.Uri uri, ContentValues values, string selection, string[] selectionArgs)
		{
			throw new NotImplementedException();
		}
		#endregion


		private class GroupCursor : AbstractCursor
		{
			private static readonly string[] ColumnNames = new[] { Android.Provider.BaseColumns.Id, 
																	SearchManager.SuggestColumnText1, 
																	SearchManager.SuggestColumnText2, 
																	SearchManager.SuggestColumnIcon1,
																	SearchManager.SuggestColumnIntentDataId,
			};

			private readonly PwGroup mGroup;
			private readonly IDictionary<PwUuid, KeyValuePair<string, string>> mResultContexts;

			public GroupCursor(PwGroup group, IDictionary<PwUuid, KeyValuePair<string, string>> resultContexts)
			{
				System.Diagnostics.Debug.Assert(!group.Groups.Any(), "Expecting a flat list of groups");

				mGroup = group;
				mResultContexts = resultContexts;
			}

			public override int Count
			{
				get { return (int)Math.Min(mGroup.GetEntriesCount(false), int.MaxValue); }
			}

			public override string[] GetColumnNames()
			{
				return ColumnNames;
			}

			public override FieldType GetType(int column)
			{
				switch (column)
				{
					case 0: // _ID
						return FieldType.Integer;
					default:
						return base.GetType(column); // Ends up as string
				}
			}

			private PwEntry CurrentEntry
			{
				get
				{
					return mGroup.Entries.GetAt((uint)MPos);
				}
			}

			public override long GetLong(int column)
			{
				switch (column)
				{
					case 0: // _ID
						return MPos;
					default:
						throw new FormatException();
				}
			}

			public override string GetString(int column)
			{
				switch (column)
				{
					case 0: // _ID
						return MPos.ToString(CultureInfo.InvariantCulture);
					case 1: // SuggestColumnText1
						return CurrentEntry.Strings.ReadSafe(PwDefs.TitleField);
					case 2: // SuggestColumnText2
						KeyValuePair<string, string> context;
						if (mResultContexts.TryGetValue(CurrentEntry.Uuid, out context))
						{
							return Internationalise(context);
						}
						return null;
					case 3: // SuggestColumnIcon1
						var builder = new Android.Net.Uri.Builder();
						builder.Scheme(ContentResolver.SchemeContent);
						builder.Authority(Authority);
						builder.Path(GetIconPathQuery);
						builder.AppendQueryParameter(IconIdParameter, CurrentEntry.IconId.ToString());
						builder.AppendQueryParameter(CustomIconUuidParameter, CurrentEntry.CustomIconUuid.ToHexString());
						return builder.Build().ToString();
					case 4: // SuggestColumnIntentDataId
						return CurrentEntry.Uuid.ToHexString();
					default:
						return null;
				}
			}

			private string Internationalise(KeyValuePair<string, string> context)
			{
				try
				{

					// Some context names can be internationalised.
					int intlResourceId = 0;
					switch (context.Key)
					{
						case PwDefs.TitleField:
							// We will already be showing Title, so ignore it entirely so it doesn't double-appear
							return null;
						case PwDefs.UserNameField:
							intlResourceId = Resource.String.entry_user_name;
							break;
						case PwDefs.UrlField:
							intlResourceId = Resource.String.entry_url;
							break;
						case PwDefs.NotesField:
							intlResourceId = Resource.String.entry_comment;
							break;
						case PwGroup.SearchContextTags:
							intlResourceId = Resource.String.entry_tags;
							break;
						default:
							//don't disclose protected strings:
							if (CurrentEntry.Strings.Get(context.Key).IsProtected)
								return null;
							break;
					}

					if (intlResourceId > 0)
					{
						return Application.Context.GetString(intlResourceId) + ": "+context.Value;
					}
					return context.Key + ": " + context.Value;
				}
				catch (Exception)
				{
					return null;
				}
				
				


				
			}

			public override bool IsNull(int column)
			{
				return false;
			}

			#region Data types appearing in no columns
			public override int GetInt(int column) { throw new FormatException(); }
			public override double GetDouble(int column) { throw new FormatException(); }
			public override float GetFloat(int column) { throw new FormatException(); }
			public override short GetShort(int column) { throw new FormatException(); }
			#endregion
		}

	}
}

