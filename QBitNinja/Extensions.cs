using System;
using System.Collections.Generic;
using System.Web.Http.Controllers;
using NBitcoin.Indexer;
using QBitNinja.Models;

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

        public static QBitNinjaConfiguration GetConfiguration(this HttpRequestContext ctx)
        {
            return ((QBitNinjaDependencyResolver)ctx.Configuration.DependencyResolver).Get<QBitNinjaConfiguration>();
        }

        public static T MinElement<T>(this IEnumerable<T> input, Func<T, int> predicate)
        {
            int min = int.MaxValue;
            T element = default(T);
            foreach (T el in input)
            {
                int val = predicate(el);
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
