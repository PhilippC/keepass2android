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
using System.Diagnostics;
using System.IO;
using System.Text;

using KeePassLib.Resources;
using KeePassLib.Utility;

namespace KeePassLib.Collections
{
	public class VariantDictionary : ICloneable
	{
		private const ushort VdVersion = 0x0100;
		private const ushort VdmCritical = 0xFF00;
		private const ushort VdmInfo = 0x00FF;

		private Dictionary<string, object> m_d = new Dictionary<string, object>();

		private enum VdType : byte
		{
			None = 0,

			// Byte = 0x02,
			// UInt16 = 0x03,
			UInt32 = 0x04,
			UInt64 = 0x05,

			// Signed mask: 0x08
			Bool = 0x08,
			// SByte = 0x0A,
			// Int16 = 0x0B,
			Int32 = 0x0C,
			Int64 = 0x0D,

			// Float = 0x10,
			// Double = 0x11,
			// Decimal = 0x12,

			// Char = 0x17, // 16-bit Unicode character
			String = 0x18,

			// Array mask: 0x40
			ByteArray = 0x42
		}

		public int Count
		{
			get { return m_d.Count; }
		}

		public VariantDictionary()
		{
			Debug.Assert((VdmCritical & VdmInfo) == ushort.MinValue);
			Debug.Assert((VdmCritical | VdmInfo) == ushort.MaxValue);
		}

		private bool Get<T>(string strName, out T t)
		{
			t = default(T);

			if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); return false; }

			object o;
			if(!m_d.TryGetValue(strName, out o)) return false; // No assert

			if(o == null) { Debug.Assert(false); return false; }
			if(o.GetType() != typeof(T)) { Debug.Assert(false); return false; }

			t = (T)o;
			return true;
		}

		private void SetStruct<T>(string strName, T t)
			where T : struct
		{
			if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); return; }

#if DEBUG
			T tEx;
			Get<T>(strName, out tEx); // Assert same type
#endif

			m_d[strName] = t;
		}

		private void SetRef<T>(string strName, T t)
			where T : class
		{
			if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); return; }
			if(t == null) { Debug.Assert(false); return; }

#if DEBUG
			T tEx;
			Get<T>(strName, out tEx); // Assert same type
