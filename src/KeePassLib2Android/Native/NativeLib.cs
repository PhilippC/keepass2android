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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

#if !KeePassUAP
using System.IO;
using System.Threading;
using System.Windows.Forms;
#endif

using KeePassLib.Utility;

namespace KeePassLib.Native
{
	/// <summary>
	/// Interface to native library (library containing fast versions of
	/// several cryptographic functions).
	/// </summary>
	public static class NativeLib
	{
		private static bool m_bAllowNative = true;

		/// <summary>
		/// If this property is set to <c>true</c>, the native library is used.
		/// If it is <c>false</c>, all calls to functions in this class will fail.
		/// </summary>
		public static bool AllowNative
		{
			get { return m_bAllowNative; }
			set { m_bAllowNative = value; }
		}

		private static int? g_oiPointerSize = null;
		/// <summary>
		/// Size of a native pointer (in bytes).
		/// </summary>
		public static int PointerSize
		{
			get
			{
				if(!g_oiPointerSize.HasValue)
#if KeePassUAP
					g_oiPointerSize = Marshal.SizeOf<IntPtr>();
#else
					g_oiPointerSize = Marshal.SizeOf(typeof(IntPtr));
#endif
				return g_oiPointerSize.Value;
			}
		}

		private static ulong? m_ouMonoVersion = null;
		public static ulong MonoVersion
		{
			get
			{
				if(m_ouMonoVersion.HasValue) return m_ouMonoVersion.Value;

				ulong uVersion = 0;
				try
				{
					Type t = Type.GetType("Mono.Runtime");
					if(t != null)
					{
						MethodInfo mi = t.GetMethod("GetDisplayName",
							BindingFlags.NonPublic | BindingFlags.Static);
						if(mi != null)
						{
							string strName = (mi.Invoke(null, null) as string);
							if(!string.IsNullOrEmpty(strName))
							{
								Match m = Regex.Match(strName, "\\d+(\\.\\d+)+");
								if(m.Success)
									uVersion = StrUtil.ParseVersion(m.Value);
								else { Debug.Assert(false); }
							}
							else { Debug.Assert(false); }
						}
						else { Debug.Assert(false); }
					}
				}
				catch(Exception) { Debug.Assert(false); }

				m_ouMonoVersion = uVersion;
				return uVersion;
			}
		}

		/// <summary>
		/// Determine if the native library is installed.
		/// </summary>
		/// <returns>Returns <c>true</c>, if the native library is installed.</returns>
		public static bool IsLibraryInstalled()
		{
			byte[] pDummy0 = new byte[32];
			byte[] pDummy1 = new byte[32];

			// Save the native state
			bool bCachedNativeState = m_bAllowNative;

			// Temporarily allow native functions and try to load the library
			m_bAllowNative = true;
			bool bResult = TransformKey256(pDummy0, pDummy1, 16);

			// Pop native state and return result
			m_bAllowNative = bCachedNativeState;
			return bResult;
		}

		private static bool? m_bIsUnix = null;
		public static bool IsUnix()
		{
			if(m_bIsUnix.HasValue) return m_bIsUnix.Value;

			PlatformID p = GetPlatformID();

			// Mono defines Unix as 128 in early .NET versions
#if !KeePassLibSD
			m_bIsUnix = ((p == PlatformID.Unix) || (p == PlatformID.MacOSX) ||
				((int)p == 128));
#else
			m_bIsUnix = (((int)p == 4) || ((int)p == 6) || ((int)p == 128));
#endif
			return m_bIsUnix.Value;
		}

		private static PlatformID? m_platID = null;
		public static PlatformID GetPlatformID()
		{
			if(m_platID.HasValue) return m_platID.Value;

#if KeePassUAP
			m_platID = EnvironmentExt.OSVersion.Platform;
#else
			m_platID = Environment.OSVersion.Platform;
#endif

#if (!KeePassLibSD && !KeePassUAP)
			// Mono returns PlatformID.Unix on Mac OS X, workaround this
			if(m_platID.Value == PlatformID.Unix)
			{
				if((RunConsoleApp("uname", null) ?? string.Empty).Trim().Equals(
					"Darwin", StrUtil.CaseIgnoreCmp))
					m_platID = PlatformID.MacOSX;
			}
#endif

			return m_platID.Value;
		}

