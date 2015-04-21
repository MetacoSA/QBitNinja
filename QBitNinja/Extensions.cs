using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using NBitcoin.Indexer;
using QBitNinja.Models;
using QBitNinja.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Xml;

namespace QBitNinja
{
    public static class Extensions
    {
        public static WalletRuleEntry CreateWalletRuleEntry(this WalletAddress address)
        {
            return new WalletRuleEntry(address.WalletName, new ScriptRule()
            {
                CustomData = address.AdditionalInformation.ToString(),
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

        public static async Task<TopicDescription> EnsureTopicExistAsync(this NamespaceManager ns, TopicCreation topic)
        {
            var create = ChangeType<TopicCreation, TopicDescription>(topic);
            create.Path = topic.Path;

            TopicDescription result = null;
            try
            {
                result = await ns.GetTopicAsync(topic.Path).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
            }
            if (result == null)
            {                
                result = await ns.CreateTopicAsync(create).ConfigureAwait(false);
            }
            if (!topic.Validate(ChangeType<TopicDescription,TopicCreation>(result)))
            {
                await ns.DeleteTopicAsync(topic.Path).ConfigureAwait(false);
                return await EnsureTopicExistAsync(ns, topic).ConfigureAwait(false);
            }
            return result;
        }

        public static Task<TopicDescription> EnsureTopicExistAsync(this NamespaceManager ns, string topicName)
        {
            return EnsureTopicExistAsync(ns, new TopicCreation()
            {
                Path = topicName
            });
        }

        public static async Task<SubscriptionDescription> EnsureSubscriptionExistsAsync(this NamespaceManager ns, SubscriptionCreation subscription)
        {
            var create = ChangeType<SubscriptionCreation, SubscriptionDescription>(subscription);
            create.TopicPath = subscription.TopicPath;
            create.Name = subscription.Name;

            SubscriptionDescription result = null;
            try
            {
                result = await ns.GetSubscriptionAsync(subscription.TopicPath, subscription.Name).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
            }
            if (result == null)
            {                
                result = await ns.CreateSubscriptionAsync(create).ConfigureAwait(false);
            }
            if (!subscription.Validate(ChangeType<SubscriptionDescription,SubscriptionCreation>(result)))
            {
                await ns.DeleteSubscriptionAsync(subscription.TopicPath, subscription.Name).ConfigureAwait(false);
                return await EnsureSubscriptionExistsAsync(ns, subscription).ConfigureAwait(false);
            }
            return result;
        }

        private static T2 ChangeType<T1, T2>(T1 input)
        {
            MemoryStream ms = new MemoryStream();
            DataContractSerializer seria = new DataContractSerializer(typeof(T1));
            seria.WriteObject(ms, input);
            ms.Position = 0;
            seria = new DataContractSerializer(typeof(T2));
            return (T2)seria.ReadObject(ms);
        }

        public static  Task<SubscriptionDescription> EnsureSubscriptionExistsAsync(this NamespaceManager ns, string topic, string subscriptionName)
        {
            return EnsureSubscriptionExistsAsync(ns, new SubscriptionCreation()
            {
                TopicPath = topic,
                Name = subscriptionName
            });
        }
    }
}
