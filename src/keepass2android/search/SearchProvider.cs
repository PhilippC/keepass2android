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
		public const String Authority = "kp2a." + AppNames.PackagePart + ".SearchProvider";
		
		private const string GetIconPathQuery = "get_icon";
		private const string IconIdParameter = "IconId";
		private const string CustomIconUuidParameter = "CustomIconUuid";
	    private const string DatabaseIndexParameter = "DatabaseIndex";

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
							    List<EntryListCursor.EntryWithContext> entriesWithContext = new List<EntryListCursor.EntryWithContext>();
							    int dbIndex = 0;
							    foreach (var db in App.Kp2a.OpenDatabases)
							    {
							        var resultsContexts = new Dictionary<PwUuid, KeyValuePair<string, string>>();
							        PwGroup group = db.Search(new SearchParameters { SearchString = searchString }, resultsContexts);

							        foreach (var entry in group.Entries)
							        {
							            KeyValuePair<string, string> context;
							            resultsContexts.TryGetValue(entry.Uuid, out context);
                                        entriesWithContext.Add(new EntryListCursor.EntryWithContext(entry, context, dbIndex));
                                    }
							        dbIndex++;

							    }
								
								return new EntryListCursor(entriesWithContext);
							}
							catch (Exception e)
							{
								Kp2aLog.LogUnexpectedError(new Exception("Failed to search for suggestions", e));
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


        public static float convertDpToPixel(float dp, Context context)
        {
            Resources resources = context.Resources;
            Android.Util.DisplayMetrics metrics = resources.DisplayMetrics;
            float px = dp * metrics.Density;
            return px;
        }

        public override ParcelFileDescriptor OpenFile(Android.Net.Uri uri, string mode)
		{
			switch ((UriMatches)UriMatcher.Match(uri))
			{
				case UriMatches.GetIcon:
					var iconId = (PwIcon)Enum.Parse(typeof(PwIcon), uri.GetQueryParameter(IconIdParameter));
					var customIconUuid = new PwUuid(MemUtil.HexStringToByteArray(uri.GetQueryParameter(CustomIconUuidParameter)));
				    int databaseIndex = int.Parse(uri.GetQueryParameter(DatabaseIndexParameter));
				    List<Database> databases = App.Kp2a.OpenDatabases.ToList();
				    Database database = databases[databaseIndex];


                    var iconDrawable = database.DrawableFactory.GetIconDrawable(App.Context, database.KpDatabase, iconId, customIconUuid, false) as BitmapDrawable;
					if (iconDrawable?.Bitmap != null)

                    {
						var pipe = ParcelFileDescriptor.CreatePipe();
						var outStream = new OutputStreamInvoker(new ParcelFileDescriptor.AutoCloseOutputStream(pipe[1]));

						ThreadPool.QueueUserWorkItem(state =>
							{
                                var original = iconDrawable.Bitmap;
                                Bitmap copy = Bitmap.CreateBitmap(original.Width, original.Height, original.GetConfig() ?? Bitmap.Config.Argb8888);
                                Canvas copiedCanvas = new Canvas(copy);
                                copiedCanvas.DrawBitmap(original, 0f, 0f, null);

                                var bitmap = copy;
                                float maxSize = convertDpToPixel(60, App.Context);
                                float scale = Math.Min(maxSize / bitmap.Width, maxSize / bitmap.Height);
                                var scaleWidth = (int)(bitmap.Width * scale);
                                var scaleHeight = (int)(bitmap.Height * scale);
                                var scaledBitmap = Bitmap.CreateScaledBitmap(bitmap, scaleWidth, scaleHeight, true);
                                Bitmap newRectBitmap = Bitmap.CreateBitmap((int)maxSize, (int)maxSize, Bitmap.Config.Argb8888);

                                Canvas c = new Canvas(newRectBitmap);
                                c.DrawBitmap(scaledBitmap, (maxSize - scaledBitmap.Width) / 2.0f, (maxSize - scaledBitmap.Height) / 2.0f, null);
                                bitmap = newRectBitmap;
                                bitmap.Compress(Bitmap.CompressFormat.Png, 100, outStream);
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


		private class EntryListCursor : AbstractCursor
		{
		    private readonly List<EntryWithContext> _entriesWithContexts;

		    private static readonly string[] ColumnNames = new[] { Android.Provider.BaseColumns.Id, 
																	SearchManager.SuggestColumnText1, 
																	SearchManager.SuggestColumnText2, 
																	SearchManager.SuggestColumnIcon1,
																	SearchManager.SuggestColumnIntentDataId,
			};

			

		    public struct EntryWithContext
		    {
		        public readonly PwEntry entry;
		        public readonly KeyValuePair<string, string> resultContext;
		        public readonly int DatabaseIndex;

		        public EntryWithContext(PwEntry entry, KeyValuePair<string, string> mResultContext, int databaseIndex)
		        {
		            this.entry = entry;
		            this.resultContext = mResultContext;
		            DatabaseIndex = databaseIndex;
		        }
		    }

			public EntryListCursor(List<EntryWithContext> entriesWithContexts)
			{
			    _entriesWithContexts = entriesWithContexts;
			}

			public override int Count
			{
				get { return _entriesWithContexts.Count; }
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
                    if (MPos < _entriesWithContexts.Count)
					    return _entriesWithContexts[MPos].entry;
                    return null;
                    
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
                        if (MPos < _entriesWithContexts.Count)
                            return Internationalise(_entriesWithContexts[MPos].resultContext);
					    return "";
					case 3: // SuggestColumnIcon1
						var builder = new Android.Net.Uri.Builder();
						builder.Scheme(ContentResolver.SchemeContent);
						builder.Authority(Authority);
						builder.Path(GetIconPathQuery);
						builder.AppendQueryParameter(IconIdParameter, CurrentEntry.IconId.ToString());
						builder.AppendQueryParameter(CustomIconUuidParameter, CurrentEntry.CustomIconUuid.ToHexString());
					    builder.AppendQueryParameter(DatabaseIndexParameter, _entriesWithContexts[MPos].DatabaseIndex.ToString());
                        return builder.Build().ToString();
					case 4: // SuggestColumnIntentDataId
						return new ElementAndDatabaseId(App.Kp2a.FindDatabaseForElement(CurrentEntry),CurrentEntry).FullId;
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

