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
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Method;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MonoDroidUnitTesting.Utils;


namespace MonoDroidUnitTesting {
  [Activity]
  public class TestMethodResultActivity : AbstractResultActivity {
    private const string INTENT_CLASS_PARAM = "MonoDroidUnitTest.TestClass";
    private const string INTENT_METHOD_PARAM = "MonoDroidUnitTest.TestMethod";

    private MethodOutcome m_testMethod;

    private ImageView m_outcomeIcon;
    private TextView m_headerText;

    private TextView m_resultText;

    private LinearLayout m_stackTraceInfo;
    private TextView m_stackTraceText;

    public TestMethodResultActivity() : base("method_") { }

    protected override View CreateMainView() {
      LinearLayout mainLayout = new LinearLayout(this);
      mainLayout.Orientation = Orientation.Vertical;

      CreateHeaderSection(mainLayout);
      CreateResultSection(mainLayout);
      CreateStackTraceSection(mainLayout);

      return mainLayout;
    }

    private void CreateHeaderSection(LinearLayout mainLayout) {
      LinearLayout headerLayout = new LinearLayout(this);
      headerLayout.Orientation = Orientation.Horizontal;
      headerLayout.LayoutParameters = LayoutParams.ForLL(marginBottom: 24);

      this.m_outcomeIcon = new ImageView(this);
      this.m_outcomeIcon.SetAdjustViewBounds(true);
      this.m_outcomeIcon.LayoutParameters = LayoutParams.ForLL(width: LayoutParams.WrapContent, margin: 7,
                                                               gravity: GravityFlags.Top|GravityFlags.CenterHorizontal);
      this.m_outcomeIcon.SetMinimumHeight(GetPreferredListItemHeight() / 2);
      this.m_outcomeIcon.SetMaxHeight(GetPreferredListItemHeight() / 2);
      headerLayout.AddView(this.m_outcomeIcon);

      this.m_headerText = new TextView(this);
      this.m_headerText.LayoutParameters = LayoutParams.ForLL();
      this.m_headerText.SetTextSize(ComplexUnitType.Px, GetPreferredListItemHeight() / 4);
      headerLayout.AddView(this.m_headerText);

      mainLayout.AddView(headerLayout);
    }
    
    private void CreateResultSection(LinearLayout mainLayout) {
      this.m_resultText = new TextView(this);
      this.m_resultText.LayoutParameters = LayoutParams.ForLL(margin: 7, marginBottom: 24);
      this.m_resultText.SetTextSize(ComplexUnitType.Px, GetPreferredListItemHeight() / 4);
      this.m_resultText.SetPadding(5, 2, 5, 2);
      this.m_resultText.SetBackgroundColor(new Color(35, 35, 35));
      mainLayout.AddView(this.m_resultText);
    }

    private void CreateStackTraceSection(LinearLayout mainLayout) {
      this.m_stackTraceInfo = new LinearLayout(this);
      this.m_stackTraceInfo.Orientation = Orientation.Vertical;
      this.m_stackTraceInfo.LayoutParameters = LayoutParams.ForLL(weight: 1);

      Button logButton = new Button(this);
      logButton.Text = "Dump exception to logcat";
      logButton.Click += (s, e) => DumpStackTrace();
      logButton.LayoutParameters = LayoutParams.ForLL();
      this.m_stackTraceInfo.AddView(logButton);

      if (!AreLineNumbersAvailable()) {
        TextView noteText = new TextView(this);
        noteText.LayoutParameters = LayoutParams.ForLL(margin: 7, marginTop: 0);
        noteText.TextFormatted = Html.FromHtml("<b>Line numbers are unavailable because no debugger is attached.");
        noteText.SetPadding(5, 2, 5, 2);
        noteText.Gravity = GravityFlags.Center;
        noteText.SetBackgroundColor(new Color(110, 3, 15));
        this.m_stackTraceInfo.AddView(noteText);
      }

      this.m_stackTraceText = new TextView(this);
      this.m_stackTraceText.Typeface = Typeface.Monospace;
      this.m_stackTraceText.SetTextSize(ComplexUnitType.Px, GetPreferredListItemHeight() / 6);
      this.m_stackTraceText.LayoutParameters = LayoutParams.ForLL(weight: 1);
      this.m_stackTraceText.VerticalScrollBarEnabled = true;
      this.m_stackTraceText.MovementMethod = new ScrollingMovementMethod();
      // Necessary so that the text color doesn't get darkened when scrolling the text view
      this.m_stackTraceText.SetTextColor(Color.White);
      this.m_stackTraceInfo.AddView(this.m_stackTraceText);

      mainLayout.AddView(this.m_stackTraceInfo);
    }

