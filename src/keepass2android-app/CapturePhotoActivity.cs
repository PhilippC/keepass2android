// This file is part of Keepass2Android, Copyright 2026 Philipp Crocoll.
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

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.View;
using Google.Android.Material.Dialog;
using System;
using System.IO;
using System.Threading;

namespace keepass2android
{
  /// <summary>
  /// Full-screen camera capture activity that keeps the captured JPEG bytes entirely
  /// in memory (never written to disk or the media gallery).
  /// The bytes are returned to the caller via the static <see cref="CapturedPhotoBytes"/>
  /// field; the generated filename is passed as an Intent extra.
  /// </summary>
  [Activity(
      Label = "@string/capture_photo_title",
      ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard
                           | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize,
      Theme = "@style/Kp2aTheme_BlueNoActionBar")]
  public class CapturePhotoActivity : LifecycleAwareActivity
  {
    /// <summary>Key for the filename Intent extra returned on success.</summary>
    public const string ExtraFilename = "capturedFilename";

    /// <summary>
    /// Holds the raw JPEG bytes captured in-memory. Set before SetResult(Ok)+Finish().
    /// The caller is responsible for clearing this after reading.
    /// A static field is used because Intent extras cannot reliably hold large byte arrays.
    /// </summary>
    public static byte[]? CapturedPhotoBytes;

    private const int RequestCodeCameraPermission = 1001;

    // Resize targets for the three size options.
    private const int MaxPxSmall = 800;
    private const int MaxPxMedium = 1600;
    private const int QualitySmall = 70;
    private const int QualityMedium = 85;
    private const int QualityOriginal = 95;

    private ProcessCameraProvider? _cameraProvider;
    private ImageCapture? _imageCapture;
    private int _lensFacing = CameraSelector.LensFacingBack;

    // -----------------------------------------------------------------
    // Activity lifecycle
    // -----------------------------------------------------------------

