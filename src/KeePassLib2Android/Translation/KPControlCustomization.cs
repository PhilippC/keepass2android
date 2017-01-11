/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2017 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml.Serialization;

#if !KeePassUAP
using System.Drawing;
using System.Windows.Forms;
#endif

using KeePassLib.Cryptography;
using KeePassLib.Utility;

namespace KeePassLib.Translation
{
	public sealed class KpccLayout
	{
		public enum LayoutParameterEx
		{
			X, Y, Width, Height
		}

		private const string m_strControlRelative = @"%c";

		internal const NumberStyles m_nsParser = (NumberStyles.AllowLeadingSign |
			NumberStyles.AllowDecimalPoint);
		internal static readonly CultureInfo m_lclInv = CultureInfo.InvariantCulture;

		private string m_strPosX = string.Empty;
		[XmlAttribute]
		[DefaultValue("")]
		public string X
		{
			get { return m_strPosX; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strPosX = value;
			}
		}

		private string m_strPosY = string.Empty;
		[XmlAttribute]
		[DefaultValue("")]
		public string Y
		{
			get { return m_strPosY; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strPosY = value;
			}
		}

		private string m_strSizeW = string.Empty;
		[XmlAttribute]
		[DefaultValue("")]
		public string Width
		{
			get { return m_strSizeW; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strSizeW = value;
			}
		}

		private string m_strSizeH = string.Empty;
		[XmlAttribute]
		[DefaultValue("")]
		public string Height
		{
			get { return m_strSizeH; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strSizeH = value;
			}
		}

		public void SetControlRelativeValue(LayoutParameterEx lp, string strValue)
		{
			Debug.Assert(strValue != null);
			if(strValue == null) throw new ArgumentNullException("strValue");

			if(strValue.Length > 0) strValue += m_strControlRelative;

			if(lp == LayoutParameterEx.X) m_strPosX = strValue;
			else if(lp == LayoutParameterEx.Y) m_strPosY = strValue;
			else if(lp == LayoutParameterEx.Width) m_strSizeW = strValue;
			else if(lp == LayoutParameterEx.Height) m_strSizeH = strValue;
			else { Debug.Assert(false); }
		}

#if (!KeePassLibSD && !KeePassUAP)
		internal void ApplyTo(Control c)
		{
			Debug.Assert(c != null); if(c == null) return;

			int? v;
			v = GetModControlParameter(c, LayoutParameterEx.X, m_strPosX);
			if(v.HasValue) c.Left = v.Value;
			v = GetModControlParameter(c, LayoutParameterEx.Y, m_strPosY);
			if(v.HasValue) c.Top = v.Value;
			v = GetModControlParameter(c, LayoutParameterEx.Width, m_strSizeW);
			if(v.HasValue) c.Width = v.Value;
			v = GetModControlParameter(c, LayoutParameterEx.Height, m_strSizeH);
			if(v.HasValue) c.Height = v.Value;
		}

		private static int? GetModControlParameter(Control c, LayoutParameterEx p,
			string strModParam)
		{
			if(strModParam.Length == 0) return null;

			Debug.Assert(c.Left == c.Location.X);
			Debug.Assert(c.Top == c.Location.Y);
			Debug.Assert(c.Width == c.Size.Width);
			Debug.Assert(c.Height == c.Size.Height);

			int iPrev;
			if(p == LayoutParameterEx.X) iPrev = c.Left;
			else if(p == LayoutParameterEx.Y) iPrev = c.Top;
			else if(p == LayoutParameterEx.Width) iPrev = c.Width;
			else if(p == LayoutParameterEx.Height) iPrev = c.Height;
			else { Debug.Assert(false); return null; }

			double? dRel = ToControlRelativePercent(strModParam);
			if(dRel.HasValue)
				return (iPrev + (int)((dRel.Value * (double)iPrev) / 100.0));
			
			Debug.Assert(false);
			return null;
		}

		public static double? ToControlRelativePercent(string strEncoded)
		{
			Debug.Assert(strEncoded != null);
			if(strEncoded == null) throw new ArgumentNullException("strEncoded");

			if(strEncoded.Length == 0) return null;

			if(strEncoded.EndsWith(m_strControlRelative))
			{
				string strValue = strEncoded.Substring(0, strEncoded.Length -
					m_strControlRelative.Length);
				if((strValue.Length == 1) && (strValue == "-"))
					strValue = "0";

				double dRel;
				if(double.TryParse(strValue, m_nsParser, m_lclInv, out dRel))
				{
					return dRel;
				}
				else
				{
					Debug.Assert(false);
					return null;
				}
			}
			
			Debug.Assert(false);
			return null;
		}
#endif

		public static string ToControlRelativeString(string strEncoded)
		{
			Debug.Assert(strEncoded != null);
			if(strEncoded == null) throw new ArgumentNullException("strEncoded");

			if(strEncoded.Length == 0) return string.Empty;

			if(strEncoded.EndsWith(m_strControlRelative))
				return strEncoded.Substring(0, strEncoded.Length -
					m_strControlRelative.Length);

			Debug.Assert(false);
			return string.Empty;
		}
	}

	public sealed class KPControlCustomization : IComparable<KPControlCustomization>
	{
		private string m_strMemberName = string.Empty;
		/// <summary>
		/// Member variable name of the control to be translated.
		/// </summary>
		[XmlAttribute]
		public string Name
		{
			get { return m_strMemberName; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strMemberName = value;
			}
		}

		private string m_strHash = string.Empty;
		[XmlAttribute]
		public string BaseHash
		{
			get { return m_strHash; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strHash = value;
			}
		}

