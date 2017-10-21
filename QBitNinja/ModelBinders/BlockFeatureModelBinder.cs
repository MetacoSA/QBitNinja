using Microsoft.AspNetCore.Mvc.ModelBinding;
using QBitNinja.Models;
using System.Threading.Tasks;

namespace QBitNinja.ModelBinders
{
    public class BlockFeatureModelBinder : IModelBinder
    {
		#region IModelBinder Members

		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
            if (!typeof(BlockFeature).IsAssignableFrom(bindingContext.ModelType))
            {
				return Task.CompletedTask;
			}

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if (val.FirstValue == null)
            {
				bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }

            string key = val.FirstValue as string;
            if (key == null)
            {
                bindingContext.Model = null;
                return Task.CompletedTask;
            }

            BlockFeature feature = BlockFeature.Parse(key);
            bindingContext.Model = feature;
            return Task.CompletedTask;
        }

        #endregion
    }
}
