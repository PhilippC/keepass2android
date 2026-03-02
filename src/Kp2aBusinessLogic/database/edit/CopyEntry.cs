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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Security;

namespace keepass2android.database.edit
{
  public class CopyEntry : AddEntry
  {
    public CopyEntry(IKp2aApp app, PwEntry entry, OnOperationFinishedHandler operationFinishedHandler, Database db)
        : base(db, app, CreateCopy(entry, app), entry.ParentGroup, operationFinishedHandler)
    {
    }

    private static PwEntry CreateCopy(PwEntry entry, IKp2aApp app)
    {
      var newEntry = entry.CloneDeep();
      newEntry.SetUuid(new PwUuid(true), true); // Create new UUID
      string strTitle = newEntry.Strings.ReadSafe(PwDefs.TitleField);
      newEntry.Strings.Set(PwDefs.TitleField, new ProtectedString(
              false, strTitle + " - " + app.GetResourceString(UiStringKey.DuplicateTitle)));

      return newEntry;
    }
  }
}