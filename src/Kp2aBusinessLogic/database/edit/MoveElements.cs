using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Android.App;
using Android.Content;
using KeePassLib;
using KeePassLib.Interfaces;

namespace keepass2android.database.edit
{
	public class MoveElements: RunnableOnFinish
	{
		private readonly List<IStructureItem> _elementsToMove;
		private readonly PwGroup _targetGroup;
		private readonly Activity _ctx;
		private readonly IKp2aApp _app;

		public MoveElements(List<IStructureItem> elementsToMove, PwGroup targetGroup, Activity ctx, IKp2aApp app, OnFinish finish) : base(ctx, finish)
		{
			_elementsToMove = elementsToMove;
			_targetGroup = targetGroup;
			_ctx = ctx;
			_app = app;
		}

		public override void Run()
		{
            //check if we will run into problems. Then finish with error before we start doing anything.
            foreach (var _elementToMove in _elementsToMove)
            {
                PwGroup pgParent = _elementToMove.ParentGroup;
                if (pgParent != _targetGroup)
                {
                    if (pgParent != null) 
                    {
                        PwGroup group = _elementToMove as PwGroup;
                        if (group != null)
                        {
                            if ((_targetGroup == group) || (_targetGroup.IsContainedIn(group)))
                            {
                                Finish(false, _app.GetResourceString(UiStringKey.CannotMoveGroupHere));
                                return;
                            }
                            
                        }
                            
                    }
                }

            }

		    foreach (var elementToMove in _elementsToMove)
		    {

                _app.GetDb().Dirty.Add(elementToMove.ParentGroup);

                PwGroup pgParent = elementToMove.ParentGroup;
                if (pgParent != _targetGroup)
                {
                    if (pgParent != null) // Remove from parent
                    {
                        PwEntry entry = elementToMove as PwEntry;
                        if (entry != null)
                        {
                            pgParent.Entries.Remove(entry);
                            _targetGroup.AddEntry(entry, true, true);
                        }
                        else
                        {
                            PwGroup group = (PwGroup)elementToMove;
                            if ((_targetGroup == group) || (_targetGroup.IsContainedIn(group)))
                            {
                                Finish(false, _app.GetResourceString(UiStringKey.CannotMoveGroupHere));
                                return;
                            }
                            pgParent.Groups.Remove(group);
                            _targetGroup.AddGroup(group, true, true);
                        }
                    }
                }

		    }
			
			
			_onFinishToRun = new ActionOnFinish(ActiveActivity, (success, message, activity) =>
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
