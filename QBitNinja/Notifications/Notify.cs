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
