using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace QBitNinja.ModelBinders
{
	public class BitcoinSerializableModelBinder : IModelBinder
	{
		#region IModelBinder Members

		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
			if(typeof(uint160).IsAssignableFrom(bindingContext.ModelType))
			{
				return new UInt160ModelBinding().BindModelAsync(bindingContext);
			}
			if(typeof(uint256).IsAssignableFrom(bindingContext.ModelType))
			{
				return new UInt256ModelBinding().BindModelAsync(bindingContext);
			}
			if(!typeof(IBitcoinSerializable).IsAssignableFrom(bindingContext.ModelType))
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

			try
			{
				bindingContext.Result = ModelBindingResult.Success(Activator.CreateInstance(bindingContext.ModelType, key));
			}
			catch(TargetInvocationException ex)
			{
				throw ex.InnerException;
			}
			if(bindingContext.Result.Model is uint256 || bindingContext.Result.Model is uint160)
			{
				if(bindingContext.Model.ToString().StartsWith(uint160.Zero.ToString()))
					throw new FormatException("Invalid hash format");
			}
			return Task.CompletedTask;
		}

		#endregion
	}
}
