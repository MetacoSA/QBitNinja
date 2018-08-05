using System;
using System.Runtime.Serialization;

namespace QBitNinja.Notifications
{
    [DataContract(Name = "SubscriptionDescription", Namespace = "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect")]
    public class SubscriptionCreation : ICreation
    {
        public string Name
        {
            get;
            set;
        }
        public string TopicPath
        {
            get;
            set;
        }

        public bool Validate(SubscriptionCreation creation)
        {
            return
                Validate(LockDuration, creation.LockDuration) &&
                Validate(RequiresSession, creation.RequiresSession) &&
                Validate(DefaultMessageTimeToLive, creation.DefaultMessageTimeToLive) &&
                Validate(EnableDeadLetteringOnMessageExpiration, creation.EnableDeadLetteringOnMessageExpiration) &&
                Validate(EnableDeadLetteringOnFilterEvaluationExceptions, creation.EnableDeadLetteringOnFilterEvaluationExceptions) &&
                Validate(MaxDeliveryCount, creation.MaxDeliveryCount) &&
                Validate(EnableBatchedOperations, creation.EnableBatchedOperations) &&
                Validate(ForwardTo, creation.ForwardTo) &&
                Validate(UserMetadata, creation.UserMetadata) &&
                Validate(ForwardDeadLetteredMessagesTo, creation.ForwardDeadLetteredMessagesTo) &&
                Validate(AutoDeleteOnIdle, creation.AutoDeleteOnIdle);

        }

        internal void Merge(SubscriptionCreation subscription)
        {
            if (subscription == null)
                return;

            if (LockDuration == null)
                LockDuration = subscription.LockDuration;
            if (RequiresSession == null)
                RequiresSession = subscription.RequiresSession;
            if (DefaultMessageTimeToLive == null)
                DefaultMessageTimeToLive = subscription.DefaultMessageTimeToLive;
            if (EnableDeadLetteringOnMessageExpiration == null)
                EnableDeadLetteringOnMessageExpiration = subscription.EnableDeadLetteringOnMessageExpiration;
            if (EnableDeadLetteringOnFilterEvaluationExceptions == null)
                EnableDeadLetteringOnFilterEvaluationExceptions = subscription.EnableDeadLetteringOnFilterEvaluationExceptions;
            if (MaxDeliveryCount == null)
                MaxDeliveryCount = subscription.MaxDeliveryCount;
            if (EnableBatchedOperations == null)
                EnableBatchedOperations = subscription.EnableBatchedOperations;
            if (ForwardTo == null)
                ForwardTo = subscription.ForwardTo;
            if (UserMetadata == null)
                UserMetadata = subscription.UserMetadata;
            if (ForwardDeadLetteredMessagesTo == null)
                ForwardDeadLetteredMessagesTo = subscription.ForwardDeadLetteredMessagesTo;
            if (AutoDeleteOnIdle == null)
                AutoDeleteOnIdle = subscription.AutoDeleteOnIdle;
        }

        private bool Validate(string a, string b)
        {
            if (a == null)
                return true;
            if (b == null)
                return false;
            return a.Equals(b);
        }

        private bool Validate<T>(T? a, T? b) where T : struct
        {
            if (!a.HasValue)
                return true;
            if (!b.HasValue)
                return false;
            return a.Value.Equals(b.Value);
        }
        [DataMember(Name = "LockDuration", IsRequired = false, Order = 1002, EmitDefaultValue = false)]
        public TimeSpan? LockDuration
        {
            get;
            set;
        }
        [DataMember(Name = "RequiresSession", IsRequired = false, Order = 1003, EmitDefaultValue = false)]
        public bool? RequiresSession
        {
            get;
            set;
        }
        [DataMember(Name = "DefaultMessageTimeToLive", IsRequired = false, Order = 1004, EmitDefaultValue = false)]
        public TimeSpan? DefaultMessageTimeToLive
        {
            get;
            set;
        }
        [DataMember(Name = "DeadLetteringOnMessageExpiration", IsRequired = false, Order = 1005, EmitDefaultValue = false)]
        public bool? EnableDeadLetteringOnMessageExpiration
        {
            get;
            set;
        }
        [DataMember(Name = "DeadLetteringOnFilterEvaluationExceptions", IsRequired = false, Order = 1006, EmitDefaultValue = false)]
        public bool? EnableDeadLetteringOnFilterEvaluationExceptions
        {
            get;
            set;
        }
        [DataMember(Name = "MaxDeliveryCount", IsRequired = false, Order = 1010, EmitDefaultValue = false)]
        public int? MaxDeliveryCount
        {
            get;
            set;
        }
        [DataMember(Name = "EnableBatchedOperations", IsRequired = false, Order = 1011, EmitDefaultValue = false)]
        public bool? EnableBatchedOperations
        {
            get;
            set;
        }
        [DataMember(Name = "ForwardTo", IsRequired = false, Order = 1018, EmitDefaultValue = false)]
        public string ForwardTo
        {
            get;
            set;
        }
        [DataMember(Name = "UserMetadata", IsRequired = false, Order = 1022, EmitDefaultValue = false)]
        public string UserMetadata
        {
            get;
            set;
        }
        [DataMember(Name = "ForwardDeadLetteredMessagesTo", IsRequired = false, Order = 1024, EmitDefaultValue = false)]
        public string ForwardDeadLetteredMessagesTo
        {
            get;
            set;
        }
        [DataMember(Name = "AutoDeleteOnIdle", IsRequired = false, Order = 1025, EmitDefaultValue = false)]
        public TimeSpan? AutoDeleteOnIdle
        {
            get;
            set;
        }



        #region ICreation Members

        bool? ICreation.RequiresDuplicateDetection
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        #endregion
    }
}