    private static bool AreLineNumbersAvailable() {
      try {
        throw new Exception();
      }
      catch (Exception e) {
        StackTrace st = new StackTrace(e, true);
        return (st.GetFrame(0).GetFileName() != null);
      }
    }

    private void DumpStackTrace() {
      // NOTE: Don't dump "this.m_stackTraceText.Text" as this text has specifically modified to be displayed.
      Log.Info(LOG_TAG, this.m_testMethod.OutcomeError.ToString());

      Toast toast = Toast.MakeText(this, "Exception dumped under tag '" + LOG_TAG + "'", ToastLength.Long);
      toast.Show();
    }

    private static MethodOutcome s_testMethodParam = null;

    internal static void StartActivity(Context ctx, MethodOutcome testMethod) {
      s_testMethodParam = testMethod;

      Intent i = new Intent(ctx, typeof(TestMethodResultActivity));
      //i.PutExtra(INTENT_CLASS_PARAM, testMethod.Class.Class.AssemblyQualifiedName);
      //i.PutExtra(INTENT_METHOD_PARAM, testMethod.Method.Name);
      ctx.StartActivity(i);
    }

    protected override void OnStart() {
      base.OnStart();

      /*string testClassName = this.Intent.GetCharSequenceExtra(INTENT_CLASS_PARAM);
      TestClass testClass = GuiTestRunnerActivity.TestRunner.GetTestClass(testClassName);

      string testMethodName = this.Intent.GetCharSequenceExtra(INTENT_METHOD_PARAM);
      TestMethod testMethod = testClass.GetTestMethod(testMethodName);*/

      if (s_testMethodParam != null) {
        FillActivity(s_testMethodParam);
      }
    }

    private void FillActivity(MethodOutcome testMethod) {
      this.m_testMethod = testMethod;

      this.Title = "Result for " + this.m_testMethod.Method.Name;
      this.ResultBar.SetColorByState(this.m_testMethod.State);

      FillHeaderSection();
      FillResultSection();
      FillStackTraceSection();
    }

    private void FillHeaderSection() {
      this.m_outcomeIcon.SetImageDrawable(GetIconForState(this.m_testMethod.State));

      string headerText = "<b>" + this.m_testMethod.Method.Name + "()</b><br>"
                        + "<small>Class: " + this.m_testMethod.Method.DeclaringType.Name + "<br>"
                        + "Namespace: " + this.m_testMethod.Method.DeclaringType.Namespace + "</small>";
      this.m_headerText.TextFormatted = Html.FromHtml(headerText);
    }

    private void FillResultSection() {
      StringBuilder resultText = new StringBuilder();
      
      resultText.Append("<b>Outcome: ");
      switch (this.m_testMethod.State) {
        case TestState.NotYetRun:
          resultText.Append("Skipped");
          break;

        case TestState.Passed:
          resultText.Append("<font color=green>passed</font>");
          break;

        case TestState.Failed:
          resultText.Append("<font color=red>failed</font>");
          break;

        case TestState.Inconclusive:
          resultText.Append("<font color=red>inconclusive</font>");
          break;

        default:
          resultText.Append(this.m_testMethod.State.ToString());
          break;
      }

      if (this.m_testMethod.OutcomeError != null) {
        resultText.Append("</b><br><small>").Append(this.m_testMethod.OutcomeError.Message);
      }

      this.m_resultText.TextFormatted = Html.FromHtml(resultText.ToString());
    }

