using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Utility;
using Object = Java.Lang.Object;

namespace keepass2android
{

	public class ZoomableImageView : ImageView
	{

		private class ScaleListener : ScaleGestureDetector.SimpleOnScaleGestureListener
		{

			private ZoomableImageView parent;

			public ScaleListener(ZoomableImageView parent)
			{
				this.parent = parent;
			}

			public override bool OnScaleBegin(ScaleGestureDetector detector)
			{
				parent.mode = ZOOM;
				return true;
			}


			public override bool OnScale(ScaleGestureDetector detector)
			{
				float scaleFactor = detector.ScaleFactor;
				float newScale = parent.saveScale * scaleFactor;
				if (newScale < parent.maxScale && newScale > parent.minScale)
				{
					parent.saveScale = newScale;
					float width = parent.Width;
					float height = parent.Height;
					parent.right = (parent.originalBitmapWidth * parent.saveScale) - width;
					parent.bottom = (parent.originalBitmapHeight * parent.saveScale) - height;

					float scaledBitmapWidth = parent.originalBitmapWidth * parent.saveScale;
					float scaledBitmapHeight = parent.originalBitmapHeight * parent.saveScale;

					if (scaledBitmapWidth <= width || scaledBitmapHeight <= height)
					{
						parent.matrix.PostScale(scaleFactor, scaleFactor, width / 2, height / 2);
					}
					else
					{
						parent.matrix.PostScale(scaleFactor, scaleFactor, detector.FocusX, detector.FocusY);
					}
				}
				return true;
			}

		}

		const int NONE = 0;
		const int DRAG = 1;
		const int ZOOM = 2;
		const int CLICK = 3;

		private int mode = NONE;

		private Matrix matrix = new Matrix();

		private PointF last = new PointF();
		private PointF start = new PointF();
		private float minScale = 1.0f;
		private float maxScale = 100.0f;
		private float[] m;

		private float redundantXSpace, redundantYSpace;
		private float saveScale = 1f;
		private float right, bottom, originalBitmapWidth, originalBitmapHeight;

		private ScaleGestureDetector mScaleDetector;
		protected ZoomableImageView(IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}
		public ZoomableImageView(Context context)
			: base(context)
		{

			init(context);
		}

		public ZoomableImageView(Context context, IAttributeSet attrs)
			: base(context, attrs)
		{
			init(context);
		}

		public ZoomableImageView(Context context, IAttributeSet attrs, int defStyleAttr)
			: base(context, attrs, defStyleAttr)
		{
			init(context);
		}

		private void init(Context context)
		{
			base.Clickable = true;
			mScaleDetector = new ScaleGestureDetector(context, new ScaleListener(this));
			m = new float[9];
			ImageMatrix = matrix;
			SetScaleType(ScaleType.Matrix);
		}


		protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
		{
			base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
			int bmHeight = getBmHeight();
			int bmWidth = getBmWidth();

			float width = MeasuredWidth;
			float height = MeasuredHeight;
			//Fit to screen.
			float scale = width > height ? height / bmHeight : width / bmWidth;

			matrix.SetScale(scale, scale);
			saveScale = 1f;

			originalBitmapWidth = scale * bmWidth;
			originalBitmapHeight = scale * bmHeight;

			// Center the image
			redundantYSpace = (height - originalBitmapHeight);
			redundantXSpace = (width - originalBitmapWidth);

			matrix.PostTranslate(redundantXSpace / 2, redundantYSpace / 2);

			ImageMatrix = matrix;
		}


