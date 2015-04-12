using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace QBitNinja
{
    public static class Extensions
    {
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

        public static async Task<TopicDescription> EnsureTopicExistAsync(this NamespaceManager ns, string topicName)
        {
            try
            {
                return await ns.GetTopicAsync(topicName).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {

            }
            return await ns.CreateTopicAsync(new TopicDescription(topicName)
            {
                DefaultMessageTimeToLive = TimeSpan.FromMinutes(5),
                AutoDeleteOnIdle = TimeSpan.FromMinutes(5)
            }).ConfigureAwait(false);
        }

        public static async Task<SubscriptionDescription> EnsureSubscriptionExistsAsync(this NamespaceManager ns, string topic, string subscriptionName)
        {
            try
            {
                return await ns.GetSubscriptionAsync(topic, subscriptionName).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
            }
            return await ns.CreateSubscriptionAsync(topic, subscriptionName).ConfigureAwait(false);
        }
    }
}
