using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Models
{
    public class RouteModel
    {
        public string Template
        {
            get;
            set;
        }
        public string[] Samples
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
