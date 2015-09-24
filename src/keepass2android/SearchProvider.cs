using System;
using Android.App;
using Android.Content;
using Android.Database;
using Uri = Android.Net.Uri;

namespace MaterialTest2
{
	[ContentProvider(new string[] {"MaterialTest2.SearchProvider"},Exported=false)]
	public class SearchProvider: ContentProvider
	{
		public override int Delete(Uri uri, string selection, string[] selectionArgs)
		{
			return 0;
		}

		public override string GetType(Uri uri)
		{
			return null;
		}

		public override Uri Insert(Uri uri, ContentValues values)
		{
			return null;
		}

		public override bool OnCreate()
		{
			return false;
		}

		public override Android.Database.ICursor Query(Android.Net.Uri uri, string[] projection, string selection, string[] selectionArgs, string sortOrder)
		{
			var c = new MatrixCursor(new String[] { "_id", SearchManager.SuggestColumnText1, "lat", "lng" });

			c.AddRow(new Java.Lang.Object[] { 123, "description", "lat", "lng" });
			c.AddRow(new Java.Lang.Object[] { 1243, "description 2", "lat", "lng" });
			c.AddRow(new Java.Lang.Object[] { 1235, "description", "lat", "lng" });
			c.AddRow(new Java.Lang.Object[] { 12436, "description 2", "lat", "lng" });
			return c;
		}

		public override int Update(Uri uri, ContentValues values, string selection, string[] selectionArgs)
		{
			return 0;
		}
	}
}