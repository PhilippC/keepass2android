
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
		public DeleteRunnable(OnFinish finish):base(finish)
		{
		}

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
				pgRecycleBin = new PwGroup(true, true, mCtx.GetString(Resource.String.RecycleBin),
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

		protected abstract int QuestionsResourceId
		{
			get;
		}
		
		public void start()
		{
			if (CanRecycle)
			{
				AlertDialog.Builder builder = new AlertDialog.Builder(mCtx);
				builder.SetTitle(mCtx.GetString(Resource.String.AskDeletePermanently_title));
				
				builder.SetMessage(mCtx.GetString(QuestionsResourceId));
				
				builder.SetPositiveButton(Resource.String.yes, new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => 
				                                                                                      {
					DeletePermanently = true;
					ProgressTask pt = new ProgressTask(mCtx, this, Resource.String.saving_database);
					pt.run();
				}));
				
				builder.SetNegativeButton(Resource.String.no, new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => {	
					DeletePermanently = false;
					ProgressTask pt = new ProgressTask(mCtx, this, Resource.String.saving_database);
					pt.run();
				}));
				
				builder.SetNeutralButton(mCtx.GetString(Android.Resource.String.Cancel), 
				                         new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => {}));
				
				Dialog dialog = builder.Create();
				dialog.Show();
				
				
			} else
			{
				ProgressTask pt = new ProgressTask(mCtx, this, Resource.String.saving_database);
				pt.run();
			}
		}

	}
}