		private static DesktopType? m_tDesktop = null;
		public static DesktopType GetDesktopType()
		{
			if(!m_tDesktop.HasValue)
			{
				DesktopType t = DesktopType.None;
				if(!IsUnix()) t = DesktopType.Windows;
				else
				{
					try
					{
						string strXdg = (Environment.GetEnvironmentVariable(
							"XDG_CURRENT_DESKTOP") ?? string.Empty).Trim();
						string strGdm = (Environment.GetEnvironmentVariable(
							"GDMSESSION") ?? string.Empty).Trim();
						StringComparison sc = StrUtil.CaseIgnoreCmp;

						if(strXdg.Equals("Unity", sc))
							t = DesktopType.Unity;
						else if(strXdg.Equals("LXDE", sc))
							t = DesktopType.Lxde;
						else if(strXdg.Equals("XFCE", sc))
							t = DesktopType.Xfce;
						else if(strXdg.Equals("MATE", sc))
							t = DesktopType.Mate;
						else if(strXdg.Equals("X-Cinnamon", sc))
							t = DesktopType.Cinnamon;
						else if(strXdg.Equals("Pantheon", sc)) // Elementary OS
							t = DesktopType.Pantheon;
						else if(strXdg.Equals("KDE", sc) || // Mint 16
							strGdm.Equals("kde-plasma", sc)) // Ubuntu 12.04
							t = DesktopType.Kde;
						else if(strXdg.Equals("GNOME", sc))
						{
							if(strGdm.Equals("cinnamon", sc)) // Mint 13
								t = DesktopType.Cinnamon;
							else t = DesktopType.Gnome;
						}
					}
					catch(Exception) { Debug.Assert(false); }
				}

				m_tDesktop = t;
			}

			return m_tDesktop.Value;
		}

#if (!KeePassLibSD && !KeePassUAP)
		public static string RunConsoleApp(string strAppPath, string strParams)
		{
			return RunConsoleApp(strAppPath, strParams, null);
		}

		public static string RunConsoleApp(string strAppPath, string strParams,
			string strStdInput)
		{
			return RunConsoleApp(strAppPath, strParams, strStdInput,
				(AppRunFlags.GetStdOutput | AppRunFlags.WaitForExit));
		}

		private delegate string RunProcessDelegate();

		public static string RunConsoleApp(string strAppPath, string strParams,
			string strStdInput, AppRunFlags f)
		{
			if(strAppPath == null) throw new ArgumentNullException("strAppPath");
			if(strAppPath.Length == 0) throw new ArgumentException("strAppPath");

			bool bStdOut = ((f & AppRunFlags.GetStdOutput) != AppRunFlags.None);

			RunProcessDelegate fnRun = delegate()
			{
				try
				{
					ProcessStartInfo psi = new ProcessStartInfo();

					psi.CreateNoWindow = true;
					psi.FileName = strAppPath;
					psi.WindowStyle = ProcessWindowStyle.Hidden;
					psi.UseShellExecute = false;
					psi.RedirectStandardOutput = bStdOut;

					if(strStdInput != null) psi.RedirectStandardInput = true;

					if(!string.IsNullOrEmpty(strParams)) psi.Arguments = strParams;

					Process p = Process.Start(psi);

					if(strStdInput != null)
					{
						EnsureNoBom(p.StandardInput);

						p.StandardInput.Write(strStdInput);
						p.StandardInput.Close();
					}

					string strOutput = string.Empty;
					if(bStdOut) strOutput = p.StandardOutput.ReadToEnd();

					if((f & AppRunFlags.WaitForExit) != AppRunFlags.None)
						p.WaitForExit();
					else if((f & AppRunFlags.GCKeepAlive) != AppRunFlags.None)
					{
						Thread th = new Thread(delegate()
						{
							try { p.WaitForExit(); }
							catch(Exception) { Debug.Assert(false); }
						});
						th.Start();
					}

					return strOutput;
				}
				catch(Exception) { Debug.Assert(false); }

				return null;
			};

			if((f & AppRunFlags.DoEvents) != AppRunFlags.None)
			{
				List<Form> lDisabledForms = new List<Form>();
				if((f & AppRunFlags.DisableForms) != AppRunFlags.None)
				{
					foreach(Form form in Application.OpenForms)
					{
						if(!form.Enabled) continue;

						lDisabledForms.Add(form);
						form.Enabled = false;
					}
				}

				IAsyncResult ar = fnRun.BeginInvoke(null, null);

				while(!ar.AsyncWaitHandle.WaitOne(0))
				{
					Application.DoEvents();
					Thread.Sleep(2);
				}

				string strRet = fnRun.EndInvoke(ar);

				for(int i = lDisabledForms.Count - 1; i >= 0; --i)
					lDisabledForms[i].Enabled = true;

				return strRet;
			}

			return fnRun();
		}

