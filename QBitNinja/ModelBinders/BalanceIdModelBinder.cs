using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using System;
using System.Threading.Tasks;

namespace QBitNinja.ModelBinders
{
	public class BalanceIdModelBinder : IModelBinder
	{
		#region IModelBinder Members

		public async Task BindModelAsync(ModelBindingContext bindingContext)
		{
			if(!typeof(BalanceId).IsAssignableFrom(bindingContext.ModelType))
			{
				return;
			}

			ValueProviderResult val = bindingContext.ValueProvider.GetValue(
				bindingContext.ModelName);
			if(val.FirstValue == null)
			{
				return;
			}

			string key = val.FirstValue;
			if(key.Length > 3 && key.Length < 5000 && key.StartsWith("0x"))
			{
				bindingContext.Result = ModelBindingResult.Success(new BalanceId(new Script(Encoders.Hex.DecodeData(key.Substring(2)))));
				return;
			}
			if(key.Length > 3 && key.Length < 5000 && key.StartsWith("W-"))
			{
				bindingContext.Result = ModelBindingResult.Success(new BalanceId(key.Substring(2)));
				return;
			}
			var data = Network.Parse(key, bindingContext.ActionContext.HttpContext.RequestServices.GetRequiredService<Network>());
			if(!(data is IDestination))
			{
				throw new FormatException("Invalid base58 type");
			}
			if(data is BitcoinColoredAddress)
			{
				bindingContext.ActionContext.HttpContext.Items["BitcoinColoredAddress"] = true;
			}
			bindingContext.Result = ModelBindingResult.Success(new BalanceId((IDestination)data));
		}

		#endregion
	}
}
