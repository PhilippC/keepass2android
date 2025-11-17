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

using System.Collections.Generic;
using KeePassLib;
using KeePassLib.Security;

namespace keepass2android
{
  public abstract class EditModeBase
  {


    public virtual bool IsVisible(string fieldKey)
    {
      return true;
    }

    public virtual IEnumerable<string> SortExtraFieldKeys(IEnumerable<string> keys)
    {
      return keys;
    }

    protected bool? manualShowAddAttachments = null;

    public virtual bool ShowAddAttachments
    {
      get
      {
        if (manualShowAddAttachments != null) return (bool)manualShowAddAttachments;
        return true;
      }
      set { manualShowAddAttachments = value; }
    }


    protected bool? manualShowAddExtras = null;

    public virtual bool ShowAddExtras
    {
      get
      {
        if (manualShowAddExtras != null) return (bool)manualShowAddExtras;
        return true;
      }
      set { manualShowAddExtras = value; }
    }

    public virtual string GetTitle(string key)
    {
      return key;
    }

    public virtual string GetFieldType(string key)
    {
      return "";
    }

    public virtual void InitializeEntry(PwEntry entry)
    {
    }

    public virtual void PrepareForSaving(PwEntry entry)
    {
    }
  }

  /// <summary>
  /// Holds the state of the EntryEditActivity. This is required to be able to keep a partially modified entry in memory
  /// through the App variable. Serializing this state (especially the Entry/EntryInDatabase) can be a performance problem
  /// when there are big attachements.
  /// </summary>
  internal class EntryEditActivityState
  {
    internal PwEntry Entry, EntryInDatabase;
    internal bool ShowPassword = false;
    internal bool IsNew;
    internal PwIcon SelectedIconId;
    internal PwUuid SelectedCustomIconId = PwUuid.Zero;
    internal bool SelectedIcon = false;

    internal PwGroup ParentGroup;

    internal bool EntryModified;

    public EditModeBase EditMode { get; set; }

    //the key of the extra field to which the last triggered file selection process belongs
    public string LastTriggeredFileSelectionProcessKey;
  }
}

