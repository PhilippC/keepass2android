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
using System.Text;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Pdf;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Dialog;

namespace keepass2android
{
  /// <summary>
  /// A tile for an attachment in the row under the attachments of an entry: an image, the first page of a PDF, a frame of
  /// a video, the first lines of a text. A tap shows it bigger: the image viewer, the page in a window, the whole text in
  /// a window; a video is handed to another app like «Open» does. Other attachments get no tile.
  /// </summary>
  public static class AttachmentPreview
  {
    /// <summary>Text above this is shown cut in the window.</summary>
    const int MaxText = 256 * 1024;

    public static string SizeText(long bytes) =>
        bytes < 1024 ? bytes + " B"
        : bytes < 1024 * 1024 ? (bytes + 512) / 1024 + " KB"
        : (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";

    /// <summary>The tile of the attachment `key`, `side` pixels square; null - nothing to show of it.</summary>
    public static View Tile(EntryActivity activity, string key, byte[] data, int side)
    {
      try
      {
        Bitmap image = Image(data, side);
        if (image != null)
          return Picture(activity, image, false, () => activity.ShowAttachedImage(key));

        if (IsPdf(data))
        {
          Bitmap page = PdfPage(activity, data, side, out _);
          if (page != null)
            return Picture(activity, page, false, () => ShowPdf(activity, key, data));
        }

        if (IsVideo(data) && Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
          Bitmap frame = VideoFrame(data);
          if (frame != null)
            return Picture(activity, frame, true, () =>
            {
              var uri = activity.WriteBinaryToFile(key, true);
              if (uri != null)
                activity.OpenBinaryFile(uri);
            });
        }

        string text = Text(data, 2048);
        if (text != null)
        {
          var tile = new TextView(activity)
          {
            Text = text,
            Typeface = Typeface.Monospace,
            TextSize = 7
          };
          tile.SetBackgroundColor(Color.Argb(40, 128, 128, 128));
          tile.SetPadding(8, 8, 8, 8);
          tile.Click += (s, e) => ShowText(activity, key, data);
          return tile;
        }
      }
      catch (Exception e)
      {
        Kp2aLog.Log(e.ToString());
      }
      return null;
    }

    static View Picture(Context ctx, Bitmap bitmap, bool play, Action open)
    {
      var image = new ImageView(ctx);
      image.SetImageBitmap(bitmap);
      image.SetScaleType(ImageView.ScaleType.CenterCrop);
      View view = image;
      if (play)
      {
        var frame = new FrameLayout(ctx);
        frame.AddView(image);
        var mark = new TextView(ctx) { Text = "▶", TextSize = 24, Gravity = GravityFlags.Center };
        mark.SetTextColor(Color.White);
        mark.SetShadowLayer(4, 0, 0, Color.Black);
        frame.AddView(mark, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        view = frame;
      }
      view.Click += (s, e) => open();
      return view;
    }

    static Bitmap Image(byte[] data, int side)
    {
      var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
      BitmapFactory.DecodeByteArray(data, 0, data.Length, bounds);
      if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
        return null;
      int sample = 1;
      while (Math.Min(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= side)
        sample *= 2;
      return BitmapFactory.DecodeByteArray(data, 0, data.Length, new BitmapFactory.Options { InSampleSize = sample });
    }

    static bool IsPdf(byte[] d) => d.Length > 5 && d[0] == '%' && d[1] == 'P' && d[2] == 'D' && d[3] == 'F' && d[4] == '-';

    /// <summary>MP4, MOV, 3GP (a box `ftyp` first) or Matroska, WebM.</summary>
    static bool IsVideo(byte[] d) =>
        d.Length > 12 && ((d[4] == 'f' && d[5] == 't' && d[6] == 'y' && d[7] == 'p')
                          || (d[0] == 0x1A && d[1] == 0x45 && d[2] == 0xDF && d[3] == 0xA3));

    /// <summary>
    /// The first page `width` pixels wide. The renderer reads only a file: the PDF is in the private cache for the moment
    /// it is read, as «Open» keeps an attachment there too.
    /// </summary>
    static Bitmap PdfPage(Context ctx, byte[] data, int width, out int pages)
    {
      pages = 0;
      var dir = new Java.IO.File(ctx.CacheDir, "AttachmentPreview");
      dir.Mkdirs();
      var file = new Java.IO.File(dir, "preview.pdf");
      try
      {
        System.IO.File.WriteAllBytes(file.AbsolutePath, data);
        using var fd = ParcelFileDescriptor.Open(file, ParcelFileMode.ReadOnly);
        using var renderer = new PdfRenderer(fd);
        pages = renderer.PageCount;
        if (pages == 0)
          return null;
        using var page = renderer.OpenPage(0);
        int height = Math.Max(1, width * page.Height / Math.Max(1, page.Width));
        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);
        bitmap.EraseColor(Color.White);
        page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);
        page.Close();
        return bitmap;
      }
      finally
      {
        file.Delete();
      }
    }

    static Bitmap VideoFrame(byte[] data)
    {
      using var retriever = new MediaMetadataRetriever();
      retriever.SetDataSource(new BytesSource(data));
      return retriever.GetFrameAtTime(-1);
    }

    /// <summary>The start of `data` as text, at most `max` bytes of it; null - not text (not UTF-8, or binary codes).</summary>
    static string Text(byte[] data, int max)
    {
      int n = Math.Min(data.Length, max);
      if (n == 0)
        return null;
      int controls = 0;
      for (int i = 0; i < n; i++)
      {
        byte b = data[i];
        if (b == 0)
          return null;
        if (b < 0x20 && b != '\n' && b != '\r' && b != '\t')
          controls++;
      }
      if (controls > n / 100)
        return null;
      try
      {
        // a strict decoder that leaves a character cut at the end for later instead of failing on it
        var decoder = new UTF8Encoding(false, true).GetDecoder();
        var chars = new char[n];
        int count = decoder.GetChars(data, 0, n, chars, 0, false);
        string s = new string(chars, 0, count);
        return s.StartsWith('﻿') ? s.Substring(1) : s;
      }
      catch (DecoderFallbackException)
      {
        return null;
      }
    }

    static void ShowText(EntryActivity activity, string key, byte[] data)
    {
      string text = Text(data, MaxText);
      if (text == null)
        return;
      if (data.Length > MaxText)
        text += "\n…";
      var view = new TextView(activity) { Text = text, Typeface = Typeface.Monospace, TextSize = 12 };
      view.SetTextIsSelectable(true);
      int pad = (int)(16 * activity.Resources.DisplayMetrics.Density);
      view.SetPadding(pad, pad, pad, pad);
      var scroll = new ScrollView(activity);
      scroll.AddView(view);
      new MaterialAlertDialogBuilder(activity)
          .SetTitle(key)
          .SetView(scroll)
          .SetPositiveButton(Android.Resource.String.Ok, (s, e) => { })
          .Show();
    }

    static void ShowPdf(EntryActivity activity, string key, byte[] data)
    {
      Bitmap page = PdfPage(activity, data, activity.Resources.DisplayMetrics.WidthPixels, out int pages);
      if (page == null)
        return;
      var image = new ImageView(activity);
      image.SetImageBitmap(page);
      image.SetAdjustViewBounds(true);
      var scroll = new ScrollView(activity);
      scroll.AddView(image);
      new MaterialAlertDialogBuilder(activity)
          .SetTitle(pages > 1 ? $"{key} · 1 / {pages}" : key)
          .SetView(scroll)
          .SetPositiveButton(Android.Resource.String.Ok, (s, e) => { })
          .Show();
    }

    /// <summary>A video straight from memory: no file of it anywhere.</summary>
    class BytesSource : MediaDataSource
    {
      readonly byte[] _data;

      public BytesSource(byte[] data) => _data = data;

      public override long Size => _data.Length;

      public override int ReadAt(long position, byte[] buffer, int offset, int size)
      {
        if (position >= _data.Length)
          return -1;
        int n = (int)Math.Min(size, _data.Length - position);
        Array.Copy(_data, position, buffer, offset, n);
        return n;
      }

      public override void Close()
      {
      }
    }
  }
}
