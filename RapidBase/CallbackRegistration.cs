namespace RapidBase
{
    public class CallbackRegistration
    {
        public CallbackRegistration(string url)
        {
            Url = url;
        }
        public CallbackRegistration()
        {

        }
        public string Id
        {
            get;
            set;
        }
        public string Url
        {
            get;
            set;
        }
    }
}
