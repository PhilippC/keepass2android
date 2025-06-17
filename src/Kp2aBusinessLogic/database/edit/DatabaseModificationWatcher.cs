using Java.Lang;

namespace keepass2android;

public interface IDatabaseModificationWatcher
{
    void BeforeModifyDatabases();
    void AfterModifyDatabases();
}

public class NullDatabaseModificationWatcher : IDatabaseModificationWatcher
{
    public void BeforeModifyDatabases() { }
    public void AfterModifyDatabases() { }
}

public class BackgroundDatabaseModificationLocker(IKp2aApp app) : IDatabaseModificationWatcher
{
    public void BeforeModifyDatabases()
    {
        while (true)
        {
            if (app.DatabasesBackgroundModificationLock.TryEnterWriteLock(TimeSpan.FromSeconds(0.1)))
            {
                break;
            }

            if (Java.Lang.Thread.Interrupted())
            {
                throw new InterruptedException();
            }
        }
    }

    public void AfterModifyDatabases()
    {
        app.DatabasesBackgroundModificationLock.ExitWriteLock();
    }
}
