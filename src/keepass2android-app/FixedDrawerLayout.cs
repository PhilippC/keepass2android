// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
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
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using AndroidX.DrawerLayout.Widget;

namespace keepass2android
{
  public class FixedDrawerLayout : DrawerLayout
  {
    private bool _fitsSystemWindows;

    protected FixedDrawerLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public FixedDrawerLayout(Context context, IAttributeSet attrs, int defStyle)
        : base(context, attrs, defStyle)
    {
    }

    public FixedDrawerLayout(Context context, IAttributeSet attrs)
        : base(context, attrs)
    {
    }

    public FixedDrawerLayout(Context context)
        : base(context)
    {
    }

    private int[] mInsets = new int[4];

    protected override bool FitSystemWindows(Rect insets)
    {
      if (Util.IsKitKatOrLater)
      {
        // Intentionally do not modify the bottom inset. For some reason, 
        // if the bottom inset is modified, window resizing stops working.
        // TODO: Figure out why.

        mInsets[0] = insets.Left;
        mInsets[1] = insets.Top;
        mInsets[2] = insets.Right;

        insets.Left = 0;
        insets.Top = 0;
        insets.Right = 0;
      }

      return base.FitSystemWindows(insets);

    }
    public int[] GetInsets()
    {
      return mInsets;
    }

    public struct MeasureArgs
    {
      public int ActualHeight;
      public int ProposedHeight;

    }

    public event EventHandler<MeasureArgs> MeasureEvent;

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
      MeasureArgs args;

      args.ProposedHeight = MeasureSpec.GetSize(heightMeasureSpec);
      args.ActualHeight = Height;


      OnMeasureEvent(args);
      base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }

    protected virtual void OnMeasureEvent(MeasureArgs args)
    {
      var handler = MeasureEvent;
      if (handler != null) handler(this, args);
    }
  }
}