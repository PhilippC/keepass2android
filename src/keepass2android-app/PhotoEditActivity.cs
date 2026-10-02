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
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Preferences;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
  /// <summary>
  /// The shot of the camera before it goes into the entry: cropped by a frame, turned by quarters, and stored at one of
  /// <see cref="Sizes"/> - each shows how many bytes it takes in the database, for this very crop. The choice is kept
  /// for the next shot. The photo never leaves the app: the shot is read from the cache and deleted when done.
  /// </summary>
  [Activity(Label = "@string/app_name", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden,
      Theme = "@style/Kp2aTheme_ActionBar")]
  public class PhotoEditActivity : LockCloseActivity
  {
    /// <summary>The sizes offered: the long side in pixels and the JPEG quality.</summary>
    static readonly (int Name, int Edge, int Quality)[] Sizes =
    {
      (Resource.String.photo_size_tiny, 320, 40),
      (Resource.String.photo_size_small, 480, 50),
      (Resource.String.photo_size_medium, 800, 60),
      (Resource.String.photo_size_large, 1280, 70),
      (Resource.String.photo_size_full, PhotoCapture.WorkEdge, 85),
    };

    const string PrefSize = "photo_size";

    CropView _crop;
    RadioButton[] _choices;
    Bitmap _photo;
    /// <summary>Counts the edits: sizes measured for an older crop are not shown.</summary>
    int _generation;

    protected override void OnCreate(Bundle savedInstanceState)
    {
      base.OnCreate(savedInstanceState);
      SupportActionBar.Title = GetString(Resource.String.photo_edit_title);

      _photo = PhotoCapture.LoadUpright(PhotoCapture.ShotPath(this), PhotoCapture.WorkEdge);
      if (_photo == null)
      {
        Finish();
        return;
      }

      float dp = Resources.DisplayMetrics.Density;
      var root = new LinearLayout(this) { Orientation = Orientation.Vertical };

      _crop = new CropView(this) { Changed = Measure };
      _crop.SetPhoto(_photo);
      root.AddView(_crop, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

      var tools = new LinearLayout(this) { Orientation = Orientation.Horizontal };
      tools.AddView(ToolButton("↺", () => Turn(false)), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
      tools.AddView(ToolButton("↻", () => Turn(true)), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
      tools.AddView(ToolButton(GetString(Resource.String.photo_reset), () => { _crop.SetPhoto(_photo); Measure(); }),
          new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
      root.AddView(tools);

      var group = new RadioGroup(this) { Orientation = Orientation.Vertical };
      group.SetPadding((int)(16 * dp), 0, (int)(16 * dp), 0);
      _choices = new RadioButton[Sizes.Length];
      int chosen = Math.Clamp(PreferenceManager.GetDefaultSharedPreferences(this).GetInt(PrefSize, 1), 0, Sizes.Length - 1);
      for (int i = 0; i < Sizes.Length; i++)
      {
        _choices[i] = new RadioButton(this) { Id = View.GenerateViewId() };
        group.AddView(_choices[i]);
      }
      _choices[chosen].Checked = true;
      root.AddView(group);

      var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
      actions.AddView(ToolButton(GetString(Android.Resource.String.Cancel), Cancel), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
      actions.AddView(ToolButton(GetString(Resource.String.photo_save), Save), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
      root.AddView(actions);

      // edge to edge since Android 15: keep the buttons above the navigation bar
      root.SetOnApplyWindowInsetsListener(new BottomInset());
      SetContentView(root);
      Measure();
    }

    Button ToolButton(string text, Action click)
    {
      var b = new Button(this) { Text = text };
      b.SetAllCaps(false);
      b.Click += (s, e) => click();
      return b;
    }

    void Turn(bool clockwise)
    {
      var m = new Matrix();
      m.PostRotate(clockwise ? 90 : 270);
      Rect crop = _crop.Crop;
      int w = _photo.Width;
      _photo = Bitmap.CreateBitmap(_photo, 0, 0, _photo.Width, _photo.Height, m, true);
      // the frame turns with the photo
      var turned = clockwise
          ? new Rect(_photo.Width - crop.Bottom, crop.Left, _photo.Width - crop.Top, crop.Right)
          : new Rect(crop.Top, w - crop.Right, crop.Bottom, w - crop.Left);
      _crop.SetPhoto(_photo, turned);
      Measure();
    }

    /// <summary>The bytes of each size for the current crop, measured off the main thread.</summary>
    void Measure()
    {
      int generation = ++_generation;
      Bitmap photo = _photo;
      Rect crop = new Rect(_crop.Crop);
      int longSide = Math.Max(crop.Width(), crop.Height());
      for (int i = 0; i < Sizes.Length; i++)
        _choices[i].Text = Label(i, Math.Min(Sizes[i].Edge, longSide), null);
      Task.Run(() =>
      {
        for (int i = 0; i < Sizes.Length && generation == _generation; i++)
        {
          int bytes = PhotoCapture.Encode(photo, crop, Sizes[i].Edge, Sizes[i].Quality).Length;
          int index = i;
          RunOnUiThread(() =>
          {
            if (generation == _generation)
              _choices[index].Text = Label(index, Math.Min(Sizes[index].Edge, longSide), bytes);
          });
        }
      });
    }

    string Label(int i, int edge, int? bytes)
    {
      string size = bytes == null ? "…" : bytes < 1024 * 1024 ? $"{(bytes.Value + 512) / 1024} KB" : $"{bytes.Value / (1024f * 1024f):0.0} MB";
      return $"{GetString(Sizes[i].Name)} · {edge} px · {size}";
    }

    int Chosen()
    {
      for (int i = 0; i < _choices.Length; i++)
        if (_choices[i].Checked)
          return i;
      return 0;
    }

    void Save()
    {
      int i = Chosen();
      PreferenceManager.GetDefaultSharedPreferences(this).Edit().PutInt(PrefSize, i).Apply();
      File.WriteAllBytes(PhotoCapture.ResultPath(this), PhotoCapture.Encode(_photo, _crop.Crop, Sizes[i].Edge, Sizes[i].Quality));
      PhotoCapture.DeleteShot(this);
      SetResult(Result.Ok);
      Finish();
    }

    void Cancel()
    {
      PhotoCapture.DeleteShot(this);
      SetResult(Result.Canceled);
      Finish();
    }

    public override void OnBackPressed() => Cancel();

    class BottomInset : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
      public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
      {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
          v.SetPadding(0, 0, 0, insets.GetInsets(WindowInsets.Type.SystemBars()).Bottom);
        return insets;
      }
    }
  }

  /// <summary>
  /// The photo fitted into the view with a frame over it: a corner or a side is dragged to crop, the inside to move the
  /// frame. The part outside the frame is dimmed. <see cref="Changed"/> is called when a drag ends.
  /// </summary>
  public class CropView : View
  {
    public Action Changed;

    Bitmap _photo;
    /// <summary>The frame in pixels of the photo.</summary>
    Rect _crop = new Rect();
    float _scale, _ox, _oy;

    // what the finger holds: the sides of the frame it moves
    bool _left, _top, _right, _bottom, _all;
    float _lastX, _lastY;

    readonly Paint _dim = new Paint { Color = Color.Argb(150, 0, 0, 0) };
    readonly Paint _frame = new Paint { Color = Color.White, StrokeWidth = 2 };
    readonly Paint _handle = new Paint { Color = Color.White, StrokeWidth = 6 };
    readonly float _grip;

    public CropView(Context ctx) : base(ctx)
    {
      float dp = ctx.Resources.DisplayMetrics.Density;
      _frame.StrokeWidth = 2 * dp;
      _frame.SetStyle(Paint.Style.Stroke);
      _handle.StrokeWidth = 4 * dp;
      _grip = 28 * dp;
    }

    public Rect Crop => _crop;

    public void SetPhoto(Bitmap photo, Rect crop = null)
    {
      _photo = photo;
      _crop = crop ?? new Rect(0, 0, photo.Width, photo.Height);
      Fit();
      Invalidate();
    }

    protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
    {
      base.OnSizeChanged(w, h, oldw, oldh);
      Fit();
    }

    void Fit()
    {
      if (_photo == null || Width == 0)
        return;
      float margin = _grip / 2;
      _scale = Math.Min((Width - 2 * margin) / _photo.Width, (Height - 2 * margin) / _photo.Height);
      _ox = (Width - _photo.Width * _scale) / 2;
      _oy = (Height - _photo.Height * _scale) / 2;
    }

    float X(int px) => _ox + px * _scale;
    float Y(int px) => _oy + px * _scale;

    protected override void OnDraw(Canvas canvas)
    {
      if (_photo == null)
        return;
      var dst = new RectF(_ox, _oy, _ox + _photo.Width * _scale, _oy + _photo.Height * _scale);
      canvas.DrawBitmap(_photo, null, dst, null);
      float l = X(_crop.Left), t = Y(_crop.Top), r = X(_crop.Right), b = Y(_crop.Bottom);
      canvas.DrawRect(dst.Left, dst.Top, dst.Right, t, _dim);
      canvas.DrawRect(dst.Left, b, dst.Right, dst.Bottom, _dim);
      canvas.DrawRect(dst.Left, t, l, b, _dim);
      canvas.DrawRect(r, t, dst.Right, b, _dim);
      canvas.DrawRect(l, t, r, b, _frame);
      float c = _grip / 2;
      foreach (var (x, y, dx, dy) in new[] { (l, t, 1, 1), (r, t, -1, 1), (l, b, 1, -1), (r, b, -1, -1) })
      {
        canvas.DrawLine(x, y, x + dx * c, y, _handle);
        canvas.DrawLine(x, y, x, y + dy * c, _handle);
      }
    }

    public override bool OnTouchEvent(MotionEvent e)
    {
      if (_photo == null)
        return false;
      switch (e.Action)
      {
        case MotionEventActions.Down:
          float l = X(_crop.Left), t = Y(_crop.Top), r = X(_crop.Right), b = Y(_crop.Bottom);
          bool inY = e.GetY() > t - _grip && e.GetY() < b + _grip;
          bool inX = e.GetX() > l - _grip && e.GetX() < r + _grip;
          _left = inY && Math.Abs(e.GetX() - l) < _grip;
          _right = inY && !_left && Math.Abs(e.GetX() - r) < _grip;
          _top = inX && Math.Abs(e.GetY() - t) < _grip;
          _bottom = inX && !_top && Math.Abs(e.GetY() - b) < _grip;
          _all = !(_left || _right || _top || _bottom) && e.GetX() > l && e.GetX() < r && e.GetY() > t && e.GetY() < b;
          _lastX = e.GetX();
          _lastY = e.GetY();
          return _left || _right || _top || _bottom || _all;
        case MotionEventActions.Move:
          int dx = (int)((e.GetX() - _lastX) / _scale), dy = (int)((e.GetY() - _lastY) / _scale);
          if (dx == 0 && dy == 0)
            return true;
          _lastX += dx * _scale;
          _lastY += dy * _scale;
          Drag(dx, dy);
          Invalidate();
          return true;
        case MotionEventActions.Up:
        case MotionEventActions.Cancel:
          Changed?.Invoke();
          return true;
      }
      return base.OnTouchEvent(e);
    }

    void Drag(int dx, int dy)
    {
      int min = Math.Max(16, Math.Min(_photo.Width, _photo.Height) / 20);
      if (_all)
      {
        dx = Math.Clamp(dx, -_crop.Left, _photo.Width - _crop.Right);
        dy = Math.Clamp(dy, -_crop.Top, _photo.Height - _crop.Bottom);
        _crop.Offset(dx, dy);
        return;
      }
      if (_left) _crop.Left = Math.Clamp(_crop.Left + dx, 0, _crop.Right - min);
      if (_right) _crop.Right = Math.Clamp(_crop.Right + dx, _crop.Left + min, _photo.Width);
      if (_top) _crop.Top = Math.Clamp(_crop.Top + dy, 0, _crop.Bottom - min);
      if (_bottom) _crop.Bottom = Math.Clamp(_crop.Bottom + dy, _crop.Top + min, _photo.Height);
    }
  }
}