		private string m_strText = string.Empty;
		[DefaultValue("")]
		public string Text
		{
			get { return m_strText; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strText = value;
			}
		}

		private string m_strEngText = string.Empty;
		[XmlIgnore]
		public string TextEnglish
		{
			get { return m_strEngText; }
			set { m_strEngText = value; }
		}

		private KpccLayout m_layout = new KpccLayout();
		public KpccLayout Layout
		{
			get { return m_layout; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_layout = value;
			}
		}

		public int CompareTo(KPControlCustomization kpOther)
		{
			if(kpOther == null) { Debug.Assert(false); return 1; }

			return m_strMemberName.CompareTo(kpOther.Name);
		}

#if (!KeePassLibSD && !KeePassUAP)
		private static readonly Type[] m_vTextControls = new Type[] {
			typeof(MenuStrip), typeof(PictureBox), typeof(ListView),
			typeof(TreeView), typeof(ToolStrip), typeof(WebBrowser),
			typeof(Panel), typeof(StatusStrip), typeof(ProgressBar),
			typeof(NumericUpDown), typeof(TabControl)
		};

		public static bool ControlSupportsText(object oControl)
		{
			if(oControl == null) return false;

			Type t = oControl.GetType();
			for(int i = 0; i < m_vTextControls.Length; ++i)
			{
				if(t == m_vTextControls[i]) return false;
			}

			return true;
		}

		// Name-unchecked (!) property application method
		internal void ApplyTo(Control c)
		{
			if((m_strText.Length > 0) && ControlSupportsText(c) &&
				(c.Text.Length > 0))
			{
				c.Text = m_strText;
			}

			m_layout.ApplyTo(c);
		}

		public static string HashControl(Control c)
		{
			if(c == null) { Debug.Assert(false); return string.Empty; }

			StringBuilder sb = new StringBuilder();
			WriteCpiParam(sb, c.Text);

			if(c is Form)
			{
				WriteCpiParam(sb, c.ClientSize.Width.ToString(KpccLayout.m_lclInv));
				WriteCpiParam(sb, c.ClientSize.Height.ToString(KpccLayout.m_lclInv));
			}
			else // Normal control
			{
				WriteCpiParam(sb, c.Left.ToString(KpccLayout.m_lclInv));
				WriteCpiParam(sb, c.Top.ToString(KpccLayout.m_lclInv));
				WriteCpiParam(sb, c.Width.ToString(KpccLayout.m_lclInv));
				WriteCpiParam(sb, c.Height.ToString(KpccLayout.m_lclInv));
				WriteCpiParam(sb, c.Dock.ToString());
			}

			WriteCpiParam(sb, c.Font.Name);
			WriteCpiParam(sb, c.Font.SizeInPoints.ToString(KpccLayout.m_lclInv));
			WriteCpiParam(sb, c.Font.Bold ? "B" : "N");
			WriteCpiParam(sb, c.Font.Italic ? "I" : "N");
			WriteCpiParam(sb, c.Font.Underline ? "U" : "N");
			WriteCpiParam(sb, c.Font.Strikeout ? "S" : "N");

			WriteControlDependentParams(sb, c);

			byte[] pb = StrUtil.Utf8.GetBytes(sb.ToString());
			byte[] pbSha = CryptoUtil.HashSha256(pb);

			// See also MatchHash
			return "v1:" + Convert.ToBase64String(pbSha, 0, 3,
				Base64FormattingOptions.None);
		}

		private static void WriteControlDependentParams(StringBuilder sb, Control c)
		{
			CheckBox cb = (c as CheckBox);
			RadioButton rb = (c as RadioButton);
			Button btn = (c as Button);
			Label l = (c as Label);
			LinkLabel ll = (c as LinkLabel);

			if(cb != null)
			{
				WriteCpiParam(sb, cb.AutoSize ? "A" : "F");
				WriteCpiParam(sb, cb.TextAlign.ToString());
				WriteCpiParam(sb, cb.TextImageRelation.ToString());
				WriteCpiParam(sb, cb.Appearance.ToString());
				WriteCpiParam(sb, cb.CheckAlign.ToString());
			}
			else if(rb != null)
			{
				WriteCpiParam(sb, rb.AutoSize ? "A" : "F");
				WriteCpiParam(sb, rb.TextAlign.ToString());
				WriteCpiParam(sb, rb.TextImageRelation.ToString());
				WriteCpiParam(sb, rb.Appearance.ToString());
				WriteCpiParam(sb, rb.CheckAlign.ToString());
			}
			else if(btn != null)
			{
				WriteCpiParam(sb, btn.AutoSize ? "A" : "F");
				WriteCpiParam(sb, btn.TextAlign.ToString());
				WriteCpiParam(sb, btn.TextImageRelation.ToString());
			}
			else if(l != null)
			{
				WriteCpiParam(sb, l.AutoSize ? "A" : "F");
				WriteCpiParam(sb, l.TextAlign.ToString());
			}
			else if(ll != null)
			{
				WriteCpiParam(sb, ll.AutoSize ? "A" : "F");
				WriteCpiParam(sb, ll.TextAlign.ToString());
			}
		}

		private static void WriteCpiParam(StringBuilder sb, string strProp)
		{
			sb.Append('/');
			sb.Append(strProp);
		}

		public bool MatchHash(string strHash)
		{
			if(strHash == null) throw new ArgumentNullException("strHash");

			// Currently only v1: is supported, see HashControl
			return (m_strHash == strHash);
		}
#endif
	}
}
