using System;

namespace QBitNinja
{
    public class ActionDisposable : IDisposable
    {
        private Action _Act;

        public ActionDisposable(Action act)
        {
            _Act = act;
        }

        public ActionDisposable(Action start, Action act)
        {
            start();
            _Act = act;
        }

        #region IDisposable Members

        public void Dispose()
        {
            _Act();
        }

        #endregion
    }
}
