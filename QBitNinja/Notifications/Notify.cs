using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class Notify
    {
        public Notify()
        {

        }
        public Notify(Notification notification)
        {
            Notification = notification;
        }
        public Notification Notification
        {
            get;
            set;
        }
        public bool SendAndForget
        {
            get;
            set;
        }
    }
}
