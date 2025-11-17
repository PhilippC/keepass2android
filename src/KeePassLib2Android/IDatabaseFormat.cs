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

using System.IO;
using KeePassLib.Interfaces;
using KeePassLib.Keys;

namespace KeePassLib
{
  public interface IDatabaseFormat
  {
    void PopulateDatabaseFromStream(PwDatabase db, Stream s, IStatusLogger slLogger);

    byte[] HashOfLastStream { get; }

    bool CanWrite { get; }
    string SuccessMessage { get; }
    void Save(PwDatabase kpDatabase, Stream stream);

    bool CanHaveEntriesInRootGroup { get; }
    bool CanHaveMultipleAttachments { get; }
    bool CanHaveCustomFields { get; }
    bool HasDefaultUsername { get; }
    bool HasDatabaseName { get; }
    bool SupportsAttachmentKeys { get; }
    bool SupportsTags { get; }
    bool SupportsOverrideUrl { get; }
    bool CanRecycle { get; }
    bool SupportsTemplates { get; }
  }
}