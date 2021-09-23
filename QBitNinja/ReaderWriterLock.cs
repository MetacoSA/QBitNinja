using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QBitNinja
{
    /// <summary>
    /// Wrapper around ReaderWriterLockSlim, offering a scoped-behavior pattern where the lock
    /// is exited at the end of a using statement block.
    /// </summary>
    internal class ReaderWriterLock
    {
        ReaderWriterLockSlim @lock = new ReaderWriterLockSlim();

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
            if(this.@lock.TryEnterWriteLock(0))
            {
                locked = new ActionDisposable(() =>
                {
                }, () => this.@lock.ExitWriteLock());
                return true;
            }
            return false;
        }
    }
}
