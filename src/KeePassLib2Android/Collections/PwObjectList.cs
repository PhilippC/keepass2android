/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2012 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using KeePassLib.Interfaces;

namespace KeePassLib.Collections
{
	/// <summary>
	/// List of objects that implement <c>IDeepCloneable</c>,
	/// and cannot be <c>null</c>.
	/// </summary>
	/// <typeparam name="T">Type specifier.</typeparam>
	public sealed class PwObjectList<T> : IEnumerable<T>
		where T : class, IDeepCloneable<T>
	{
		private List<T> m_vObjects = new List<T>();

		/// <summary>
		/// Get number of objects in this list.
		/// </summary>
		public uint UCount
		{
			get { return (uint)m_vObjects.Count; }
		}

		/// <summary>
		/// Construct a new list of objects.
		/// </summary>
		public PwObjectList()
		{
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_vObjects.GetEnumerator();
		}

		public IEnumerator<T> GetEnumerator()
		{
			return m_vObjects.GetEnumerator();
		}

		public void Clear()
		{
			// Do not destroy contained objects!
			m_vObjects.Clear();
		}

		/// <summary>
		/// Clone the current <c>PwObjectList</c>, including all
		/// stored objects (deep copy).
		/// </summary>
		/// <returns>New <c>PwObjectList</c>.</returns>
		public PwObjectList<T> CloneDeep()
		{
			PwObjectList<T> pl = new PwObjectList<T>();

			foreach(T po in m_vObjects)
				pl.Add(po.CloneDeep());

			return pl;
		}

		public PwObjectList<T> CloneShallow()
		{
			PwObjectList<T> tNew = new PwObjectList<T>();

			foreach(T po in m_vObjects) tNew.Add(po);

			return tNew;
		}

		public List<T> CloneShallowToList()
		{
			PwObjectList<T> tNew = CloneShallow();
			return tNew.m_vObjects;
		}

		/// <summary>
		/// Add an object to this list.
		/// </summary>
		/// <param name="pwObject">Object to be added.</param>
		/// <exception cref="System.ArgumentNullException">Thrown if the input
		/// parameter is <c>null</c>.</exception>
		public void Add(T pwObject)
		{
			Debug.Assert(pwObject != null);
			if(pwObject == null) throw new ArgumentNullException("pwObject");

			m_vObjects.Add(pwObject);
		}

		public void Add(PwObjectList<T> vObjects)
		{
			Debug.Assert(vObjects != null);
			if(vObjects == null) throw new ArgumentNullException("vObjects");

			foreach(T po in vObjects)
			{
				m_vObjects.Add(po);
			}
		}

		public void Add(List<T> vObjects)
		{
			Debug.Assert(vObjects != null);
			if(vObjects == null) throw new ArgumentNullException("vObjects");

			foreach(T po in vObjects)
			{
				m_vObjects.Add(po);
			}
		}

		/// <summary>
		/// Get an object of the list.
		/// </summary>
		/// <param name="uIndex">Index of the object to get. Must be valid, otherwise an
		/// exception is thrown.</param>
		/// <returns>Reference to an existing <c>T</c> object. Is never <c>null</c>.</returns>
		public T GetAt(uint uIndex)
		{
			Debug.Assert(uIndex < m_vObjects.Count);
			if(uIndex >= m_vObjects.Count) throw new ArgumentOutOfRangeException("uIndex");

			return m_vObjects[(int)uIndex];
		}

		public void SetAt(uint uIndex, T pwObject)
		{
			Debug.Assert(pwObject != null);
			if(pwObject == null) throw new ArgumentNullException("pwObject");
			if(uIndex >= (uint)m_vObjects.Count)
				throw new ArgumentOutOfRangeException("uIndex");

			m_vObjects[(int)uIndex] = pwObject;
		}

		/// <summary>
		/// Get a range of objects.
		/// </summary>
		/// <param name="uStartIndexIncl">Index of the first object to be
		/// returned (inclusive).</param>
		/// <param name="uEndIndexIncl">Index of the last object to be
		/// returned (inclusive).</param>
		/// <returns></returns>
		public List<T> GetRange(uint uStartIndexIncl, uint uEndIndexIncl)
		{
			if(uStartIndexIncl >= (uint)m_vObjects.Count)
				throw new ArgumentOutOfRangeException("uStartIndexIncl");
			if(uEndIndexIncl >= (uint)m_vObjects.Count)
				throw new ArgumentOutOfRangeException("uEndIndexIncl");
			if(uStartIndexIncl > uEndIndexIncl)
				throw new ArgumentException();

			List<T> list = new List<T>((int)(uEndIndexIncl - uStartIndexIncl) + 1);
			for(uint u = uStartIndexIncl; u <= uEndIndexIncl; ++u)
			{
				list.Add(m_vObjects[(int)u]);
			}

			return list;
		}

		public int IndexOf(T pwReference)
		{
			Debug.Assert(pwReference != null); if(pwReference == null) throw new ArgumentNullException("pwReference");

			return m_vObjects.IndexOf(pwReference);
		}

		/// <summary>
		/// Delete an object of this list. The object to be deleted is identified
		/// by a reference handle.
		/// </summary>
		/// <param name="pwReference">Reference of the object to be deleted.</param>
		/// <returns>Returns <c>true</c> if the object was deleted, <c>false</c> if
		/// the object wasn't found in this list.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown if the input
		/// parameter is <c>null</c>.</exception>
		public bool Remove(T pwReference)
		{
			Debug.Assert(pwReference != null); if(pwReference == null) throw new ArgumentNullException("pwReference");

			return m_vObjects.Remove(pwReference);
		}

		public void RemoveAt(uint uIndex)
		{
			m_vObjects.RemoveAt((int)uIndex);
		}

		/// <summary>
		/// Move an object up or down.
		/// </summary>
		/// <param name="tObject">The object to be moved.</param>
		/// <param name="bUp">Move one up. If <c>false</c>, move one down.</param>
		public void MoveOne(T tObject, bool bUp)
		{
			Debug.Assert(tObject != null);
			if(tObject == null) throw new ArgumentNullException("tObject");

			int nCount = m_vObjects.Count;
			if(nCount <= 1) return;

			int nIndex = m_vObjects.IndexOf(tObject);
			Debug.Assert(nIndex >= 0);

			if(bUp && (nIndex > 0)) // No assert for top item
			{
				T tTemp = m_vObjects[nIndex - 1];
				m_vObjects[nIndex - 1] = m_vObjects[nIndex];
				m_vObjects[nIndex] = tTemp;
			}
			else if(!bUp && (nIndex != (nCount - 1))) // No assert for bottom item
			{
				T tTemp = m_vObjects[nIndex + 1];
				m_vObjects[nIndex + 1] = m_vObjects[nIndex];
				m_vObjects[nIndex] = tTemp;
			}
		}

		/// <summary>
		/// Move some of the objects in this list to the top/bottom.
		/// </summary>
		/// <param name="vObjects">List of objects to be moved.</param>
		/// <param name="bTop">Move to top. If <c>false</c>, move to bottom.</param>
		public void MoveTopBottom(T[] vObjects, bool bTop)
		{
			Debug.Assert(vObjects != null);
			if(vObjects == null) throw new ArgumentNullException("vObjects");

			if(vObjects.Length == 0) return;

			int nCount = m_vObjects.Count;
			foreach(T t in vObjects) m_vObjects.Remove(t);

			if(bTop)
			{
				int nPos = 0;
				foreach(T t in vObjects)
				{
					m_vObjects.Insert(nPos, t);
					++nPos;
				}
			}
			else // Move to bottom
			{
				foreach(T t in vObjects) m_vObjects.Add(t);
			}

			Debug.Assert(nCount == m_vObjects.Count);
			if(nCount != m_vObjects.Count)
				throw new ArgumentException("At least one of the T objects in the vObjects list doesn't exist!");
		}

		public void Sort(IComparer<T> tComparer)
		{
			if(tComparer == null) throw new ArgumentNullException("tComparer");

			m_vObjects.Sort(tComparer);
		}

		public static PwObjectList<T> FromArray(T[] tArray)
		{
			if(tArray == null) throw new ArgumentNullException("tArray");

			PwObjectList<T> l = new PwObjectList<T>();
			foreach(T t in tArray) { l.Add(t); }
			return l;
		}

		public static PwObjectList<T> FromList(List<T> tList)
		{
			if(tList == null) throw new ArgumentNullException("tList");

			PwObjectList<T> l = new PwObjectList<T>();
			l.Add(tList);
			return l;
		}
	}
}
