// This file is part of Keepass2Android.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Provider;
using Uri = Android.Net.Uri;

namespace keepass2android
{
  /// <summary>
  /// A photo from the camera as an attachment of an entry. The camera app writes the full-size shot into one file of the
  /// cache through <see cref="PhotoCaptureProvider"/>; <see cref="PhotoEditActivity"/> crops, turns and shrinks it to the
  /// size the user picks, and the shot is deleted, so it leaves no trace outside the database.
  /// </summary>
  public static class PhotoCapture
  {
    /// <summary>The long side the shot is loaded with for editing: the largest size offered is not above it.</summary>
    public const int WorkEdge = 2048;

    const string SubDir = "PhotoCapture";
    const string ShotName = "shot.jpg";
    const string ResultName = "result.jpg";

    static Java.IO.File InDir(Context ctx, string name)
    {
      var dir = new Java.IO.File(ctx.CacheDir, SubDir);
      dir.Mkdirs();
      return new Java.IO.File(dir, name);
    }

    static Java.IO.File ShotFile(Context ctx) => InDir(ctx, ShotName);

    public static string ShotPath(Context ctx) => ShotFile(ctx).AbsolutePath;

    /// <summary>Where the edited photo goes back to the entry editor.</summary>
    public static string ResultPath(Context ctx) => InDir(ctx, ResultName).AbsolutePath;

    /// <summary>Starts the camera app for one shot; false - there is no camera app.</summary>
    public static bool Start(Activity activity, int requestCode)
    {
      var shot = ShotFile(activity);
      shot.Delete();
      var uri = Uri.Parse("content://" + PhotoCaptureProvider.Authority + "/" + ShotName);
      var intent = new Intent(MediaStore.ActionImageCapture);
      intent.PutExtra(MediaStore.ExtraOutput, uri);
      intent.ClipData = ClipData.NewRawUri("", uri);
      intent.AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
      try
      {
        activity.StartActivityForResult(intent, requestCode);
        return true;
      }
      catch (ActivityNotFoundException)
      {
        return false;
      }
    }

    /// <summary>The shot upright, the long side at most <paramref name="maxEdge"/>; null - no shot.</summary>
    public static Bitmap LoadUpright(string path, int maxEdge)
    {
      if (!File.Exists(path))
        return null;
      var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
      BitmapFactory.DecodeFile(path, bounds);
      int longSide = Math.Max(bounds.OutWidth, bounds.OutHeight);
      if (longSide <= 0)
        return null;
      // the power of two that still leaves at least maxEdge: decoding at full size would take tens of megabytes
      int sample = 1;
      while (longSide / (sample * 2) >= maxEdge)
        sample *= 2;
      Bitmap b = BitmapFactory.DecodeFile(path, new BitmapFactory.Options { InSampleSize = sample });
      if (b == null)
        return null;

      float k = (float)maxEdge / Math.Max(b.Width, b.Height);
      var m = new Matrix();
      if (k < 1f)
        m.PostScale(k, k);
      int turn = Rotation(path);
      if (turn != 0)
        m.PostRotate(turn);
      if (m.IsIdentity)
        return b;
      Bitmap t = Bitmap.CreateBitmap(b, 0, 0, b.Width, b.Height, m, true);
      if (t != b)
        b.Recycle();
      return t;
    }

    /// <summary>The part <paramref name="crop"/> of <paramref name="src"/>, its long side at most <paramref name="edge"/>, as JPEG.</summary>
    public static byte[] Encode(Bitmap src, Rect crop, int edge, int quality)
    {
      float k = Math.Min(1f, (float)edge / Math.Max(crop.Width(), crop.Height()));
      var m = new Matrix();
      m.PostScale(k, k);
      Bitmap part = Bitmap.CreateBitmap(src, crop.Left, crop.Top, crop.Width(), crop.Height(), m, true);
      using var ms = new MemoryStream();
      part.Compress(Bitmap.CompressFormat.Jpeg, quality, ms);
      if (part != src)
        part.Recycle();
      return ms.ToArray();
    }

    public static void DeleteShot(Context ctx) => ShotFile(ctx).Delete();

    /// <summary>The name of a new photo: when it was taken.</summary>
    public static string Name() => "photo_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".jpg";

    static int Rotation(string path)
    {
      try
      {
        switch (new ExifInterface(path).GetAttributeInt(ExifInterface.TagOrientation, 1))
        {
          case 6: return 90;
          case 3: return 180;
          case 8: return 270;
        }
      }
      catch (Exception e)
      {
        Kp2aLog.Log(e.ToString());
      }
      return 0;
    }
  }

  /// <summary>
  /// The one file the camera app writes a shot into: not exported, reachable only by the app the write permission of
  /// <see cref="PhotoCapture.Start"/> is granted to.
  /// </summary>
  [ContentProvider(new[] { "keepass2android." + AppNames.PackagePart + ".photocapture" }, Exported = false, GrantUriPermissions = true)]
  public class PhotoCaptureProvider : ContentProvider
  {
    public const string Authority = "keepass2android." + AppNames.PackagePart + ".photocapture";

    public override bool OnCreate() => true;

    public override ParcelFileDescriptor OpenFile(Uri uri, string mode)
    {
      if (uri.LastPathSegment != "shot.jpg")
        throw new Java.IO.FileNotFoundException("Unsupported uri: " + uri);
      var dir = new Java.IO.File(Context.CacheDir, "PhotoCapture");
      dir.Mkdirs();
      var file = new Java.IO.File(dir, "shot.jpg");
      var fileMode = mode != null && mode.Contains("w")
        ? ParcelFileMode.WriteOnly | ParcelFileMode.Create | ParcelFileMode.Truncate
        : ParcelFileMode.ReadOnly;
      return ParcelFileDescriptor.Open(file, fileMode);
    }

    public override string GetType(Uri uri) => "image/jpeg";
    public override Android.Database.ICursor Query(Uri uri, string[] projection, string selection, string[] selectionArgs, string sortOrder) => null;
    public override Uri Insert(Uri uri, ContentValues values) => null;
    public override int Delete(Uri uri, string selection, string[] selectionArgs) => 0;
    public override int Update(Uri uri, ContentValues values, string selection, string[] selectionArgs) => 0;
  }
}
