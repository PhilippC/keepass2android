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
using Android.App;
using Android.Content;
using Android.Util;
using Android.Widget;


namespace Android.Views {
  internal struct Dimension {
    public DimensionUnit Unit { get; private set; }
    public float Value { get; private set; }

    public Dimension(float val, DimensionUnit unit) : this() {
      this.Value = val;
      this.Unit = unit;
    }

    public Dimension(TypedValue val) : this() {
      if (val.Type != DataType.Dimension) {
        throw new ArgumentException("Wrong type: " + val.Type);
      }

      ComplexUnitType unit = (ComplexUnitType)((val.Data >> (int)ComplexUnitType.Shift) & (int)ComplexUnitType.Mask);
      switch (unit) {
        case ComplexUnitType.Dip:
          this.Unit = DimensionUnit.Dp;
          break;
        case ComplexUnitType.Sp:
          this.Unit = DimensionUnit.Sp;
          break;
        case ComplexUnitType.Px:
          this.Unit = DimensionUnit.Px;
          break;
        case ComplexUnitType.In:
          this.Unit = DimensionUnit.In;
          break;
        case ComplexUnitType.Mm:
          this.Unit = DimensionUnit.Mm;
          break;
        case ComplexUnitType.Pt:
          this.Unit = DimensionUnit.Pt;
          break;

        default:
          throw new Exception("Unexpected: " + (int)unit);
      }

      this.Value = TypedValue.ComplexToFloat(val.Data);
    }

    private static Context ResolveCtx(Context ctx) {
      return (ctx != null) ? ctx : Application.Context;
    }

    public float ConvertToValue(DimensionUnit destUnit, Context ctx = null,
                                Orientation orientation = Orientation.Horizontal) {
      return ConvertToValue(destUnit, ResolveCtx(ctx).Resources.DisplayMetrics, orientation);
    }

    public float ConvertToValue(DimensionUnit destUnit, DisplayMetrics dm,
                                Orientation orientation = Orientation.Horizontal) {
      if (destUnit == this.Unit) {
        return this.Value;
      }

      float pixels = ToSubPixels(dm, orientation);

      if (destUnit == DimensionUnit.Px) {
        return pixels;
      }

      return FromSubPixels(pixels, destUnit, dm, orientation);
    }

    public Dimension ConvertToDimension(DimensionUnit destUnit, Context ctx = null,
                                        Orientation orientation = Orientation.Horizontal) {
      return ConvertToDimension(destUnit, ResolveCtx(ctx).Resources.DisplayMetrics, orientation);
    }

    public Dimension ConvertToDimension(DimensionUnit destUnit, DisplayMetrics dm,
                                        Orientation orientation = Orientation.Horizontal) {
      if (destUnit == this.Unit) {
        return this;
      }

      return new Dimension(ConvertToValue(destUnit, dm, orientation), destUnit);
    }

    /// <summary>
    /// Returns the scale factor for converting from the source unit to pixels (when multiplying)
    /// </summary>
    private static float GetScaleFor(DisplayMetrics dm, DimensionUnit srcUnit, Orientation orientation) {
      switch (srcUnit) {
        case DimensionUnit.Dp:
          return dm.Density;

        case DimensionUnit.Sp:
          return dm.ScaledDensity;

        case DimensionUnit.Px:
          return 1.0f;

        case DimensionUnit.In:
          if (orientation == Orientation.Horizontal) {
            return dm.Xdpi;
          }
          else {
            return dm.Ydpi;
          }

        case DimensionUnit.Mm:
          if (orientation == Orientation.Horizontal) {
            return dm.Xdpi * (1.0f / 25.4f);
          }
          else {
            return dm.Ydpi * (1.0f / 25.4f);
          }

        case DimensionUnit.Pt:
          if (orientation == Orientation.Horizontal) {
            return dm.Xdpi * (1.0f / 72f);
          }
          else {
            return dm.Ydpi * (1.0f / 72f);
          }

        default:
          throw new Exception("Unexpected: " + srcUnit);
      }
    }

