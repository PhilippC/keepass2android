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
using System.Linq;
using Android.App;
using Android.Content;
using KeePassLib;
using KeePassLib.Interfaces;

namespace keepass2android
{
    public class DeleteMultipleItemsFromOneDatabase : DeleteRunnable
    {
        private readonly List<IStructureItem> _elementsToDelete;
        private readonly bool _canRecycle;

        public DeleteMultipleItemsFromOneDatabase(Activity activity, Database db, List<IStructureItem> elementsToDelete, OnOperationFinishedHandler operationFinishedHandler, IKp2aApp app)
            : base(operationFinishedHandler, app)
        {
            _elementsToDelete = elementsToDelete;
            SetMembers(db);

            //determine once. The property is queried for each delete operation, but might return false
            //after one entry/group is deleted (and thus in recycle bin and thus can't be recycled anymore)
            _canRecycle = DetermineCanRecycle();
            ShowDatabaseIocInStatus = true;
        }

        private bool DetermineCanRecycle()
        {
            Android.Util.Log.Debug("KP2A", "CanRecycle?");
            if (!Db.DatabaseFormat.CanRecycle)
            {
                Android.Util.Log.Debug("KP2A", "CanRecycle? No because of DB format.");
                return false;
            }


            if (_elementsToDelete.OfType<PwGroup>().Any(g => !CanRecycleGroup(g)))
            {
                return false;
            }

            if (_elementsToDelete.OfType<PwEntry>().Any(e => !CanRecycleGroup(e.ParentGroup)))
            {
                return false;
            }
            Android.Util.Log.Debug("KP2A", "CanRecycle? Yes.");
            return true;
        }

        public override bool CanRecycle
        {
            get { return _canRecycle; }
        }

        protected override UiStringKey QuestionRecycleResourceId
        {
            get { return UiStringKey.AskDeletePermanentlyItems; }
        }

        protected override UiStringKey QuestionNoRecycleResourceId
        {
            get { return UiStringKey.AskDeletePermanentlyItemsNoRecycle; }
        }

        protected override void PerformDelete(List<PwGroup> touchedGroups, List<PwGroup> permanentlyDeletedGroups)
        {
            foreach (var g in _elementsToDelete.OfType<PwGroup>())
            {
                Android.Util.Log.Debug("KP2A", "Deleting " + g.Name);
                DoDeleteGroup(g, touchedGroups, permanentlyDeletedGroups);

            }

            foreach (var e in _elementsToDelete.OfType<PwEntry>())
            {
                Android.Util.Log.Debug("KP2A", "Deleting " + e.Strings.ReadSafe(PwDefs.TitleField));
                DoDeleteEntry(e, touchedGroups);
            }
        }

        public override UiStringKey StatusMessage
        {
            get { return UiStringKey.DeletingItems; }
        }
    }
}