/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2016 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Diagnostics;

using KeePassLib.Interfaces;

namespace KeePassLib.Collections
{
	[Flags]
	public enum AutoTypeObfuscationOptions
	{
		None = 0,
		UseClipboard = 1
	}

	public sealed class AutoTypeAssociation : IEquatable<AutoTypeAssociation>,
		IDeepCloneable<AutoTypeAssociation>
	{
		private string m_strWindow = string.Empty;
		public string WindowName
		{
			get { return m_strWindow; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_strWindow = value;
			}
		}

		private string m_strSequence = string.Empty;
		public string Sequence
		{
			get { return m_strSequence; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_strSequence = value;
			}
		}

		public AutoTypeAssociation() { }

		public AutoTypeAssociation(string strWindow, string strSeq)
		{
			if(strWindow == null) throw new ArgumentNullException("strWindow");
			if(strSeq == null) throw new ArgumentNullException("strSeq");

			m_strWindow = strWindow;
			m_strSequence = strSeq;
		}

		public bool Equals(AutoTypeAssociation other)
		{
			if(other == null) return false;

			if(m_strWindow != other.m_strWindow) return false;
			if(m_strSequence != other.m_strSequence) return false;

			return true;
		}

		public AutoTypeAssociation CloneDeep()
		{
			return (AutoTypeAssociation)this.MemberwiseClone();
		}
	}

	/// <summary>
	/// A list of auto-type associations.
	/// </summary>
	public sealed class AutoTypeConfig : IEquatable<AutoTypeConfig>,
		IDeepCloneable<AutoTypeConfig>
	{
		private bool m_bEnabled = true;
		private AutoTypeObfuscationOptions m_atooObfuscation =
			AutoTypeObfuscationOptions.None;
		private string m_strDefaultSequence = string.Empty;
		private List<AutoTypeAssociation> m_lWindowAssocs =
			new List<AutoTypeAssociation>();

		/// <summary>
		/// Specify whether auto-type is enabled or not.
		/// </summary>
		public bool Enabled
		{
			get { return m_bEnabled; }
			set { m_bEnabled = value; }
		}

		/// <summary>
		/// Specify whether the typing should be obfuscated.
		/// </summary>
		public AutoTypeObfuscationOptions ObfuscationOptions
		{
			get { return m_atooObfuscation; }
			set { m_atooObfuscation = value; }
		}

		/// <summary>
		/// The default keystroke sequence that is auto-typed if
		/// no matching window is found in the <c>Associations</c>
		/// container.
		/// </summary>
		public string DefaultSequence
		{
			get { return m_strDefaultSequence; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_strDefaultSequence = value;
			}
		}

		/// <summary>
		/// Get all auto-type window/keystroke sequence pairs.
		/// </summary>
		public IEnumerable<AutoTypeAssociation> Associations
		{
			get { return m_lWindowAssocs; }
		}

		public int AssociationsCount
		{
			get { return m_lWindowAssocs.Count; }
		}

		/// <summary>
		/// Construct a new auto-type associations list.
		/// </summary>
		public AutoTypeConfig()
		{
		}

		/// <summary>
		/// Remove all associations.
		/// </summary>
		public void Clear()
		{
			m_lWindowAssocs.Clear();
		}

		/// <summary>
		/// Clone the auto-type associations list.
		/// </summary>
		/// <returns>New, cloned object.</returns>
		public AutoTypeConfig CloneDeep()
		{
			AutoTypeConfig newCfg = new AutoTypeConfig();

			newCfg.m_bEnabled = m_bEnabled;
			newCfg.m_atooObfuscation = m_atooObfuscation;
			newCfg.m_strDefaultSequence = m_strDefaultSequence;

			foreach(AutoTypeAssociation a in m_lWindowAssocs)
				newCfg.Add(a.CloneDeep());

			return newCfg;
		}

		public bool Equals(AutoTypeConfig other)
		{
			if(other == null) { Debug.Assert(false); return false; }

			if(m_bEnabled != other.m_bEnabled) return false;
			if(m_atooObfuscation != other.m_atooObfuscation) return false;
			if(m_strDefaultSequence != other.m_strDefaultSequence) return false;

			if(m_lWindowAssocs.Count != other.m_lWindowAssocs.Count) return false;
			for(int i = 0; i < m_lWindowAssocs.Count; ++i)
			{
				if(!m_lWindowAssocs[i].Equals(other.m_lWindowAssocs[i]))
					return false;
			}

			return true;
		}

		public AutoTypeAssociation GetAt(int iIndex)
		{
			if((iIndex < 0) || (iIndex >= m_lWindowAssocs.Count))
				throw new ArgumentOutOfRangeException("iIndex");

			return m_lWindowAssocs[iIndex];
		}

		public void Add(AutoTypeAssociation a)
		{
			if(a == null) { Debug.Assert(false); throw new ArgumentNullException("a"); }

			m_lWindowAssocs.Add(a);
		}

		public void Insert(int iIndex, AutoTypeAssociation a)
		{
			if((iIndex < 0) || (iIndex > m_lWindowAssocs.Count))
				throw new ArgumentOutOfRangeException("iIndex");
			if(a == null) { Debug.Assert(false); throw new ArgumentNullException("a"); }

			m_lWindowAssocs.Insert(iIndex, a);
		}

		public void RemoveAt(int iIndex)
		{
			if((iIndex < 0) || (iIndex >= m_lWindowAssocs.Count))
				throw new ArgumentOutOfRangeException("iIndex");

			m_lWindowAssocs.RemoveAt(iIndex);
		}

		// public void Sort()
		// {
		//	m_lWindowAssocs.Sort(AutoTypeConfig.AssocCompareFn);
		// }

		// private static int AssocCompareFn(AutoTypeAssociation x,
		//	AutoTypeAssociation y)
		// {
		//	if(x == null) { Debug.Assert(false); return ((y == null) ? 0 : -1); }
		//	if(y == null) { Debug.Assert(false); return 1; }
		//	int cn = x.WindowName.CompareTo(y.WindowName);
		//	if(cn != 0) return cn;
		//	return x.Sequence.CompareTo(y.Sequence);
		// }
	}
}
