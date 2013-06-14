//
// Copyright (C) 2012 Maya Studios (http://mayastudios.com)
//
// This file is part of MonoDroidUnitTesting.
//
// MonoDroidUnitTesting is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MonoDroidUnitTesting is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with MonoDroidUnitTesting. If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.IO;
using System.Reflection;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Preferences;
using Android.Views;
using Android.Widget;
using MonoDroidUnitTesting.Utils;


namespace MonoDroidUnitTesting {
  public abstract class AbstractResultActivity : Activity {
    protected const string LOG_TAG = "MonoDroid Unit Test";

    protected const string ACTIVITY_PREFS_NAME = "last_activity";
    private const string ASSEMBLY_NAMESPACE = "MonoDroidUnitTesting";
    private const int RESULT_BAR_HEIGHT = 8;

    private readonly string m_iconFileNamePrefix;

    public Drawable IconOutcomeInconclusive { get; private set; }
    public Drawable IconOutcomePassed { get; private set; }
    public Drawable IconOutcomeError { get; private set; }

    protected ResultBarView ResultBar { get; private set; }

    protected AbstractResultActivity(string iconFileNamePrefix) {
      this.m_iconFileNamePrefix = iconFileNamePrefix;
    }

    protected override void OnCreate(Bundle bundle) {
      base.OnCreate(bundle);

      this.IconOutcomePassed = GetDrawable(this.m_iconFileNamePrefix + "outcome_passed.png");
      this.IconOutcomeError = GetDrawable(this.m_iconFileNamePrefix + "outcome_error.png");
      this.IconOutcomeInconclusive = GetDrawable(this.m_iconFileNamePrefix + "outcome_inconclusive.png");

      LinearLayout mainLayout = new LinearLayout(this);
      mainLayout.Orientation = Orientation.Vertical;

      this.ResultBar = new ResultBarView(this);
      this.ResultBar.LayoutParameters = LayoutParams.ForLL(height: RESULT_BAR_HEIGHT);
      mainLayout.AddView(this.ResultBar);

      View mainView = CreateMainView();
      mainView.LayoutParameters = LayoutParams.ForLL();
      mainLayout.AddView(mainView);

      SetContentView(mainLayout);
    }

    private Drawable GetDrawable(string filename) {
      // As long as MonoDroid doesn't support Android resources in library projects we need to fall back to regular
      // .NET resources.
      Assembly assembly = Assembly.GetExecutingAssembly();
      Stream resStream = assembly.GetManifestResourceStream(ASSEMBLY_NAMESPACE + ".Resources.Drawable." + filename);
      return new BitmapDrawable(this.Resources, resStream);
    }

    protected abstract View CreateMainView();

    private int m_preferredListItemHeight = -1;

    public int GetPreferredListItemHeight() {
      if (this.m_preferredListItemHeight == -1) {
        this.m_preferredListItemHeight = GetPreferredListItemHeight(this);
      }

      return this.m_preferredListItemHeight;
    }

    public static int GetPreferredListItemHeight(Context ctx) {
      return Dimension.FromResId(Android.Resource.Attribute.ListPreferredItemHeight)
                      .ToPixels(ctx, Orientation.Vertical);
    }

    public Drawable GetIconForState(TestState state) {
      switch (state) {
        case TestState.NotYetRun:
        case TestState.Inconclusive:
          return this.IconOutcomeInconclusive;

        case TestState.Passed:
          return this.IconOutcomePassed;

        case TestState.Failed:
          return this.IconOutcomeError;

        default:
          throw new Exception("Unexpected");
      }
    }

    public static ISharedPreferences GetPreferences() {
      return PreferenceManager.GetDefaultSharedPreferences(Application.Context);
    }

    public class ResultBarView : View {
      private Paint m_paint = new Paint();

      public Color BarColor {
        get { return this.m_paint.Color; }
        set {
          this.m_paint.Color = value;
          Invalidate();
        }
      }

      public ResultBarView(Context context) : base(context) {
        this.BarColor = Color.Green;
      }

      public void SetColorByState(TestState state) {
        switch (state) {
          case TestState.Failed:
            this.BarColor = Color.Red;
            break;
          case TestState.Inconclusive:
            this.BarColor = Color.Yellow;
            break;
          case TestState.Passed:
            this.BarColor = Color.Green;
            break;
          default:
            this.BarColor = Color.Blue;
            break;
        }
      }

      protected override void OnDraw(Canvas canvas) {
        canvas.DrawRect(canvas.ClipBounds, this.m_paint);
      }
    }
  }
}