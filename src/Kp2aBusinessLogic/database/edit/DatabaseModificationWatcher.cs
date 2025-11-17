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
