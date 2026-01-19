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
  public class MoveElements : OperationWithFinishHandler
  {
    private readonly List<IStructureItem> _elementsToMove;
    private readonly PwGroup _targetGroup;
    private readonly IKp2aApp _app;

    public MoveElements(List<IStructureItem> elementsToMove, PwGroup targetGroup, IKp2aApp app, OnOperationFinishedHandler operationFinishedHandler) : base(app, operationFinishedHandler)
    {
      _elementsToMove = elementsToMove;
      _targetGroup = targetGroup;
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
      Database addDatabase = _app.FindDatabaseForElement(_targetGroup);
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
              var dbRem = _app.FindDatabaseForElement(entry);
              removeDatabases.Add(dbRem);
              dbRem.EntriesById.Remove(entry.Uuid);
              dbRem.Elements.Remove(entry);
              pgParent.Entries.Remove(entry);
              _targetGroup.AddEntry(entry, true, true);
              addDatabase.EntriesById.Add(entry.Uuid, entry);
              addDatabase.Elements.Add(entry);
            }
            else
            {
              PwGroup group = (PwGroup)elementToMove;
              if ((_targetGroup == group) || (_targetGroup.IsContainedIn(group)))
              {
                Finish(false, _app.GetResourceString(UiStringKey.CannotMoveGroupHere));
                return;
              }

              var dbRem = _app.FindDatabaseForElement(@group);
              if (dbRem == null)
              {
                Finish(false, "Did not find source database. Did you lock it?");
                return;
              }

              dbRem.GroupsById.Remove(group.Uuid);
              dbRem.Elements.Remove(group);
              removeDatabases.Add(dbRem);
              pgParent.Groups.Remove(group);
              _targetGroup.AddGroup(group, true, true);
              addDatabase.GroupsById.Add(group.Uuid, group);
              addDatabase.Elements.Add(group);
            }
          }

        }

      }




      //first save the database where we added the elements
      var allDatabasesToSave = new List<Database> { addDatabase };
      //then all databases where we removed elements:
      removeDatabases.RemoveWhere(db => db == addDatabase);
      allDatabasesToSave.AddRange(removeDatabases);

      int indexToSave = 0;
      bool allSavesSuccess = true;
      void ContinueSave(bool success, string message, bool importantMessage, Exception exception, Context activeActivity)
      {
        allSavesSuccess &= success;
        indexToSave++;
        if (indexToSave == allDatabasesToSave.Count)
        {
          operationFinishedHandler.SetResult(allSavesSuccess);
          operationFinishedHandler.Run();
          return;
        }
        SaveDb saveDb = new SaveDb(_app, allDatabasesToSave[indexToSave], new ActionOnOperationFinished(_app, ContinueSave), false, null);
        saveDb.SetStatusLogger(StatusLogger);
        saveDb.ShowDatabaseIocInStatus = allDatabasesToSave.Count > 1;
        saveDb.Run();
      }


      SaveDb save = new SaveDb(_app, allDatabasesToSave[0], new ActionOnOperationFinished(_app, ContinueSave), false, null);
      save.SetStatusLogger(StatusLogger);
      save.ShowDatabaseIocInStatus = allDatabasesToSave.Count > 1;
      save.Run();
    }
  }
}
