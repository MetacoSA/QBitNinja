using System;
using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;

namespace QBitNinja.ModelBinders
{
    public class BalanceIdModelBinder : IModelBinder
    {
        public bool BindModel(HttpActionContext actionContext, ModelBindingContext bindingContext)
        {
            if (!typeof(BalanceId).IsAssignableFrom(bindingContext.ModelType))
            {
                return false;
            }

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (val?.RawValue == null)
            {
                return false;
            }

            var key = val.RawValue as string;
            if (key.Length > 3 && key.Length < 5000 && key.StartsWith("0x"))
            {
                bindingContext.Model = new BalanceId(new Script(Encoders.Hex.DecodeData(key.Substring(2))));
                return true;
            }

            if (key.Length > 3 && key.Length < 5000 && key.StartsWith("W-"))
            {
                bindingContext.Model = new BalanceId(key.Substring(2));
                return true;
            }

            IBitcoinString data = Network.Parse(key, actionContext.RequestContext.GetConfiguration().Indexer.Network);
            if (!(data is IDestination))
            {
                throw new FormatException("Invalid base58 type");
            }

            if (data is BitcoinColoredAddress)
            {
                actionContext.Request.Properties["BitcoinColoredAddress"] = true;
            }

            bindingContext.Model = new BalanceId((IDestination)data);
            return true;
        }
    }
}