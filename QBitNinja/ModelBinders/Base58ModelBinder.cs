using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using System;
using System.Threading.Tasks;

namespace QBitNinja.ModelBinders
{
    public class Base58ModelBinder : IModelBinder
    {
		#region IModelBinder Members

		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
            if (!
                (typeof(Base58Data).IsAssignableFrom(bindingContext.ModelType) ||
                typeof(IDestination).IsAssignableFrom(bindingContext.ModelType)))
            {
				return Task.CompletedTask;
			}

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if (val.FirstValue == null)
            {
				return Task.CompletedTask;
			}

            string key = val.FirstValue;

            var data = Network.Parse(key, bindingContext.ActionContext.HttpContext.RequestServices.GetRequiredService<Network>());
            if (!bindingContext.ModelType.IsInstanceOfType(data))
            {
                throw new FormatException("Invalid base58 type");
            }
            bindingContext.Result = ModelBindingResult.Success(data);
            return Task.CompletedTask;
        }

        #endregion
    }
}
