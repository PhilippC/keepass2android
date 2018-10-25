using System;
using System.Collections.Generic;
using System.Linq;
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

		    HashSet<Database> removeDatabases = new HashSet<Database>();
            Database addDatabase = _app.FindDatabaseForGroupId(_targetGroup.Uuid);
		    if (addDatabase == null)
		    {
		        Finish(false, "Did not find target database. Did you lock it?");
		        return;
		    }

		    foreach (var elementToMove in _elementsToMove)
		    {

                _app.DirtyGroups.Add(elementToMove.ParentGroup);
                

                PwGroup pgParent = elementToMove.ParentGroup;
                if (pgParent != _targetGroup)
                {
                    if (pgParent != null) // Remove from parent
                    {
                        PwEntry entry = elementToMove as PwEntry;
                        if (entry != null)
                        {
                            var dbRem = _app.FindDatabaseForEntryId(entry.Uuid);
                            removeDatabases.Add(dbRem);
                            dbRem.Entries.Remove(entry.Uuid);
                            pgParent.Entries.Remove(entry);
                            _targetGroup.AddEntry(entry, true, true);
                            addDatabase.Entries.Add(entry.Uuid, entry);
                        }
                        else
                        {
                            PwGroup group = (PwGroup)elementToMove;
                            if ((_targetGroup == group) || (_targetGroup.IsContainedIn(group)))
                            {
                                Finish(false, _app.GetResourceString(UiStringKey.CannotMoveGroupHere));
                                return;
                            }

                            var dbRem = _app.FindDatabaseForEntryId(@group.Uuid);
                            if (dbRem == null)
                            {
                                Finish(false, "Did not find source database. Did you lock it?");
                                return;
                            }

                            dbRem.Groups.Remove(group.Uuid);
                            removeDatabases.Add(dbRem);
                            pgParent.Groups.Remove(group);
                            _targetGroup.AddGroup(group, true, true);
                            addDatabase.Groups.Add(group.Uuid, group);
                        }
                    }

                }

		    }

		    
            
		    
		    //first save the database where we added the elements
            var allDatabasesToSave = new List<Database> {addDatabase};
            //then all databases where we removed elements:
		    removeDatabases.RemoveWhere(db => db == addDatabase);
            allDatabasesToSave.AddRange(removeDatabases);

		    int indexToSave = 0;
		    bool allSavesSuccess = true;
		    void ContinueSave(bool success, string message, Activity activeActivity)
		    {
		        allSavesSuccess &= success;
                indexToSave++;
		        if (indexToSave == allDatabasesToSave.Count)
		        {
		            OnFinishToRun.SetResult(allSavesSuccess);
		            OnFinishToRun.Run();
		            return;
		        }
		        SaveDb saveDb = new SaveDb(_ctx, _app, allDatabasesToSave[indexToSave], new ActionOnFinish(activeActivity, ContinueSave), false);
		        saveDb.SetStatusLogger(StatusLogger);
		        saveDb.ShowDatabaseIocInStatus = allDatabasesToSave.Count > 1;
		        saveDb.Run();
		    }


		    SaveDb save = new SaveDb(_ctx, _app, allDatabasesToSave[0], new ActionOnFinish(ActiveActivity, ContinueSave), false);
            save.SetStatusLogger(StatusLogger);
		    save.ShowDatabaseIocInStatus = allDatabasesToSave.Count > 1;
            save.Run();
		}
	}
}
