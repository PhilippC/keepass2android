/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2013 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.IO;
using System.Drawing;

using KeePassLib;
using KeePassLib.Interfaces;

namespace KeePass.DataExchange
{
	public abstract class FileFormatProvider
	{
		public abstract bool SupportsImport { get; }
		public abstract bool SupportsExport { get; }

		public abstract string FormatName { get; }

		public virtual string DisplayName
		{
			get { return this.FormatName; }
		}

		/// <summary>
		/// Default file name extension, without leading dot.
		/// If there are multiple default/equivalent extensions
		/// (like e.g. "html" and "htm"), specify all of them
		/// separated by a '|' (e.g. "html|htm").
		/// </summary>
		public virtual string DefaultExtension
		{
			get { return string.Empty; }
		}

		
		public virtual bool RequiresFile
		{
			get { return true; }
		}

		public virtual bool SupportsUuids
		{
			get { return false; }
		}

		public virtual bool RequiresKey
		{
			get { return false; }
		}

		/// <summary>
		/// This property specifies if entries are only appended to the
		/// end of the root group. This is true for example if the
		/// file format doesn't support groups (i.e. no hierarchy).
		/// </summary>
		public virtual bool ImportAppendsToRootGroupOnly
		{
			get { return false; }
		}


		
		/// <summary>
		/// Called before the <c>Export</c> method is invoked.
		/// </summary>
		/// <returns>Returns <c>true</c>, if the <c>Export</c> method
		/// can be invoked. If it returns <c>false</c>, something has
		/// failed and the export process should be aborted.</returns>
		public virtual bool TryBeginExport()
		{
			return true;
		}

		/// <summary>
		/// Import a stream into a database. Throws an exception if an error
		/// occurs. Do not call the base class method when overriding it.
		/// </summary>
		/// <param name="pwStorage">Data storage into which the data will be imported.</param>
		/// <param name="sInput">Input stream to read the data from.</param>
		/// <param name="slLogger">Status logger. May be <c>null</c>.</param>
		public abstract void Import(PwDatabase pwStorage, Stream sInput,
		                            IStatusLogger slLogger);
		/// <summary>
		/// Export data into a stream. Throws an exception if an error
		/// occurs (like writing to stream fails, etc.). Returns <c>true</c>,
		/// if the export was successful.
		/// </summary>
		/// <param name="pwExportInfo">Contains the data source and detailed
		/// information about which entries should be exported.</param>
		/// <param name="sOutput">Output stream to write the data to.</param>
		/// <param name="slLogger">Status logger. May be <c>null</c>.</param>
		/// <returns>Returns <c>false</c>, if the user has aborted the export
		/// process (like clicking Cancel in an additional export settings
		/// dialog).</returns>
		public virtual bool Export(PwExportInfo pwExportInfo, Stream sOutput,
			IStatusLogger slLogger)
		{
			throw new NotSupportedException();
		}
	}
}
