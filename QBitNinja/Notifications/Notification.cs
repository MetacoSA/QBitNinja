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

        public string ToString(NBitcoin.Network network)
        {
            return Serializer.ToString(this, network);
        }

        public int Tried
        {
            get;
            set;
        }
    }
}
