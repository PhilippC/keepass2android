using System;
using AndroidX.Activity;

namespace keepass2android
{
  internal static class PredictiveBack
  {
    public static void Register(ComponentActivity activity, Action onBackPressed)
    {
      if (!OperatingSystem.IsAndroidVersionAtLeast(36))
        return;

      activity.OnBackPressedDispatcher.AddCallback(activity, new ActionOnBackPressedCallback(onBackPressed));
    }

    private sealed class ActionOnBackPressedCallback : OnBackPressedCallback
    {
      private readonly Action _onBackPressed;

      public ActionOnBackPressedCallback(Action onBackPressed) : base(true)
      {
        _onBackPressed = onBackPressed;
      }

      public override void HandleOnBackPressed()
      {
        _onBackPressed();
      }
    }
  }
}