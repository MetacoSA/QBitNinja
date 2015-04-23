using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Models
{
    public class NewBlockSubscription
    {
        public string Id
        {
            get;
            set;
        }
        public string Type
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