		public override bool OnTouchEvent(MotionEvent event_)
		{
			mScaleDetector.OnTouchEvent(event_);

			matrix.GetValues(m);
			float x = m[Matrix.MtransX];
			float y = m[Matrix.MtransY];
			PointF curr = new PointF(event_.GetX(), event_.GetY());
			Log.Debug("TOUCH", event_.Action.ToString(), " mode=" + mode);
			switch (event_.Action)
			{
				//when one finger is touching
				//set the mode to DRAG
				case MotionEventActions.Down:
					last.Set(event_.GetX(), event_.GetY());
					start.Set(last);
					mode = DRAG;
					break;
				//when two fingers are touching
				//set the mode to ZOOM
				case MotionEventActions.Pointer2Down:
				case MotionEventActions.PointerDown:
					last.Set(event_.GetX(), event_.GetY());
					start.Set(last);
					mode = ZOOM;
					break;
				//when a finger moves
				//If mode is applicable move image
				case MotionEventActions.Move:
					//if the mode is ZOOM or
					//if the mode is DRAG and already zoomed
					if (mode == ZOOM || (mode == DRAG && saveScale > minScale))
					{
						float deltaX = curr.X - last.X;// x difference
						float deltaY = curr.Y - last.Y;// y difference
						float scaleWidth = (float)System.Math.Round(originalBitmapWidth * saveScale);// width after applying current scale
						float scaleHeight = (float)System.Math.Round(originalBitmapHeight * saveScale);// height after applying current scale

						bool limitX = false;
						bool limitY = false;

						//if scaleWidth is smaller than the views width
						//in other words if the image width fits in the view
						//limit left and right movement
						if (scaleWidth < Width && scaleHeight < Height)
						{
							// don't do anything
						}
						else if (scaleWidth < Width)
						{
							deltaX = 0;
							limitY = true;
						}
						//if scaleHeight is smaller than the views height
						//in other words if the image height fits in the view
						//limit up and down movement
						else if (scaleHeight < Height)
						{
							deltaY = 0;
							limitX = true;
						}
						//if the image doesnt fit in the width or height
						//limit both up and down and left and right
						else
						{
							limitX = true;
							limitY = true;
						}

						if (limitY)
						{
							if (y + deltaY > 0)
							{
								deltaY = -y;
							}
							else if (y + deltaY < -bottom)
							{
								deltaY = -(y + bottom);
							}

						}

						if (limitX)
						{
							if (x + deltaX > 0)
							{
								deltaX = -x;
							}
							else if (x + deltaX < -right)
							{
								deltaX = -(x + right);
							}

						}
						//move the image with the matrix
						matrix.PostTranslate(deltaX, deltaY);
						//set the last touch location to the current
						last.Set(curr.X, curr.Y);
					}
					break;
				//first finger is lifted
				case MotionEventActions.Up:
					mode = NONE;
					int xDiff = (int)System.Math.Abs(curr.X - start.X);
					int yDiff = (int)System.Math.Abs(curr.Y - start.Y);
					if (xDiff < CLICK && yDiff < CLICK)
						PerformClick();
					break;
				// second finger is lifted
				case MotionEventActions.Pointer2Up:
				case MotionEventActions.PointerUp:
					mode = NONE;
					break;
			}
			ImageMatrix = matrix;
			Invalidate();
			return true;
		}

		public void setMaxZoom(float x)
		{
			maxScale = x;
		}

		private int getBmWidth()
		{
			Drawable drawable = Drawable;
			if (drawable != null)
			{
				return drawable.IntrinsicWidth;
			}
			return 0;
		}

		private int getBmHeight()
		{
			Drawable drawable = Drawable;
			if (drawable != null)
			{
				return drawable.IntrinsicHeight;
			}
			return 0;
		}
	}
	[Activity(Label = "@string/app_name", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
		Theme = "@style/MyTheme_ActionBar")]
	public class ImageViewActivity : LockCloseActivity
	{
		private ActivityDesign _activityDesign;

		public ImageViewActivity()
		{
			_activityDesign = new ActivityDesign(this);
		}

		protected override void OnResume()
		{
			base.OnResume();
			_activityDesign.ReapplyTheme();
		}

		protected override void OnCreate(Bundle savedInstanceState)
		{
			_activityDesign.ApplyTheme(); 
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.ImageViewActivity);

		    ElementAndDatabaseId fullId = new ElementAndDatabaseId(Intent.GetStringExtra("EntryId"));

            var uuid = new PwUuid(MemUtil.HexStringToByteArray(fullId.ElementIdString));
			string key = Intent.GetStringExtra("EntryKey");
			var binary = App.Kp2a.GetDatabase(fullId.DatabaseId).EntriesById[uuid].Binaries.Get(key);
			SupportActionBar.Title = key;
			byte[] pbdata = binary.ReadData();

			var bmp = BitmapFactory.DecodeByteArray(pbdata,0,pbdata.Length);

			FindViewById<ImageView>(Resource.Id.imageView).SetImageBitmap(bmp);

		}
	}
}