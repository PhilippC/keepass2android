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
using KeePass.Util.Spr;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;

namespace keepass2android
{
    /// <summary>
    /// Represents the strings which are output from a PwEntry.
    /// </summary>
    /// In contrast to the original PwEntry, this means that placeholders are replaced. Also, plugins may modify
    /// or add fields.
    public class PwEntryOutput
    {
        private readonly PwEntry _entry;
        private readonly Database _db;
        private readonly ProtectedStringDictionary _outputStrings = new ProtectedStringDictionary();

        /// <summary>
        /// Constructs the PwEntryOutput by replacing the placeholders
        /// </summary>
        public PwEntryOutput(PwEntry entry, Database db)
        {
            _entry = entry;
            _db = db;

            foreach (var pair in entry.Strings)
            {
                _outputStrings.Set(pair.Key, new ProtectedString(entry.Strings.Get(pair.Key).IsProtected, GetStringAndReplacePlaceholders(pair.Key)));
            }
        }

        string GetStringAndReplacePlaceholders(string key)
        {
            String value = Entry.Strings.ReadSafe(key);
            value = SprEngine.Compile(value, new SprContext(Entry, _db.KpDatabase, SprCompileFlags.All));
            return value;
        }


        /// <summary>
        /// Returns the ID of the entry
        /// </summary>
        public PwUuid Uuid
        {
            get { return Entry.Uuid; }
        }

        /// <summary>
        /// The output strings for the represented entry
        /// </summary>
        public ProtectedStringDictionary OutputStrings { get { return _outputStrings; } }

        public PwEntry Entry
        {
            get { return _entry; }
        }

        /// <summary>
        /// if the entry was selected by searching for a URL, the query URL is returned here.
        /// </summary>
	    public string SearchUrl { get; set; }
    }
}