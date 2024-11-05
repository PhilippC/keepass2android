/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using KeePassLib;
using KeePassLib.Interfaces;

namespace keepass2android
{
	public class DeleteEntry : DeleteRunnable {

        private readonly PwEntry _entry;
	    private UiStringKey _statusMessage;

	    public DeleteEntry(Activity activiy, IKp2aApp app, PwEntry entry, OnFinish finish):base(activiy, finish, app) {
			Ctx = activiy;
			Db = app.FindDatabaseForElement(entry);
			_entry = entry;
			
		}

		public override bool CanRecycle
		{
			get
			{
				return Db.DatabaseFormat.CanRecycle && CanRecycleGroup(_entry.ParentGroup);
			}
		}

		protected override UiStringKey QuestionRecycleResourceId
		{
			get
			{
				return UiStringKey.AskDeletePermanentlyEntry;
			}
		}

		protected override UiStringKey QuestionNoRecycleResourceId
		{
			get
			{
				return UiStringKey.AskDeletePermanentlyEntryNoRecycle;
			}
		}

	    protected override void PerformDelete(List<PwGroup> touchedGroups, List<PwGroup> permanentlyDeletedGroups)
	    {
	        DoDeleteEntry(_entry, touchedGroups);
	    }

	    public override UiStringKey StatusMessage
	    {
	        get { return UiStringKey.DeletingEntry; }
	    }
	}

}