    private void FillStackTraceSection() {
      if (this.m_testMethod.OutcomeError == null) {
        this.m_stackTraceInfo.Visibility = ViewStates.Gone;
      }
      else {
        this.m_stackTraceText.Text = CreateExceptionString(this.m_testMethod.OutcomeError, this.m_stackTraceText);
        this.m_stackTraceInfo.Visibility = ViewStates.Visible;
      }
    }

    private static string CreateExceptionString(TestError e, TextView tv) {
      List<string> lines = new List<string>();
      FormatException(e, lines, false);
      int maxChar = FindCharacterLineCount(tv);

      StringBuilder sb = new StringBuilder(512);

      foreach (string line in lines) {
        if (line == "") {
          sb.AppendLine();
        }
        else {
          sb.Append(WordWrap(line, maxChar));
        }
      }

      return sb.ToString();
    }

    private static List<StackFrame> FilterStackTrace(Exception e) {
      StackTrace st = new StackTrace(e, true);
      List<StackFrame> stackFrames = new List<StackFrame>(st.GetFrames());

      // Last stack frame is always "MonoMethod.Invoke()". Remove it.
      stackFrames.RemoveAt(stackFrames.Count - 1);
      
      // Search for first unit testing api frame
      int index = -1;
      foreach (StackFrame sf in stackFrames) {
        MethodBase method = sf.GetMethod();
        if (method.DeclaringType.Namespace != typeof(Assert).Namespace) {
          break;
        }

        index++;
      }

      if (index != -1) {
        stackFrames.RemoveRange(0, index);
      }

      return stackFrames;
    }

    // Make special string to be displayed on an Android display
    private static void FormatException(TestError e, List<string> lines, bool isInnerException) {
      lines.Add(e.Exception.GetType().Name + ":");
      // We don't add the message here as it has already been written above the stack trace.

      int index = 0;
      IEnumerable<StackFrame> frames;
      if (isInnerException) {
        StackTrace st = new StackTrace(e.Exception, true);
        frames = st.GetFrames();
      }
      else {
        frames = FilterStackTrace(e.Exception);
      }

      foreach (StackFrame sf in frames) {
        MethodBase method = sf.GetMethod();
        bool isTestMethod = (method.GetCustomAttributes(typeof(TestMethodAttribute), true).Length != 0);
        lines.Add(string.Format("  at {0}.{1}({2})", method.DeclaringType.Name, method.Name,
                                                     (method.GetParameters().Length == 0 ? "" : "..."))
                 );
        if (sf.GetFileName() != null) {
          // line numbers and file names are available (only when debugger is attached)
          string filename = sf.GetFileName();
          filename = filename.Substring(filename.LastIndexOfAny(new char[] { '\\', '/' }) + 1);
          lines.Add(string.Format("    in {0}:{1}", filename, sf.GetFileLineNumber()));
        }

        index++;
      }

      if (e.Exception.InnerException != null) {
        lines.Add("");
        lines.Add("------- inner exception ---------");
        lines.Add("");
        FormatException(new TestError(e.Exception.InnerException), lines, true);
      }
    }

    private static int FindCharacterLineCount(TextView tv) {
      int viewWidth = tv.Context.Resources.DisplayMetrics.WidthPixels;
      float charWidth = tv.Paint.MeasureText("M");

      // -1 to be on the safe side
      return (int)(viewWidth / charWidth - 1);
    }

