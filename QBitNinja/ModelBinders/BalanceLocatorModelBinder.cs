using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin.Indexer;

namespace QBitNinja.ModelBinders
{
    public class BalanceLocatorModelBinder : IModelBinder
    {
		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
            if (!typeof(BalanceLocator).IsAssignableFrom(bindingContext.ModelType))
            {
				return Task.CompletedTask;
			}

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if (val.FirstValue == null)
            {
				bindingContext.Model = null;
				return Task.CompletedTask;
			}
            string key = val.FirstValue;
            bindingContext.Result = ModelBindingResult.Success(BalanceLocator.Parse(key));
			return Task.CompletedTask;
        }

	}
}
