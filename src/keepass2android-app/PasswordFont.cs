using Android.Graphics;
using Android.Widget;

namespace keepass2android
{
    public class PasswordFont
    {
        private static Typeface _passwordFont;

        public void ApplyTo(TextView view)
        {
            if (_passwordFont == null)
                _passwordFont = Typeface.CreateFromAsset(view.Context.Assets, "SourceCodePro-Regular.ttf");

            view.Typeface = _passwordFont;
        }
    }
}