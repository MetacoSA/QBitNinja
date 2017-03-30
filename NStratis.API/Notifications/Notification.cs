using QBitNinja.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class Notification
    {
        public Subscription Subscription
        {
            get;
            set;
        }

        public NotificationData Data
        {
            get;
            set;
        }

        public override string ToString()
        {
            return Serializer.ToString(this);
        }

        public int Tried
        {
            get;
            set;
        }
    }
}
