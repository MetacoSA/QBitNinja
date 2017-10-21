using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace QBitNinja.ModelBinders
{
    public class UInt256ModelBinding : IModelBinder
    {
		#region IModelBinder Members

		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
            if(!typeof(uint256).IsAssignableFrom(bindingContext.ModelType))
            {
                return Task.CompletedTask;
            }

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);

            string key = val.FirstValue;
            if(key == null)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }
            bindingContext.Result = ModelBindingResult.Success(uint256.Parse(key));
            if(bindingContext.Model.ToString().StartsWith(uint160.Zero.ToString()))
                throw new FormatException("Invalid hash format");
			return Task.CompletedTask;
		}

        #endregion
    }

    public class UInt160ModelBinding : IModelBinder
    {
		#region IModelBinder Members

		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
            if(!typeof(uint160).IsAssignableFrom(bindingContext.ModelType))
            {
                return Task.CompletedTask;
            }

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            
            string key = val.FirstValue;
            if(key == null)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }
            bindingContext.Result =	ModelBindingResult.Success(uint160.Parse(key));
            if(bindingContext.Model.ToString().StartsWith(uint160.Zero.ToString()))
                throw new FormatException("Invalid hash format");
            return Task.CompletedTask;
        }

        #endregion
    }
}
