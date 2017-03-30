using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class NewBlockSubscription : Subscription
    {
        public NewBlockSubscription()
        {
            Type = SubscriptionType.NewBlock;
        }
    }
}
