using System;
using System.Runtime.Serialization;

namespace QBitNinja.Notifications
{
    [DataContract(Name = "TopicDescription", Namespace = "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect")]
    public class TopicCreation : ICreation
    {
        public TopicCreation(string path)
        {
            if (path == null)
                throw new ArgumentNullException("path");
            Path = path;
        }
        internal TopicCreation()
        {

        }
        [DataMember(Name = "DefaultMessageTimeToLive", IsRequired = false, Order = 1002, EmitDefaultValue = false)]
        public  TimeSpan? DefaultMessageTimeToLive
        {
            get;
            set;
        }

        public bool Validate(TopicCreation creation)
        {
            return
                Validate(MaxSizeInMegabytes, creation.MaxSizeInMegabytes) &&
                Validate(RequiresDuplicateDetection, creation.RequiresDuplicateDetection) &&
                Validate(DuplicateDetectionHistoryTimeWindow, creation.DuplicateDetectionHistoryTimeWindow) &&
                Validate(EnableBatchedOperations, creation.EnableBatchedOperations) &&
                Validate(EnableFilteringMessagesBeforePublishing, creation.EnableFilteringMessagesBeforePublishing) &&
                Validate(IsAnonymousAccessible, creation.IsAnonymousAccessible) &&
                Validate(ForwardTo, creation.ForwardTo) &&
                Validate(UserMetadata, creation.UserMetadata) &&
                Validate(SupportOrdering, creation.SupportOrdering) &&
                Validate(AutoDeleteOnIdle, creation.AutoDeleteOnIdle) &&
                Validate(EnablePartitioning, creation.EnablePartitioning) &&
                Validate(EnableExpress, creation.EnableExpress) &&
                Validate(NewPath, creation.NewPath);
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

        [DataMember(Name = "MaxSizeInMegabytes", IsRequired = false, Order = 1004, EmitDefaultValue = false)]
        public  long? MaxSizeInMegabytes
        {
            get;
            set;
        }
        [DataMember(Name = "RequiresDuplicateDetection", IsRequired = false, Order = 1005, EmitDefaultValue = false)]
        public  bool? RequiresDuplicateDetection
        {
            get;
            set;
        }
        [DataMember(Name = "DuplicateDetectionHistoryTimeWindow", IsRequired = false, Order = 1006, EmitDefaultValue = false)]
        public  TimeSpan? DuplicateDetectionHistoryTimeWindow
        {
            get;
            set;
        }
        [DataMember(Name = "EnableBatchedOperations", IsRequired = false, Order = 1007, EmitDefaultValue = false)]
        public  bool? EnableBatchedOperations
        {
            get;
            set;
        }
        [DataMember(Name = "FilteringMessagesBeforePublishing", IsRequired = false, Order = 1014, EmitDefaultValue = false)]
        public  bool? EnableFilteringMessagesBeforePublishing
        {
            get;
            set;
        }
        [DataMember(Name = "IsAnonymousAccessible", IsRequired = false, Order = 1015, EmitDefaultValue = false)]
        public  bool? IsAnonymousAccessible
        {
            get;
            set;
        }
        [DataMember(Name = "ForwardTo", IsRequired = false, Order = 1018, EmitDefaultValue = false)]
        public  string ForwardTo
        {
            get;
            set;
        }
        [DataMember(Name = "UserMetadata", IsRequired = false, Order = 1023, EmitDefaultValue = false)]
        public  string UserMetadata
        {
            get;
            set;
        }
        [DataMember(Name = "SupportOrdering", IsRequired = false, Order = 1024, EmitDefaultValue = false)]
        public  bool? SupportOrdering
        {
            get;
            set;
        }
        [DataMember(Name = "AutoDeleteOnIdle", IsRequired = false, Order = 1027, EmitDefaultValue = false)]
        public  TimeSpan? AutoDeleteOnIdle
        {
            get;
            set;
        }
        [DataMember(Name = "EnablePartitioning", IsRequired = false, Order = 1028, EmitDefaultValue = false)]
        public  bool? EnablePartitioning
        {
            get;
            set;
        }
        [DataMember(Name = "EnableSubscriptionPartitioning", IsRequired = false, Order = 1031, EmitDefaultValue = false)]
        public  bool? EnableSubscriptionPartitioning
        {
            get;
            set;
        }
        [DataMember(Name = "EnableExpress", IsRequired = false, Order = 1032, EmitDefaultValue = false)]
        public  bool? EnableExpress
        {
            get;
            set;
        }
        [DataMember(Name = "NewPath", IsRequired = false, Order = 1034, EmitDefaultValue = false)]
        public  string NewPath
        {
            get;
            set;
        }

        public string Path
        {
            get;
            set;
        }
    }
}
