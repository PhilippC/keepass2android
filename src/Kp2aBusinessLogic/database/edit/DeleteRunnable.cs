using Android.Content;
using KeePassLib;

namespace keepass2android
{
	public abstract class DeleteRunnable : RunnableOnFinish
	{
		protected DeleteRunnable(OnFinish finish, IKp2aApp app):base(finish)
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
			bool bShiftPressed = false;
			PwDatabase pd = Db.KpDatabase;
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
			if ((Db == null) || (Db.KpDatabase == null)) { return; }
			
			if(pgRecycleBin == Db.KpDatabase.RootGroup)
			{
				pgRecycleBin = null;
			}
			
			if(pgRecycleBin == null)
			{
				pgRecycleBin = new PwGroup(true, true, App.GetResourceString(UiStringKey.RecycleBin),
				                           PwIcon.TrashBin) 
						{
							EnableAutoType = false, 
							EnableSearching = false, 
							IsExpanded = false
						};

				Db.KpDatabase.RootGroup.AddGroup(pgRecycleBin, true);
				
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
                (dlgSender, dlgEvt) => {	
	                                       DeletePermanently = false;
	                                       ProgressTask pt = new ProgressTask(App, Ctx, this);
	                                       pt.Run();
                },
                (dlgSender, dlgEvt) => {},
                Ctx);


				
			} else
			{
				ProgressTask pt = new ProgressTask(App, Ctx, this);
				pt.Run();
			}
		}

	}
}