    private float ToSubPixels(DisplayMetrics dm, Orientation destOrientation) {
      if (this.Unit == DimensionUnit.Px) {
        return this.Value;
      }

      float scale = GetScaleFor(dm, this.Unit, destOrientation);

      return this.Value * scale;
    }

    public int ToPixels(Context ctx = null, Orientation destOrientation = Orientation.Horizontal) {
      return ToPixels(ResolveCtx(ctx).Resources.DisplayMetrics, destOrientation);
    }

    public int ToPixels(DisplayMetrics dm, Orientation destOrientation = Orientation.Horizontal) {
      if (this.Unit == DimensionUnit.Px) {
        return (int)this.Value;
      }

      return (int)(ToSubPixels(dm, destOrientation) + 0.5f);
    }

    private static float FromSubPixels(float pixels, DimensionUnit destUnit, DisplayMetrics dm,
                                       Orientation srcOrientation) {
      if (destUnit == DimensionUnit.Px) {
        return pixels;
      }

      float scale = GetScaleFor(dm, destUnit, srcOrientation);

      return (pixels / scale);
    }

    public static Dimension FromPixels(int pixels, DimensionUnit destUnit, Context ctx = null,
                                       Orientation srcOrientation = Orientation.Horizontal) {
      return FromPixels(pixels, destUnit, ResolveCtx(ctx).Resources.DisplayMetrics, srcOrientation);
    }

    public static Dimension FromPixels(int pixels, DimensionUnit destUnit, DisplayMetrics dm,
                                       Orientation srcOrientation = Orientation.Horizontal) {
      if (destUnit == DimensionUnit.Px) {
        return new Dimension(pixels, DimensionUnit.Px);
      }

      return new Dimension(FromSubPixels(pixels, destUnit, dm, srcOrientation), destUnit);
    }

    public static Dimension FromResId(int resId, Context ctx = null) {
      TypedValue val = new TypedValue();

      if (!ResolveCtx(ctx).Theme.ResolveAttribute(resId, val, true)) {
        throw new ArgumentException("Could not resolve resource ID #" + resId);
      }

      return new Dimension(val);
    }
  }

  internal enum DimensionUnit {
    /// <summary>
    /// Density-independent Pixels - An abstract unit that is based on the physical density of the screen. These units 
    /// are relative to a 160 dpi (dots per inch) screen, on which 1dp is roughly equal to 1px. When running on a higher
    /// density screen, the number of pixels used to draw 1dp is scaled up by a factor appropriate for the screen's dpi.
    /// Likewise, when on a lower density screen, the number of pixels used for 1dp is scaled down. The ratio of 
    /// dp-to-pixel will change with the screen density, but not necessarily in direct proportion. Using dp units 
    /// (instead of px units) is a simple solution to making the view dimensions in your layout resize properly for 
    /// different screen densities. In other words, it provides consistency for the real-world sizes of your UI 
    /// elements across different devices.
    /// </summary>
    Dp,
    /// <summary>
    /// Scale-independent Pixels - This is like the dp unit, but it is also scaled by the user's font size preference. 
    /// It is recommend you use this unit when specifying font sizes, so they will be adjusted for both the screen 
    /// density and the user's preference.
    /// </summary>
    Sp,
    /// <summary>
    /// Points - 1/72 of an inch based on the physical size of the screen.
    /// </summary>
    Pt,
    /// <summary>
    /// Pixels - Corresponds to actual pixels on the screen. This unit of measure is not recommended because the actual
    /// representation can vary across devices; each devices may have a different number of pixels per inch and may 
    /// have more or fewer total pixels available on the screen.
    /// </summary>
    Px,
    /// <summary>
    /// Millimeters - Based on the physical size of the screen.
    /// </summary>
    Mm,
    /// <summary>
    /// Inches - Based on the physical size of the screen.
    /// </summary>
    In
  }
}