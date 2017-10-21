using NBitcoin.Indexer;
using QBitNinja.Models;
using QBitNinja.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;

namespace QBitNinja
{
    public static class Extensions
    {
        public static WalletRuleEntry CreateWalletRuleEntry(this WalletAddress address)
        {
            return new WalletRuleEntry(address.WalletName, new ScriptRule()
            {
                CustomData = Serializer.ToString(address),
                ScriptPubKey = address.ScriptPubKey,
                RedeemScript = address.RedeemScript
            });

        }
        public static T MinElement<T>(this IEnumerable<T> input, Func<T, int> predicate)
        {
            int min = int.MaxValue;
            T element = default(T);

            foreach (var el in input)
            {
                var val = predicate(el);
                if (val < min)
                {
                    min = predicate(el);
                    element = el;
                }
            }
            return element;
        }
    }
}
