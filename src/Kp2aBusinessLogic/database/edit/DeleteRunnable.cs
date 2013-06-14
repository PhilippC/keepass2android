
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;

namespace keepass2android
{
	public abstract class DeleteRunnable : RunnableOnFinish
	{
		public DeleteRunnable(OnFinish finish, IKp2aApp app):base(finish)
		{
            mApp = app;
		}

        protected IKp2aApp mApp;

		protected Database mDb;

		protected Context mCtx;

		protected void setMembers(Context ctx, Database db)
		{
			mCtx = ctx;
			mDb = db;
		}

		
		private bool mDeletePermanently = true;
		
		public bool DeletePermanently
		{
			get
			{
				return mDeletePermanently;
			}
			set
			{
				mDeletePermanently = value;
			}
		}

		public abstract bool CanRecycle
		{
			get;
		}

		protected bool CanRecycleGroup(PwGroup pgParent)
		{
			bool bShiftPressed = false;
			PwDatabase pd = mDb.pm;
			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);
			bool bPermanent = false;
			if (pgParent != null)
			{
				if (pd.RecycleBinEnabled == false)
					bPermanent = true;
				else if (bShiftPressed)
					bPermanent = true;
				else if (pgRecycleBin == null)
				{
				} // Recycle
				else if (pgParent == pgRecycleBin)
					bPermanent = true;
				else if (pgParent.IsContainedIn(pgRecycleBin))
					bPermanent = true;
			}
			return !bPermanent;
		}

		
		protected void EnsureRecycleBin(ref PwGroup pgRecycleBin,
		                                    ref bool bGroupListUpdateRequired)
		{
			if ((mDb == null) || (mDb.pm == null)) { return; }
			
			if(pgRecycleBin == mDb.pm.RootGroup)
			{
				pgRecycleBin = null;
			}
			
			if(pgRecycleBin == null)
			{
				pgRecycleBin = new PwGroup(true, true, mApp.GetResourceString(UiStringKey.RecycleBin),
				                           PwIcon.TrashBin);
				pgRecycleBin.EnableAutoType = false;
				pgRecycleBin.EnableSearching = false;
				pgRecycleBin.IsExpanded = false;
				mDb.pm.RootGroup.AddGroup(pgRecycleBin, true);
				
				mDb.pm.RecycleBinUuid = pgRecycleBin.Uuid;
				
				bGroupListUpdateRequired = true;
			}
			else { System.Diagnostics.Debug.Assert(pgRecycleBin.Uuid.EqualsValue(mDb.pm.RecycleBinUuid)); }
		}

		protected abstract UiStringKey QuestionsResourceId
		{
			get;
		}
		
		public void start()
		{
			if (CanRecycle)
			{
                mApp.AskYesNoCancel(UiStringKey.AskDeletePermanently_title, 
                    QuestionsResourceId,
                    new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => 
				                                                                                      {
					DeletePermanently = true;
                    ProgressTask pt = new ProgressTask(mApp, mCtx, this, UiStringKey.saving_database);
					pt.run();
				}),
                new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => {	
					DeletePermanently = false;
                    ProgressTask pt = new ProgressTask(mApp, mCtx, this, UiStringKey.saving_database);
					pt.run();
				}),
                new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => {}),
                mCtx);


				
			} else
			{
				ProgressTask pt = new ProgressTask(mApp, mCtx, this, UiStringKey.saving_database);
				pt.run();
			}
		}

	}
}

