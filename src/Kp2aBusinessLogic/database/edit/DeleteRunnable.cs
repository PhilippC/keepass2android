using System;
using System.Collections.Generic;
using Android.Content;
using KeePassLib;

namespace keepass2android
{
	public abstract class DeleteRunnable : RunnableOnFinish
	{
		protected DeleteRunnable(OnFinish finish, IKp2aApp app)
			: base(finish)
		{
			App = app;
		}

		protected IKp2aApp App;

		protected Database Db;

		protected Context Ctx;

		protected void SetMembers(Context ctx, Database db)
		{
			Ctx = ctx;
			Db = db;
		}


		private bool _deletePermanently = true;

		public bool DeletePermanently
		{
			get
			{
				return _deletePermanently;
			}
			set
			{
				_deletePermanently = value;
			}
		}

		public abstract bool CanRecycle
		{
			get;
		}

		protected bool CanRecycleGroup(PwGroup pgParent)
		{
			PwDatabase pd = Db.KpDatabase;
			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);
			bool bPermanent = false;
			if (pgParent != null)
			{
				if (pd.RecycleBinEnabled == false)
				{
					Android.Util.Log.Debug("KP2A", "CanRecycle? No, RecycleBinIsNotEnabled");
					bPermanent = true;
				}
					
				else if (pgRecycleBin == null)
				{
				} // Recycle
				else if (pgParent == pgRecycleBin)
				{
					Android.Util.Log.Debug("KP2A", "CanRecycle? No, Can't recycle RecycleBin");
					bPermanent = true;
				}
					
				else if (pgParent.IsContainedIn(pgRecycleBin))
				{
					Android.Util.Log.Debug("KP2A", "CanRecycle? No, "+pgParent.Name+" is in RecycleBin");
					bPermanent = true;
				}
					
			}
			return !bPermanent;
		}


		protected void EnsureRecycleBinExists(ref PwGroup pgRecycleBin,
											ref bool bGroupListUpdateRequired)
		{
			if ((Db == null) || (Db.KpDatabase == null)) { return; }

			if (pgRecycleBin == Db.KpDatabase.RootGroup)
			{
				pgRecycleBin = null;
			}

			if (pgRecycleBin == null)
			{
				pgRecycleBin = new PwGroup(true, true, App.GetResourceString(UiStringKey.RecycleBin),
										   PwIcon.TrashBin)
						{
							EnableAutoType = false,
							EnableSearching = false,
							IsExpanded = false
						};

				Db.KpDatabase.RootGroup.AddGroup(pgRecycleBin, true);
				Db.Groups[pgRecycleBin.Uuid] = pgRecycleBin;
				Db.KpDatabase.RecycleBinUuid = pgRecycleBin.Uuid;

				bGroupListUpdateRequired = true;
			}
			else { System.Diagnostics.Debug.Assert(pgRecycleBin.Uuid.Equals(Db.KpDatabase.RecycleBinUuid)); }
		}

		protected abstract UiStringKey QuestionsResourceId
		{
			get;
		}

		public void Start()
		{
			if (CanRecycle)
			{
				App.AskYesNoCancel(UiStringKey.AskDeletePermanently_title,
					QuestionsResourceId,
					(dlgSender, dlgEvt) =>
					{
						DeletePermanently = true;
						ProgressTask pt = new ProgressTask(App, Ctx, this);
						pt.Run();
					},
				(dlgSender, dlgEvt) =>
				{
					DeletePermanently = false;
					ProgressTask pt = new ProgressTask(App, Ctx, this);
					pt.Run();
				},
				(dlgSender, dlgEvt) => { },
				Ctx);



			}
			else
			{
				ProgressTask pt = new ProgressTask(App, Ctx, this);
				pt.Run();
			}
		}

		protected void DoDeleteEntry(PwEntry pe, List<PwGroup> touchedGroups)
		{
			PwDatabase pd = Db.KpDatabase;

			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);

			bool bUpdateGroupList = false;
			DateTime dtNow = DateTime.Now;

			PwGroup pgParent = pe.ParentGroup;
			if (pgParent != null)
			{
				pgParent.Entries.Remove(pe);
				//TODO check if RecycleBin is deleted
				//TODO no recycle bin in KDB

				if ((DeletePermanently) || (!CanRecycle))
				{
					PwDeletedObject pdo = new PwDeletedObject(pe.Uuid, dtNow);
					pd.DeletedObjects.Add(pdo);
					touchedGroups.Add(pgParent);
				}
				else // Recycle
				{
					EnsureRecycleBinExists(ref pgRecycleBin, ref bUpdateGroupList);

					pgRecycleBin.AddEntry(pe, true, true);
					pe.Touch(false);

					touchedGroups.Add(pgParent);
					// Mark new parent dirty
					touchedGroups.Add(pgRecycleBin);
					// mark root dirty if recycle bin was created
					touchedGroups.Add(Db.Root);
				}
			}
		}


		public override void Run()
		{
			StatusLogger.UpdateMessage(StatusMessage);

			List<PwGroup> touchedGroups = new List<PwGroup>();
			List<PwGroup> permanentlyDeletedGroups = new List<PwGroup>();
			Android.Util.Log.Debug("KP2A", "Calling PerformDelete..");
			PerformDelete(touchedGroups, permanentlyDeletedGroups);

			_onFinishToRun = new ActionOnFinish((success, message) =>
			{
				if (success)
				{
					foreach (var g in touchedGroups)
						Db.Dirty.Add(g);
					foreach (var g in permanentlyDeletedGroups)
					{
						//remove groups from global lists if present there
						Db.Dirty.Remove(g);
						Db.Groups.Remove(g.Uuid);
					}

				}
				else
				{
					// Let's not bother recovering from a failure to save.  It is too much work.
					App.LockDatabase(false);
				}
			}, OnFinishToRun);

			// Commit database
			SaveDb save = new SaveDb(Ctx, App, OnFinishToRun, false);
			save.SetStatusLogger(StatusLogger);
			save.Run();


		}

		protected abstract void PerformDelete(List<PwGroup> touchedGroups, List<PwGroup> permanentlyDeletedGroups);

		public abstract UiStringKey StatusMessage { get; }

		protected bool DoDeleteGroup(PwGroup pg, List<PwGroup> touchedGroups, List<PwGroup> permanentlyDeletedGroups)
		{
			PwGroup pgParent = pg.ParentGroup;
			if (pgParent == null) return false;

			PwDatabase pd = Db.KpDatabase;
			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);

			pgParent.Groups.Remove(pg);
			touchedGroups.Add(pgParent);
			if ((DeletePermanently) || (!CanRecycle))
			{
				pg.DeleteAllObjects(pd);

				PwDeletedObject pdo = new PwDeletedObject(pg.Uuid, DateTime.Now);
				pd.DeletedObjects.Add(pdo);


				permanentlyDeletedGroups.Add(pg);

			}
			else // Recycle
			{
				bool groupListUpdateRequired = false;
				EnsureRecycleBinExists(ref pgRecycleBin, ref groupListUpdateRequired);

				pgRecycleBin.AddGroup(pg, true, true);
				pg.Touch(false);
				// Mark new parent (Recycle bin) touched
				touchedGroups.Add(pg.ParentGroup);
				// mark root touched if recycle bin was created
				if (groupListUpdateRequired)
					touchedGroups.Add(Db.Root);
			}
			return true;
		}
	}
}

