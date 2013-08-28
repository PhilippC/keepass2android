using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Android.Content;
using KeePassLib;
using KeePassLib.Interfaces;

namespace keepass2android.database.edit
{
	public class MoveElement: RunnableOnFinish
	{
		private readonly IStructureItem _elementToMove;
		private readonly PwGroup _targetGroup;
		private readonly Context _ctx;
		private readonly IKp2aApp _app;

		public MoveElement(IStructureItem elementToMove, PwGroup targetGroup, Context ctx, IKp2aApp app, OnFinish finish) : base(finish)
		{
			_elementToMove = elementToMove;
			_targetGroup = targetGroup;
			_ctx = ctx;
			_app = app;
		}

		public override void Run()
		{
			
			_app.GetDb().Dirty.Add(_elementToMove.ParentGroup);

			PwGroup pgParent = _elementToMove.ParentGroup;
			if (pgParent != _targetGroup)
			{
				if (pgParent != null) // Remove from parent
				{
					PwEntry entry = _elementToMove as PwEntry;
					if (entry != null)
					{
						pgParent.Entries.Remove(entry);
						_targetGroup.AddEntry(entry, true, true);
					}
					else
					{
						PwGroup group = (PwGroup)_elementToMove;
						pgParent.Groups.Remove(group);
						_targetGroup.AddGroup(group, true, true);
					}
				}
			}

			_onFinishToRun = new ActionOnFinish((success, message) =>
			{
				if (!success)
				{	// Let's not bother recovering from a failure.
					_app.LockDatabase(false);
				}
			}, OnFinishToRun);

			// Save
			SaveDb save = new SaveDb(_ctx, _app, OnFinishToRun, false);
			save.SetStatusLogger(StatusLogger);
			save.Run();
		}
	}
}
