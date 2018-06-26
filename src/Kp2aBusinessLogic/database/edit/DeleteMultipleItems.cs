using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using KeePassLib;
using KeePassLib.Interfaces;

namespace keepass2android
{
	public class DeleteMultipleItems : DeleteRunnable
	{
		private readonly List<IStructureItem> _elementsToDelete;
		private readonly bool _canRecycle;

		public DeleteMultipleItems(Activity activity, Database db, List<IStructureItem> elementsToDelete, OnFinish finish, IKp2aApp app)
			: base(activity, finish, app)
		{
			_elementsToDelete = elementsToDelete;
			SetMembers(activity, db);

			//determine once. The property is queried for each delete operation, but might return false
			//after one entry/group is deleted (and thus in recycle bin and thus can't be recycled anymore)
			_canRecycle = DetermineCanRecycle();
		}

		private bool DetermineCanRecycle()
		{
			Android.Util.Log.Debug("KP2A", "CanRecycle?");
			if (!App.GetDb().DatabaseFormat.CanRecycle)
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