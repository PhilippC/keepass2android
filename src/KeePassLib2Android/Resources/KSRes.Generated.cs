// This is a generated file!
// Do not edit manually, changes will be overwritten.

using System;
using System.Collections.Generic;

namespace KeePassLib.Resources
{
	/// <summary>
	/// A strongly-typed resource class, for looking up localized strings, etc.
	/// </summary>
	public static class KSRes
	{
		private static string TryGetEx(Dictionary<string, string> dictNew,
			string strName, string strDefault)
		{
			string strTemp;

			if(dictNew.TryGetValue(strName, out strTemp))
				return strTemp;

			return strDefault;
		}

		public static void SetTranslatedStrings(Dictionary<string, string> dictNew)
		{
			if(dictNew == null) throw new ArgumentNullException("dictNew");

			m_strTest = TryGetEx(dictNew, "Test", m_strTest);
		}

		private static readonly string[] m_vKeyNames = {
			"Test"
		};

		public static string[] GetKeyNames()
		{
			return m_vKeyNames;
		}

		private static string m_strTest =
			@"Test";
		/// <summary>
		/// Look up a localized string similar to
		/// 'Test'.
		/// </summary>
		public static string Test
		{
			get { return m_strTest; }
		}
	}
}
