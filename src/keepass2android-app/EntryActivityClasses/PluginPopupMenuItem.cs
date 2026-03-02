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

using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Keepass2android.Pluginsdk;

namespace keepass2android
{
  /// <summary>
  /// Represents a popup menu item in EntryActivity which was added by a plugin. The click will therefore broadcast to the plugin.
  /// </summary>
  class PluginPopupMenuItem : IPopupMenuItem
  {
    private readonly EntryActivity _activity;
    private readonly string _pluginPackage;
    private readonly string _fieldId;
    private readonly string _popupItemId;
    private readonly string _displayText;
    private readonly int _iconId;
    private readonly Bundle _bundleExtra;

    public PluginPopupMenuItem(EntryActivity activity, string pluginPackage, string fieldId, string popupItemId, string displayText, int iconId, Bundle bundleExtra)
    {
      _activity = activity;
      _pluginPackage = pluginPackage;
      _fieldId = fieldId;
      _popupItemId = popupItemId;
      _displayText = displayText;
      _iconId = iconId;
      _bundleExtra = bundleExtra;
    }

    public Drawable Icon
    {
      get { return _activity.PackageManager.GetResourcesForApplication(_pluginPackage).GetDrawable(_iconId); }
    }
    public string Text
    {
      get { return _displayText; }
    }

    public string PopupItemId
    {
      get { return _popupItemId; }
    }

    public void HandleClick()
    {
      Intent i = new Intent(Strings.ActionEntryActionSelected);
      i.SetPackage(_pluginPackage);
      i.PutExtra(Strings.ExtraActionData, _bundleExtra);
      i.PutExtra(Strings.ExtraFieldId, _fieldId);
      i.PutExtra(Strings.ExtraSender, _activity.PackageName);

      _activity.AddEntryToIntent(i);

      _activity.SendBroadcast(i);
    }
  }
}