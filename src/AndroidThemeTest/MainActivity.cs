using Android.Content;
using AndroidX.AppCompat.App;

namespace AndroidThemeTest
{
    public class TestActivity : AndroidX.AppCompat.App.AppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            //FindViewById<TextView>(Resource.Id.textView).Click += MainActivity_Click;
            FindViewById<Button>(Resource.Id.with_actionbar).Click += (sender, args) =>
            {
                Intent intent = new Intent(this, typeof(ActivityWithActionBar));
                StartActivity(intent);
            };
            FindViewById<Button>(Resource.Id.with_blue_actionbar).Click += (sender, args) =>
            {
                Intent intent = new Intent(this, typeof(ActivityWithBlueActionBar));
                StartActivity(intent);
            };
            FindViewById<Button>(Resource.Id.without_actionbar).Click += (sender, args) =>
            {
                Intent intent = new Intent(this, typeof(ActivityWithoutActionBar));
                StartActivity(intent);
            };
            FindViewById<Button>(Resource.Id.theme_light).Click += (sender, args) =>
            {
                OnSelectTheme(AppCompatDelegate.ModeNightNo);
            };
            FindViewById<Button>(Resource.Id.theme_dark).Click += (sender, args) =>
            {
                OnSelectTheme(AppCompatDelegate.ModeNightYes);
            };
            FindViewById<Button>(Resource.Id.theme_auto).Click += (sender, args) =>
            {
                OnSelectTheme(AppCompatDelegate.ModeNightFollowSystem);
            };
        }

        void OnSelectTheme(int mode)
        {
            AppCompatDelegate.DefaultNightMode = mode;

        }

    }

    [Activity(Label = "@string/app_name", MainLauncher = true, Theme= "@style/Kp2aTheme_ActionBar")]
    public class ActivityWithActionBar : TestActivity
    {
        
    }


    [Activity(Label = "@string/app_name", Theme = "@style/Kp2aTheme_BlueActionBar")]
    public class ActivityWithBlueActionBar : TestActivity
    {

    }


    [Activity(Label = "@string/app_name", Theme = "@style/Kp2aTheme_NoActionBar")]
    public class ActivityWithoutActionBar : TestActivity
    {

    }
}   