#endif

			m_d[strName] = t;
		}

		public bool Remove(string strName)
		{
			if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); return false; }

			return m_d.Remove(strName);
		}

		public void CopyTo(VariantDictionary d)
		{
			if(d == null) { Debug.Assert(false); return; }

			// Do not clear the target
			foreach(KeyValuePair<string, object> kvp in m_d)
			{
				d.m_d[kvp.Key] = kvp.Value;
			}
		}

		public Type GetTypeOf(string strName)
		{
			if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); return null; }

			object o;
			m_d.TryGetValue(strName, out o);
			if(o == null) return null; // No assert

			return o.GetType();
		}

		public uint GetUInt32(string strName, uint uDefault)
		{
			uint u;
			if(Get<uint>(strName, out u)) return u;
			return uDefault;
		}

		public void SetUInt32(string strName, uint uValue)
		{
			SetStruct<uint>(strName, uValue);
		}

		public ulong GetUInt64(string strName, ulong uDefault)
		{
			ulong u;
			if(Get<ulong>(strName, out u)) return u;
			return uDefault;
		}

		public void SetUInt64(string strName, ulong uValue)
		{
			SetStruct<ulong>(strName, uValue);
		}

		public bool GetBool(string strName, bool bDefault)
		{
			bool b;
			if(Get<bool>(strName, out b)) return b;
			return bDefault;
		}

		public void SetBool(string strName, bool bValue)
		{
			SetStruct<bool>(strName, bValue);
		}

		public int GetInt32(string strName, int iDefault)
		{
			int i;
			if(Get<int>(strName, out i)) return i;
			return iDefault;
		}

		public void SetInt32(string strName, int iValue)
		{
			SetStruct<int>(strName, iValue);
		}

		public long GetInt64(string strName, long lDefault)
		{
			long l;
			if(Get<long>(strName, out l)) return l;
			return lDefault;
		}

		public void SetInt64(string strName, long lValue)
		{
			SetStruct<long>(strName, lValue);
		}

		public string GetString(string strName)
		{
			string str;
			Get<string>(strName, out str);
			return str;
		}

		public void SetString(string strName, string strValue)
		{
			SetRef<string>(strName, strValue);
		}

		public byte[] GetByteArray(string strName)
		{
			byte[] pb;
			Get<byte[]>(strName, out pb);
			return pb;
		}

		public void SetByteArray(string strName, byte[] pbValue)
		{
			SetRef<byte[]>(strName, pbValue);
		}

		/// <summary>
		/// Create a deep copy.
		/// </summary>
		public virtual object Clone()
		{
			VariantDictionary vdNew = new VariantDictionary();

			foreach(KeyValuePair<string, object> kvp in m_d)
			{
				object o = kvp.Value;
				if(o == null) { Debug.Assert(false); continue; }

				Type t = o.GetType();
				if(t == typeof(byte[]))
				{
					byte[] p = (byte[])o;
					byte[] pNew = new byte[p.Length];
					if(p.Length > 0) Array.Copy(p, pNew, p.Length);

					o = pNew;
				}

				vdNew.m_d[kvp.Key] = o;
			}

			return vdNew;
		}

		public static byte[] Serialize(VariantDictionary p)
		{
			if(p == null) { Debug.Assert(false); return null; }

			byte[] pbRet;
			using(MemoryStream ms = new MemoryStream())
			{
				MemUtil.Write(ms, MemUtil.UInt16ToBytes(VdVersion));

				foreach(KeyValuePair<string, object> kvp in p.m_d)
				{
					string strName = kvp.Key;
					if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); continue; }
					byte[] pbName = StrUtil.Utf8.GetBytes(strName);

					object o = kvp.Value;
					if(o == null) { Debug.Assert(false); continue; }

					Type t = o.GetType();
					VdType vt = VdType.None;
					byte[] pbValue = null;
					if(t == typeof(uint))
					{
						vt = VdType.UInt32;
						pbValue = MemUtil.UInt32ToBytes((uint)o);
					}
					else if(t == typeof(ulong))
					{
						vt = VdType.UInt64;
						pbValue = MemUtil.UInt64ToBytes((ulong)o);
					}
					else if(t == typeof(bool))
					{
						vt = VdType.Bool;
						pbValue = new byte[1];
						pbValue[0] = ((bool)o ? (byte)1 : (byte)0);
					}
					else if(t == typeof(int))
					{
						vt = VdType.Int32;
						pbValue = MemUtil.Int32ToBytes((int)o);
					}
					else if(t == typeof(long))
					{
						vt = VdType.Int64;
						pbValue = MemUtil.Int64ToBytes((long)o);
					}
					else if(t == typeof(string))
					{
						vt = VdType.String;
						pbValue = StrUtil.Utf8.GetBytes((string)o);
					}
					else if(t == typeof(byte[]))
					{
						vt = VdType.ByteArray;
						pbValue = (byte[])o;
					}
					else { Debug.Assert(false); continue; } // Unknown type

					ms.WriteByte((byte)vt);
					MemUtil.Write(ms, MemUtil.Int32ToBytes(pbName.Length));
					MemUtil.Write(ms, pbName);
					MemUtil.Write(ms, MemUtil.Int32ToBytes(pbValue.Length));
					MemUtil.Write(ms, pbValue);
				}

				ms.WriteByte((byte)VdType.None);
				pbRet = ms.ToArray();
			}

			return pbRet;
		}

		public static VariantDictionary Deserialize(byte[] pb)
		{
			if(pb == null) { Debug.Assert(false); return null; }

			VariantDictionary d = new VariantDictionary();
			using(MemoryStream ms = new MemoryStream(pb, false))
			{
				ushort uVersion = MemUtil.BytesToUInt16(MemUtil.Read(ms, 2));
				if((uVersion & VdmCritical) > (VdVersion & VdmCritical))
					throw new FormatException(KLRes.FileNewVerReq);

				while(true)
				{
					int iType = ms.ReadByte();
					if(iType < 0) throw new EndOfStreamException(KLRes.FileCorrupted);
					byte btType = (byte)iType;
					if(btType == (byte)VdType.None) break;

					int cbName = MemUtil.BytesToInt32(MemUtil.Read(ms, 4));
					byte[] pbName = MemUtil.Read(ms, cbName);
					if(pbName.Length != cbName)
						throw new EndOfStreamException(KLRes.FileCorrupted);
					string strName = StrUtil.Utf8.GetString(pbName);

					int cbValue = MemUtil.BytesToInt32(MemUtil.Read(ms, 4));
					byte[] pbValue = MemUtil.Read(ms, cbValue);
					if(pbValue.Length != cbValue)
						throw new EndOfStreamException(KLRes.FileCorrupted);

					switch(btType)
					{
						case (byte)VdType.UInt32:
							if(cbValue == 4)
								d.SetUInt32(strName, MemUtil.BytesToUInt32(pbValue));
							else { Debug.Assert(false); }
							break;

						case (byte)VdType.UInt64:
							if(cbValue == 8)
								d.SetUInt64(strName, MemUtil.BytesToUInt64(pbValue));
							else { Debug.Assert(false); }
							break;

						case (byte)VdType.Bool:
							if(cbValue == 1)
								d.SetBool(strName, (pbValue[0] != 0));
							else { Debug.Assert(false); }
							break;

						case (byte)VdType.Int32:
							if(cbValue == 4)
								d.SetInt32(strName, MemUtil.BytesToInt32(pbValue));
							else { Debug.Assert(false); }
							break;

						case (byte)VdType.Int64:
							if(cbValue == 8)
								d.SetInt64(strName, MemUtil.BytesToInt64(pbValue));
							else { Debug.Assert(false); }
							break;

						case (byte)VdType.String:
							d.SetString(strName, StrUtil.Utf8.GetString(pbValue));
							break;

						case (byte)VdType.ByteArray:
							d.SetByteArray(strName, pbValue);
							break;

						default:
							Debug.Assert(false); // Unknown type
							break;
					}
				}

				Debug.Assert(ms.ReadByte() < 0);
			}

			return d;
		}
	}
}