    // The word wrapping methods are from:
    // http://www.codeproject.com/Articles/51488/Implementing-Word-Wrap-in-C
    /// <summary>
    /// Word wraps the given text to fit within the specified width.
    /// </summary>
    /// <param name="text">Text to be word wrapped</param>
    /// <param name="width">Width, in characters, to which the text
    /// should be word wrapped</param>
    /// <returns>The modified text</returns>
    private static string WordWrap(string text, int width) {
      int pos, next;
      StringBuilder sb = new StringBuilder();

      // Lucidity check
      if (width < 1)
        return text;

      // Parse each line of text
      for (pos = 0; pos < text.Length; pos = next) {
        // Find end of line
        int eol = text.IndexOf('\n', pos);
        if (eol == -1)
          next = eol = text.Length;
        else
          next = eol + 1;

        // Copy this line of text, breaking into smaller lines as needed
        if (eol > pos) {
          int indent = 0;
          while (pos + indent < eol && Char.IsWhiteSpace(text[pos + indent])) {
            indent++;
          }
          indent += 2;

          bool isFirstLine = true;
          do {
            int len = eol - pos;
            if (isFirstLine) {
              if (len > width) {
                len = BreakLine(text, pos, width);
              }
            }
            else {
              if (len > width - indent) {
                len = BreakLine(text, pos, width - indent);
              }
            }


            if (!isFirstLine) {
              sb.Append(' ', indent);
            }
            sb.Append(text, pos, len);
            sb.Append("\n");

            // Trim whitespace following break
            pos += len;
            while (pos < eol && text[pos] == ' ') {
              pos++;
            }

            isFirstLine = false;
          }
          while (eol > pos);
        }
        else {
          // Empty line
          sb.Append('\n');
        }
      }

      return sb.ToString();
    }

    /// <summary>
    /// Locates position to break the given line so as to avoid
    /// breaking words.
    /// </summary>
    /// <param name="text">String that contains line of text</param>
    /// <param name="pos">Index where line of text starts</param>
    /// <param name="max">Maximum line length</param>
    /// <returns>The modified line length</returns>
    private static int BreakLine(string text, int pos, int max) {
      // Find last whitespace in line
      int i = max;
      while (i >= 0) {
        switch (text[pos + i]) {
          case ' ':
          case '.':
          case '(':
            goto After;
        }
        i--;
      }
    After:

      // If no whitespace found, break at maximum length
      if (i < 0)
        return max;

      // Find start of whitespace
      while (i >= 0 && text[pos + i] == ' ') {
        i--;
      }

      // Return length of text before whitespace
      return i + 1;
    }

    protected override void OnResume() {
      base.OnResume();

      ISharedPreferencesEditor e = GetPreferences().Edit();
      e.PutString(ACTIVITY_PREFS_NAME, typeof(TestMethodResultActivity).Name);

      TestMethod testMethod = this.m_testMethod as TestMethod;
      if (testMethod != null) {
        // Only for actual test methods. No way for just test outcomes.
        e.PutString(INTENT_CLASS_PARAM, testMethod.Class.Class.AssemblyQualifiedName);
        e.PutString(INTENT_METHOD_PARAM, testMethod.Method.Name);
      }

      e.Commit();
    }

    internal static bool RestoreActivity(Context ctx) {
      var prefs = GetPreferences();
      if (prefs.GetString(ACTIVITY_PREFS_NAME, "") != typeof(TestMethodResultActivity).Name) {
        return false;
      }

      string className = prefs.GetString(INTENT_CLASS_PARAM, "");
      TestClass testClass = GuiTestRunnerActivity.TestRunner.GetTestClass(className);
      if (testClass == null) {
        // Class is not longer under test or not set
        return true;
      }

      string methodName = prefs.GetString(INTENT_METHOD_PARAM, "");
      TestMethod testMethod = testClass.GetTestMethod(methodName);
      if (testMethod == null) {
        // Test class no longer contains this test method
        return true;
      }

      StartActivity(ctx, testMethod);
      return true;
    }
  }
}