using System;
using System.Runtime.Serialization;

namespace QBitNinja.Notifications
{
    public interface ICreation
    {
        bool? RequiresDuplicateDetection
        {
            get;
            set;
        }
    }
    [DataContract(Name = "QueueDescription", Namespace = "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect")]
    public class QueueCreation : ICreation
    {
        public QueueCreation()
        {

        }
        public QueueCreation(string path)
        {
            Path = path;
        }
        [DataMember(Name = "LockDuration", IsRequired = false, Order = 1002, EmitDefaultValue = false)]
        public TimeSpan? LockDuration
        {
            get;
            set;
        }
        [DataMember(Name = "MaxSizeInMegabytes", IsRequired = false, Order = 1004, EmitDefaultValue = false)]
        public long? MaxSizeInMegabytes
        {
            get;
            set;
        }
        [DataMember(Name = "RequiresDuplicateDetection", IsRequired = false, Order = 1005, EmitDefaultValue = false)]
        public bool? RequiresDuplicateDetection
        {
            get;
            set;
        }
        [DataMember(Name = "RequiresSession", IsRequired = false, Order = 1006, EmitDefaultValue = false)]
        public bool? RequiresSession
        {
            get;
            set;
        }
        [DataMember(Name = "DefaultMessageTimeToLive", IsRequired = false, Order = 1007, EmitDefaultValue = false)]
        public TimeSpan? DefaultMessageTimeToLive
        {
            get;
            set;
        }
        [DataMember(Name = "DeadLetteringOnMessageExpiration", IsRequired = false, Order = 1008, EmitDefaultValue = false)]
        public bool? EnableDeadLetteringOnMessageExpiration
        {
            get;
            set;
        }
        [DataMember(Name = "DuplicateDetectionHistoryTimeWindow", IsRequired = false, Order = 1009, EmitDefaultValue = false)]
        public TimeSpan? DuplicateDetectionHistoryTimeWindow
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
        
        [DataMember(Name = "IsAnonymousAccessible", IsRequired = false, Order = 1015, EmitDefaultValue = false)]
        public bool? IsAnonymousAccessible
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
        [DataMember(Name = "SupportOrdering", IsRequired = false, Order = 1023, EmitDefaultValue = false)]
        public bool? SupportOrdering
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
        [DataMember(Name = "EnablePartitioning", IsRequired = false, Order = 1026, EmitDefaultValue = false)]
        public bool? EnablePartitioning
        {
            get;
            set;
        }
        [DataMember(Name = "ForwardDeadLetteredMessagesTo", IsRequired = false, Order = 1028, EmitDefaultValue = false)]
        public string ForwardDeadLetteredMessagesTo
        {
            get;
            set;
        }
        [DataMember(Name = "EnableExpress", IsRequired = false, Order = 1029, EmitDefaultValue = false)]
        public bool? EnableExpress
        {
            get;
            set;
        }
        [DataMember(Name = "NewPath", IsRequired = false, Order = 1031, EmitDefaultValue = false)]
        public string NewPath
        {
            get;
            set;
        }

        public string Path
        {
            get;
            set;
        }

        public bool Validate(QueueCreation creation)
        {
            return
                Validate(MaxSizeInMegabytes, creation.MaxSizeInMegabytes) &&
                Validate(RequiresDuplicateDetection, creation.RequiresDuplicateDetection) &&
                Validate(DuplicateDetectionHistoryTimeWindow, creation.DuplicateDetectionHistoryTimeWindow) &&
                Validate(EnableBatchedOperations, creation.EnableBatchedOperations) &&                
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
    }
}
