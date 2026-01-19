/*
 * Keepass2Android - Password Manager for Android
 * Copyright (C) 2026 Philipp Crocoll
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * ---
 * Module: QuickSettingsTileService
 * Description: Provides a system-wide Quick Settings Tile to launch the application.
 * This service allows users to quickly access their password database from the 
 * Android notification shade.
 */

using System;
using Android.App;
using Android.Content;
using Android.Service.QuickSettings;

namespace keepass2android
{
    /// <summary>
    /// Service to provide a Quick Settings Tile for the Android notification shade.
    /// </summary>
    [Service(Name = "keepass2android.QuickSettingsTileService",
             Permission = Android.Manifest.Permission.BindQuickSettingsTile,
             Label = "@string/app_name", 
             Icon = "@drawable/ic_quick_settings_tile", 
             Exported = true)]
    [IntentFilter(new[] { TileService.ActionQsTile })]
    public class QuickSettingsTileService : TileService
    {
        public override void OnClick()
        {
            base.OnClick();

            if (IsLocked)
            {
                // Ensures the device is unlocked before attempting to show the UI.
                UnlockAndRun(new Java.Lang.Runnable(StartKp2a));
            }
            else
            {
                StartKp2a();
            }
        }

        private void StartKp2a()
        {
            // Use the verified entry point: SelectCurrentDbActivity.
            Intent intent = new Intent(this, typeof(SelectCurrentDbActivity));
            
            // MANDATORY: Services must launch activities in a New Task to avoid crashes.
            intent.AddFlags(ActivityFlags.NewTask);
            
            // Closes the notification shade and executes the intent.
            StartActivityAndCollapse(intent);
        }
    }
}
