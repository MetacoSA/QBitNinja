using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using System;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;

namespace QBitNinja.ModelBinders
{
	public class BalanceIdModelBinder : IModelBinder
	{
		#region IModelBinder Members

		public bool BindModel(System.Web.Http.Controllers.HttpActionContext actionContext, ModelBindingContext bindingContext)
		{
			if(!typeof(BalanceId).IsAssignableFrom(bindingContext.ModelType))
			{
				return false;
			}

			ValueProviderResult val = bindingContext.ValueProvider.GetValue(
				bindingContext.ModelName);
			if(val == null)
			{
				return false;
			}

			string key = val.RawValue as string;
			if(key.Length > 3 && key.Length < 5000 && key.StartsWith("0x"))
			{
				bindingContext.Model = new BalanceId(new Script(Encoders.Hex.DecodeData(key.Substring(2))));
				return true;
			}
			var data = Network.CreateFromBase58Data(key, actionContext.RequestContext.GetConfiguration().Indexer.Network);
			if(!(data is IDestination))
			{
				throw new FormatException("Invalid base58 type");
			}
			if(data is BitcoinColoredAddress)
			{
				actionContext.Request.Properties["BitcoinColoredAddress"] = true;
			}
			bindingContext.Model = new BalanceId((IDestination)data);
			return true;
		}

		#endregion
	}
}
