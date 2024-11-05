namespace keepass2android
{
    public interface ILockCloseActivity
    {
        void OnLockDatabase(bool lockedByTimeout);
    }
}