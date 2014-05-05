using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

namespace keepass2android
{
	/// <summary>
	/// Shows text as a drawable.
	/// </summary>
	/// Based on http://stackoverflow.com/questions/3972445/how-to-put-text-in-a-drawable
	public class TextDrawable: Drawable {

		private readonly String _text;
		private readonly Paint _paint;
		private static Typeface _iconFont;

		public TextDrawable(String text, Context ctx) {

			_text = text;


			if (_iconFont == null)
				_iconFont = Typeface.CreateFromAsset(ctx.Assets, "fontawesome-webfont.ttf");

			_paint = new Paint {Color = (Color.White), TextSize = 22f, AntiAlias = true};
			//_paint.SetTypeface(_iconFont);
			_paint.SetShadowLayer(6f, 0, 0, Color.Black);
			_paint.SetStyle(Paint.Style.Fill);
			_paint.TextAlign = Paint.Align.Left;
		}

		
		public override void Draw(Canvas canvas) {
			canvas.DrawText("x"+_text, 0, 0, _paint);
		}

		
		public override void SetAlpha(int alpha) {
			_paint.Alpha = alpha;
		}


		public override void SetColorFilter(ColorFilter cf)
		{
			_paint.SetColorFilter(cf);
		}

		public override int Opacity
		{
			get { return -3; /*translucent*/ }
		}

		
	}
}