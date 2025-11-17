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
using Android.Runtime;
using Android.Util;
using Android.Widget;

namespace keepass2android
{
    public class MeasuringLinearLayout : LinearLayout
    {
        protected MeasuringLinearLayout(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        public MeasuringLinearLayout(Context context)
            : base(context)
        {
        }

        public MeasuringLinearLayout(Context context, IAttributeSet attrs)
            : base(context, attrs)
        {
        }

        public MeasuringLinearLayout(Context context, IAttributeSet attrs, int defStyleAttr)
            : base(context, attrs, defStyleAttr)
        {
        }

        public MeasuringLinearLayout(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes)
            : base(context, attrs, defStyleAttr, defStyleRes)
        {
        }


        public class MeasureArgs
        {
            public int ActualHeight;
            public int ProposedHeight;

        }

        public event EventHandler<MeasureArgs> MeasureEvent;

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            MeasureArgs args = new MeasureArgs();

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