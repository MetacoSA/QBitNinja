using NBitcoin.Crypto;
using System;
using System.Text;

namespace RapidBase
{
    public class CallbackRepository
    {
        public CallbackRepository(CrudTable<CallbackRegistration> table)
        {
            if (table == null)
                throw new ArgumentNullException("table");
            _table = table;
        }

        readonly CrudTable<CallbackRegistration> _table;

        public CallbackRegistration CreateCallback(string eventName, CallbackRegistration callback)
        {
            callback.Id = null;
            var callbackStr = Serializer.ToString(callback);
            var id = Hash(callbackStr);
            callback.Id = id;
            _table.Create(eventName, id, callback);
            return callback;
        }

        private static string Hash(string data)
        {
            return Hashes.Hash256(Encoding.UTF8.GetBytes(data)).ToString();
        }

        public CallbackRegistration[] GetCallbacks(string eventName)
        {
            return _table.Read(eventName);
        }

        public void Delete(string eventName, string id)
        {
            _table.Delete(eventName, id);
        }
    }
}
