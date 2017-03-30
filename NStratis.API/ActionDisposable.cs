using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja
{
    public class ActionDisposable : IDisposable
    {
        Action _Act;
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
