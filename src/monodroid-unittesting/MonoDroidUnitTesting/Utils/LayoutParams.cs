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
using Android.Views;
using Android.Widget;


namespace MonoDroidUnitTesting.Utils {
  internal static class LayoutParams {
    public const int MatchParent = ViewGroup.LayoutParams.MatchParent;
    public const int WrapContent = ViewGroup.LayoutParams.WrapContent;

    public static LinearLayout.LayoutParams ForLL(int width = MatchParent, int height = WrapContent,
                                                  float weight = 0, 
                                                  int margin = -1, 
                                                  int marginLeft = -1, int marginRight = -1,
                                                  int marginTop = -1, int marginBottom = -1,
                                                  GravityFlags gravity = GravityFlags.NoGravity) {
      var layoutParams = new LinearLayout.LayoutParams(width, height, weight);

      SetMargin(layoutParams, margin: margin, marginLeft: marginLeft, marginRight: marginRight, 
                marginTop: marginTop, marginBottom: marginBottom);

      if (gravity != GravityFlags.NoGravity) {
        layoutParams.Gravity = gravity;
      }

      return layoutParams;
    }

    private static void SetMargin(ViewGroup.MarginLayoutParams layoutParams,
                                  int margin,
                                  int marginLeft, int marginRight,
                                  int marginTop, int marginBottom) {
      if (marginLeft >= 0) {
        layoutParams.LeftMargin = marginLeft;
      }
      else if (margin >= 0) {
        layoutParams.LeftMargin = margin;
      }

      if (marginRight >= 0) {
        layoutParams.RightMargin = marginRight;
      }
      else if (margin >= 0) {
        layoutParams.RightMargin = margin;
      }

      if (marginTop >= 0) {
        layoutParams.TopMargin = marginTop;
      }
      else if (margin >= 0) {
        layoutParams.TopMargin = margin;
      }

      if (marginBottom >= 0) {
        layoutParams.BottomMargin = marginBottom;
      }
      else if (margin >= 0) {
        layoutParams.BottomMargin = margin;
      }
    }
  }
}