		private static void EnsureNoBom(StreamWriter sw)
		{
			if(sw == null) { Debug.Assert(false); return; }
			if(!MonoWorkarounds.IsRequired(1219)) return;

			try
			{
				Encoding enc = sw.Encoding;
				if(enc == null) { Debug.Assert(false); return; }
				byte[] pbBom = enc.GetPreamble();
				if((pbBom == null) || (pbBom.Length == 0)) return;

				// For Mono >= 4.0 (using Microsoft's reference source)
				try
				{
					FieldInfo fi = typeof(StreamWriter).GetField("haveWrittenPreamble",
						BindingFlags.Instance | BindingFlags.NonPublic);
					if(fi != null)
					{
						fi.SetValue(sw, true);
						return;
					}
				}
				catch(Exception) { Debug.Assert(false); }

				// For Mono < 4.0
				FieldInfo fiPD = typeof(StreamWriter).GetField("preamble_done",
					BindingFlags.Instance | BindingFlags.NonPublic);
				if(fiPD != null) fiPD.SetValue(sw, true);
				else { Debug.Assert(false); }
			}
			catch(Exception) { Debug.Assert(false); }
		}
#endif

		/// <summary>
		/// Transform a key.
		/// </summary>
		/// <param name="pBuf256">Source and destination buffer.</param>
		/// <param name="pKey256">Key to use in the transformation.</param>
		/// <param name="uRounds">Number of transformation rounds.</param>
		/// <returns>Returns <c>true</c>, if the key was transformed successfully.</returns>
		public static bool TransformKey256(byte[] pBuf256, byte[] pKey256,
			ulong uRounds)
		{
#if KeePassUAP
			return false;
#else
			if(!m_bAllowNative) return false;

			KeyValuePair<IntPtr, IntPtr> kvp = PrepareArrays256(pBuf256, pKey256);
			bool bResult = false;

			try
			{
				bResult = NativeMethods.TransformKey(kvp.Key, kvp.Value, uRounds);
			}
			catch(Exception) { bResult = false; }

			if(bResult) GetBuffers256(kvp, pBuf256, pKey256);

			FreeArrays(kvp);
			return bResult;
#endif
		}

		/// <summary>
		/// Benchmark key transformation.
		/// </summary>
		/// <param name="uTimeMs">Number of milliseconds to perform the benchmark.</param>
		/// <param name="puRounds">Number of transformations done.</param>
		/// <returns>Returns <c>true</c>, if the benchmark was successful.</returns>
		public static bool TransformKeyBenchmark256(uint uTimeMs, out ulong puRounds)
		{
			puRounds = 0;

#if KeePassUAP
			return false;
#else
			if(!m_bAllowNative) return false;

			try { puRounds = NativeMethods.TransformKeyBenchmark(uTimeMs); }
			catch(Exception) { return false; }

			return true;
#endif
		}

		private static KeyValuePair<IntPtr, IntPtr> PrepareArrays256(byte[] pBuf256,
			byte[] pKey256)
		{
			Debug.Assert((pBuf256 != null) && (pBuf256.Length == 32));
			if(pBuf256 == null) throw new ArgumentNullException("pBuf256");
			if(pBuf256.Length != 32) throw new ArgumentException();

			Debug.Assert((pKey256 != null) && (pKey256.Length == 32));
			if(pKey256 == null) throw new ArgumentNullException("pKey256");
			if(pKey256.Length != 32) throw new ArgumentException();

			IntPtr hBuf = Marshal.AllocHGlobal(pBuf256.Length);
			Marshal.Copy(pBuf256, 0, hBuf, pBuf256.Length);

			IntPtr hKey = Marshal.AllocHGlobal(pKey256.Length);
			Marshal.Copy(pKey256, 0, hKey, pKey256.Length);

			return new KeyValuePair<IntPtr, IntPtr>(hBuf, hKey);
		}

		private static void GetBuffers256(KeyValuePair<IntPtr, IntPtr> kvpSource,
			byte[] pbDestBuf, byte[] pbDestKey)
		{
			if(kvpSource.Key != IntPtr.Zero)
				Marshal.Copy(kvpSource.Key, pbDestBuf, 0, pbDestBuf.Length);

			if(kvpSource.Value != IntPtr.Zero)
				Marshal.Copy(kvpSource.Value, pbDestKey, 0, pbDestKey.Length);
		}

		private static void FreeArrays(KeyValuePair<IntPtr, IntPtr> kvpPointers)
		{
			if(kvpPointers.Key != IntPtr.Zero)
				Marshal.FreeHGlobal(kvpPointers.Key);

			if(kvpPointers.Value != IntPtr.Zero)
				Marshal.FreeHGlobal(kvpPointers.Value);
		}
	}
}
