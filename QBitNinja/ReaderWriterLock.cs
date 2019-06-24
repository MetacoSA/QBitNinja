using System;
using System.Threading;

namespace QBitNinja
{
    internal class ReaderWriterLock
    {
        private readonly ReaderWriterLockSlim @lock = new ReaderWriterLockSlim();

        public IDisposable LockRead()
        {
            return new ActionDisposable(() => @lock.EnterReadLock(), () => @lock.ExitReadLock());
        }

        public IDisposable LockWrite()
        {
            return new ActionDisposable(() => @lock.EnterWriteLock(), () => @lock.ExitWriteLock());
        }

        internal bool TryLockWrite(out IDisposable locked)
        {
            locked = null;
            if (@lock.TryEnterWriteLock(0))
            {
                locked = new ActionDisposable(
                    () => { },
                    () => @lock.ExitWriteLock());
                return true;
            }

            return false;
        }
    }
}