    protected override void OnCreate(Bundle? savedInstanceState)
    {
      base.OnCreate(savedInstanceState);

      // Go edge-to-edge so the camera preview fills the full screen including
      // behind the system bars; we handle insets manually on the control bar.
      WindowCompat.SetDecorFitsSystemWindows(Window!, false);

      SetContentView(Resource.Layout.capture_photo);

      // Apply navigation-bar inset as extra bottom padding on the control bar
      // so that buttons never overlap the gesture handle or navigation buttons.
      var controlBar = FindViewById<LinearLayout>(Resource.Id.camera_controls)!;
      ViewCompat.SetOnApplyWindowInsetsListener(controlBar, new ControlBarInsetsListener());

      FindViewById<ImageButton>(Resource.Id.btn_capture)!.Click += (_, _) => TakePhoto();
      FindViewById<ImageButton>(Resource.Id.btn_flip_camera)!.Click += (_, _) => FlipCamera();
      FindViewById<ImageButton>(Resource.Id.btn_cancel)!.Click += (_, _) =>
      {
        SetResult(Result.Canceled);
        Finish();
      };

      if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.Camera)
          == Permission.Granted)
      {
        StartCamera();
      }
      else
      {
        AndroidX.Core.App.ActivityCompat.RequestPermissions(
            this,
            new[] { Android.Manifest.Permission.Camera },
            RequestCodeCameraPermission);
      }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
      base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
      if (requestCode == RequestCodeCameraPermission)
      {
        if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
        {
          StartCamera();
        }
        else
        {
          App.Kp2a.ShowMessage(this,
              GetString(Resource.String.camera_permission_required),
              MessageSeverity.Error);
          SetResult(Result.Canceled);
          Finish();
        }
      }
    }

    // -----------------------------------------------------------------
    // Camera setup
    // -----------------------------------------------------------------

    private void StartCamera()
    {
      var future = ProcessCameraProvider.GetInstance(this);
      future.AddListener(
          new Java.Lang.Runnable(() =>
          {
            _cameraProvider = (ProcessCameraProvider)future.Get();
            BindCamera();
          }),
          new MainThreadExecutor());
    }

    private void BindCamera()
    {
      if (_cameraProvider == null) return;

      var previewView = FindViewById<PreviewView>(Resource.Id.camera_preview)!;

      var preview = new Preview.Builder().Build();
      preview.SetSurfaceProvider(new MainThreadExecutor(), previewView.SurfaceProvider);

      // CameraX 1.4.x OnImageCapturedCallback delivers JPEG on devices where the sensor
      // natively produces JPEG, and falls back via YUV->Bitmap->JPEG when it does not.
      _imageCapture = new ImageCapture.Builder().Build();

      // Hide the flip button when a front camera is unavailable (e.g. some tablets).
      bool hasFrontCamera;
      try { hasFrontCamera = _cameraProvider.HasCamera(CameraSelector.DefaultFrontCamera); }
      catch { hasFrontCamera = false; }
      FindViewById<ImageButton>(Resource.Id.btn_flip_camera)!.Visibility =
          hasFrontCamera ? ViewStates.Visible : ViewStates.Gone;

      var cameraSelector = new CameraSelector.Builder()
          .RequireLensFacing(_lensFacing)
          .Build();

      try
      {
        _cameraProvider.UnbindAll();
        _cameraProvider.BindToLifecycle(
            (AndroidX.Lifecycle.ILifecycleOwner)this,
            cameraSelector,
            preview,
            _imageCapture);
      }
      catch (Exception ex)
      {
        Kp2aLog.LogUnexpectedError(ex);
        App.Kp2a.ShowMessage(this, ex.Message ?? "Camera error", MessageSeverity.Error);
      }
    }

    private void FlipCamera()
    {
      _lensFacing = _lensFacing == CameraSelector.LensFacingBack
          ? CameraSelector.LensFacingFront
          : CameraSelector.LensFacingBack;
      BindCamera();
    }

    // -----------------------------------------------------------------
    // Capture -> size-selection dialog
    // -----------------------------------------------------------------

    private void TakePhoto()
    {
      var btnCapture = FindViewById<ImageButton>(Resource.Id.btn_capture)!;
      btnCapture.Enabled = false;                 // prevent double-tap

      // Snapshot the current display rotation so we can correct for it during
      // encoding. CameraX 1.4.x always uses ROTATION_0 as its internal target
      // rotation (because this activity never recreates), so its EXIF orientation
      // tag is written for ROTATION_0. We apply the delta between the actual
      // display rotation and ROTATION_0 during post-processing to ensure both
      // portrait and landscape photos come out correctly oriented.
#pragma warning disable CS0618 // DefaultDisplay deprecated in API 30
      int displayRotation = (int)(WindowManager!.DefaultDisplay!.Rotation);
#pragma warning restore CS0618
      _imageCapture?.TakePicture(new MainThreadExecutor(), new InMemoryCaptureCallback(this, displayRotation));
    }

    /// <summary>
    /// Spawns a background thread to compute the three compressed variants,
    /// then shows the size-selection dialog on the UI thread.
    /// </summary>
    private void OnPhotoCaptured(byte[] rawJpeg, int displayRotation)
    {
      ThreadPool.QueueUserWorkItem(_ =>
      {
        byte[]? smallJpeg = null, mediumJpeg = null, originalJpeg = null;
        try
        {
          smallJpeg = ResizeAndEncode(rawJpeg, MaxPxSmall, QualitySmall, displayRotation);
          mediumJpeg = ResizeAndEncode(rawJpeg, MaxPxMedium, QualityMedium, displayRotation);
          originalJpeg = ResizeAndEncode(rawJpeg, int.MaxValue, QualityOriginal, displayRotation);
        }
        catch (Exception ex)
        {
          Kp2aLog.LogUnexpectedError(ex);
          // Graceful fallback: if re-encoding fails, use raw bytes for all options.
          originalJpeg ??= rawJpeg;
          smallJpeg ??= originalJpeg;
          mediumJpeg ??= originalJpeg;
        }

        RunOnUiThread(() => ShowSizeSelectionDialog(smallJpeg!, mediumJpeg!, originalJpeg!));
      });
    }

    private void ShowSizeSelectionDialog(byte[] smallJpeg, byte[] mediumJpeg, byte[] originalJpeg)
    {
      string[] items =
      {
                $"{GetString(Resource.String.capture_size_small)}  ({FormatBytes(smallJpeg.Length)})",
                $"{GetString(Resource.String.capture_size_medium)}  ({FormatBytes(mediumJpeg.Length)})",
                $"{GetString(Resource.String.capture_size_original)}  ({FormatBytes(originalJpeg.Length)})",
            };

      // SetMessage and SetItems are mutually exclusive in AlertDialog — using both
      // suppresses the item list. The warning is shown as the title instead.
      new MaterialAlertDialogBuilder(this)
          .SetTitle(Resource.String.capture_attachment_size_warning)
          .SetItems(items, (_, e) =>
          {
            byte[] chosen = e.Which switch
            {
              0 => smallJpeg,
              1 => mediumJpeg,
              _ => originalJpeg,
            };
            FinishWithBytes(chosen);
          })
          .SetNegativeButton(Android.Resource.String.Cancel, (_, _) =>
          {
            // Let the user retake the shot.
            FindViewById<ImageButton>(Resource.Id.btn_capture)!.Enabled = true;
          })
          .SetCancelable(false)
          .Show();
    }

    private void FinishWithBytes(byte[] jpeg)
    {
      CapturedPhotoBytes = jpeg;
      var filename = $"photo_{Java.Lang.JavaSystem.CurrentTimeMillis()}.jpg";
      var resultIntent = new Intent();
      resultIntent.PutExtra(ExtraFilename, filename);
      SetResult(Result.Ok, resultIntent);
      Finish();
    }

    // -----------------------------------------------------------------
    // Image helpers
    // -----------------------------------------------------------------

    /// <summary>
    /// Decodes the source JPEG, applies any EXIF rotation so the pixel data matches
    /// the visual orientation (important for viewers that ignore EXIF, e.g. KeePassXC
    /// on desktop), optionally scales it down so that its longest side does not exceed
    /// <paramref name="maxPx"/>, then re-encodes as JPEG in RAM.
    /// Pass <see cref="int.MaxValue"/> for <paramref name="maxPx"/> to skip scaling.
    /// </summary>
    private static byte[] ResizeAndEncode(byte[] jpegBytes, int maxPx, int quality, int displayRotation)
    {
      var bmp = BitmapFactory.DecodeByteArray(jpegBytes, 0, jpegBytes.Length)
                ?? throw new InvalidOperationException("Failed to decode captured image.");

      // Read EXIF orientation and rotate/flip the bitmap so the pixel data is
      // already in the correct visual orientation. This ensures that applications
      // that do not honour EXIF metadata (e.g. KeePassXC on desktop) display the
      // image correctly.
      // displayRotation is passed from the capture moment to compensate for CameraX
      // always encoding EXIF as if targetRotation=ROTATION_0 (portrait).
      bmp = ApplyExifOrientation(bmp, jpegBytes, displayRotation);

      if (Math.Max(bmp.Width, bmp.Height) > maxPx)
      {
        float scale = (float)maxPx / Math.Max(bmp.Width, bmp.Height);
        var scaled = Bitmap.CreateScaledBitmap(
            bmp,
            (int)(bmp.Width * scale),
            (int)(bmp.Height * scale),
            true /* bilinear filter */);
        bmp.Recycle();
        bmp = scaled;
      }

      using var ms = new MemoryStream();
      bmp.Compress(Bitmap.CompressFormat.Jpeg, quality, ms);
      bmp.Recycle();
      return ms.ToArray();
    }

    private static string FormatBytes(long bytes)
    {
      if (bytes < 1024L) return $"{bytes} B";
      if (bytes < 1024L * 1024L) return $"{bytes / 1024.0:F1} KB";
      return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    /// <summary>
    /// Reads the EXIF orientation tag from <paramref name="jpegBytes"/> and returns a
    /// new <see cref="Bitmap"/> with the pixels rotated/flipped to match the visual
    /// orientation.  Returns the original bitmap unchanged when no correction is needed.
    /// <paramref name="displayRotation"/> is the Surface.Rotation* value at capture time
    /// (0=portrait, 1=landscape-90, 2=portrait-180, 3=landscape-270). CameraX always
    /// writes EXIF as if targetRotation=ROTATION_0, so we subtract the actual display
    /// rotation to get the true correction angle.
    /// </summary>
    private static Bitmap ApplyExifOrientation(Bitmap source, byte[] jpegBytes, int displayRotation)
    {
      int orientation;
      try
      {
        using var stream = new MemoryStream(jpegBytes);
        var exif = new Android.Media.ExifInterface(stream);
        orientation = exif.GetAttributeInt(
            Android.Media.ExifInterface.TagOrientation,
            (int)Android.Media.Orientation.Normal);
      }
      catch
      {
        return source; // If EXIF read fails, return as-is.
      }

      var matrix = new Matrix();

      // CameraX 1.4.x always encodes EXIF as if targetRotation=ROTATION_0 (portrait).
      // The EXIF tag therefore represents the rotation from sensor space to portrait space,
      // regardless of what the device was actually doing at capture time.
      // We must subtract the actual display rotation (in degrees) from the EXIF correction
      // so that the final pixels are in the true display orientation.
      //   displayRotation: 0=portrait, 1=landscape-90°, 2=portrait-180°, 3=landscape-270°
      int displayDeg = displayRotation * 90;

      // Degrees to rotate the bitmap to go from sensor space to correct visual orientation.
      int exifDeg = (Android.Media.Orientation)orientation switch
      {
        Android.Media.Orientation.Rotate90 => 90,
        Android.Media.Orientation.Rotate180 => 180,
        Android.Media.Orientation.Rotate270 => 270,
        _ => 0,
      };

      // For flip cases we still need the matrix approach; handle them separately.
      bool isFlip = (Android.Media.Orientation)orientation is
          Android.Media.Orientation.FlipHorizontal or
          Android.Media.Orientation.FlipVertical or
          Android.Media.Orientation.Transpose or
          Android.Media.Orientation.Transverse;

      if (isFlip)
      {
        // For flip orientations don't try to compensate display rotation —
        // these are rare (scanner/mirror modes) and never produced by the camera.
        switch ((Android.Media.Orientation)orientation)
        {
          case Android.Media.Orientation.FlipHorizontal:
            matrix.PreScale(-1f, 1f); break;
          case Android.Media.Orientation.FlipVertical:
            matrix.PreScale(1f, -1f); break;
          case Android.Media.Orientation.Transpose:
            matrix.PostRotate(90); matrix.PreScale(-1f, 1f); break;
          case Android.Media.Orientation.Transverse:
            matrix.PostRotate(270); matrix.PreScale(-1f, 1f); break;
        }
      }
      else
      {
        // Net rotation = EXIF correction − display rotation (mod 360)
        int netDeg = ((exifDeg - displayDeg) % 360 + 360) % 360;
        if (netDeg == 0) return source;
        matrix.PostRotate(netDeg);
      }

      var rotated = Bitmap.CreateBitmap(source, 0, 0, source.Width, source.Height, matrix, true);
      if (!ReferenceEquals(rotated, source))
        source.Recycle();
      return rotated;
    }

    // -----------------------------------------------------------------
    // Inner helpers
    // -----------------------------------------------------------------

    /// <summary>
    /// CameraX in-memory capture callback. Keeps JPEG bytes in RAM — nothing is
    /// written to any file or media store.
    /// </summary>
    private class InMemoryCaptureCallback : ImageCapture.OnImageCapturedCallback
    {
      private readonly CapturePhotoActivity _activity;
      private readonly int _displayRotation;

      // Required JNI constructor for .NET-Android interop.
      protected InMemoryCaptureCallback(IntPtr javaRef, JniHandleOwnership transfer)
          : base(javaRef, transfer) { }

      public InMemoryCaptureCallback(CapturePhotoActivity activity, int displayRotation)
      {
        _activity = activity;
        _displayRotation = displayRotation;
      }

      public override void OnCaptureSuccess(IImageProxy image)
      {
        try
        {
          byte[] bytes = ExtractJpegBytes(image);
          _activity.RunOnUiThread(() => _activity.OnPhotoCaptured(bytes, _displayRotation));
        }
        finally
        {
          image.Close();
        }
      }

      /// <summary>
      /// Extracts raw JPEG bytes from the ImageProxy.
      /// When the sensor delivers JPEG natively, <c>planes[0].Buffer</c> contains
      /// the complete JPEG stream and is copied directly. For other formats
      /// (e.g. YUV_420_888 on some devices), the image is decoded to a Bitmap
      /// and re-encoded in RAM — still never touching disk.
      /// </summary>
      private static byte[] ExtractJpegBytes(IImageProxy image)
      {
        if (image.Format == (int)ImageFormatType.Jpeg)
        {
          // Fast path: the buffer is already a complete JPEG.
          var buffer = image.GetPlanes()[0].Buffer;
          var bytes = new byte[buffer.Remaining()];
          buffer.Get(bytes);
          return bytes;
        }

        // Fallback for YUV or other sensor formats: decode to Bitmap, compress in RAM.
        var bitmap = image.ToBitmap();
        using var ms = new MemoryStream();
        bitmap.Compress(Bitmap.CompressFormat.Jpeg, 95, ms);
        bitmap.Recycle();
        return ms.ToArray();
      }

      public override void OnError(ImageCaptureException exception)
      {
        Kp2aLog.LogUnexpectedError(exception);
        _activity.RunOnUiThread(() =>
        {
          _activity.FindViewById<ImageButton>(Resource.Id.btn_capture)!.Enabled = true;
          App.Kp2a.ShowMessage(
                      _activity,
                      exception.Message ?? "Camera capture failed",
                      MessageSeverity.Error);
        });
      }
    }

    /// <summary>
    /// Applies the navigation-bar inset as extra bottom padding on the control bar
    /// so that buttons never overlap the gesture handle or navigation buttons.
    /// The base 12 dp padding is preserved on all four sides.
    /// </summary>
    private class ControlBarInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
      public WindowInsetsCompat OnApplyWindowInsets(View v, WindowInsetsCompat insets)
      {
        var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
        int dp12 = (int)(12 * v.Resources!.DisplayMetrics!.Density);
        v.SetPadding(dp12, dp12, dp12, dp12 + bars.Bottom);
        return insets;
      }
    }

    /// <summary>
    /// Simple IExecutor that always runs on the Android main (UI) thread.
    /// Used to drive the CameraX futures and capture callbacks on the main thread.
    /// </summary>
    private class MainThreadExecutor : Java.Lang.Object, Java.Util.Concurrent.IExecutor
    {
      public void Execute(Java.Lang.IRunnable runnable)
      {
        new Handler(Looper.MainLooper).Post(() => runnable.Run());
      }
    }
  }
}
