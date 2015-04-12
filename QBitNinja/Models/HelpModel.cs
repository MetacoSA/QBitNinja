namespace QBitNinja.Models
{
    public class RouteSample
    {
        public RouteSample(string url, string comment)
        {
            Url = url;
            Comment = comment;
        }
        public string Url
        {
            get;
            set;
        }
        public string Comment
        {
            get;
            set;
        }

        public static implicit operator RouteSample(string url)
        {
            return new RouteSample(url, null);
        }

    }
    public class RouteModel
    {
        public string Template
        {
            get;
            set;
        }
        public RouteSample[] Samples
        {
            get;
            set;
        }
    }
    public class HelpModel
    {
        public RouteModel[] Routes
        {
            get;
            set;
        }
    }
}
