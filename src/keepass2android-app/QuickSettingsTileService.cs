/*
 * Keepass2Android - Password Manager for Android
 * Copyright (C) 2025 Philipp Crocoll
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
 * * ---
 * Module: QuickSettingsTileService
 * Description: Provides a system-wide Quick Settings Tile to launch the application.
 * This service allows users to quickly access their password database from the 
 * Android notification shade.
 */

using System;
using Android.App;
using Android.Content;
using Android.Service.QuickSettings;
using Android.Graphics.Drawables;

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
        /// <summary>
        /// Called when the user taps the tile in the Quick Settings panel.
        /// </summary>
        public override void OnClick()
        {
            base.OnClick();

            // Security check: prompt for device unlock if the phone is currently locked.
            if (IsLocked)
            {
                UnlockAndRun(new Java.Lang.Runnable(() => {
                    StartKp2a();
                }));
            }
            else
            {
                StartKp2a();
            }
        }

        /// <summary>
        /// Launches the main application entry point.
        /// </summary>
        private void StartKp2a()
        {
            // Redirect to LaunchActivity to handle database lock/unlock state.
            Intent intent = new Intent(this, typeof(LaunchActivity));
            
            // Flags to handle activity stack and background-to-foreground transition.
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            
            // Closes the notification drawer and executes the intent.
            StartActivityAndCollapse(intent);
        }
    }
}