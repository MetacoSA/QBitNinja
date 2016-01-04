using NBitcoin;
using System;
using System.Reflection;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;

namespace QBitNinja.ModelBinders
{
    public class BitcoinSerializableModelBinder : IModelBinder
    {
        #region IModelBinder Members

        public bool BindModel(System.Web.Http.Controllers.HttpActionContext actionContext, ModelBindingContext bindingContext)
        {
            if(typeof(uint160).IsAssignableFrom(bindingContext.ModelType))
            {
                return new UInt160ModelBinding().BindModel(actionContext, bindingContext);
            }
            if(typeof(uint256).IsAssignableFrom(bindingContext.ModelType))
            {
                return new UInt256ModelBinding().BindModel(actionContext, bindingContext);
            }
            if (!typeof(IBitcoinSerializable).IsAssignableFrom(bindingContext.ModelType))
            {
                return false;
            }

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if (val == null)
            {
                return false;
            }

            string key = val.RawValue as string;
            if (key == null)
            {
                bindingContext.Model = null;
                return true;
            }

            try
            {
                bindingContext.Model = Activator.CreateInstance(bindingContext.ModelType, key);
            }
            catch(TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
            if (bindingContext.Model is uint256 || bindingContext.Model is uint160)
            {
                if (bindingContext.Model.ToString().StartsWith(uint160.Zero.ToString()))
                    throw new FormatException("Invalid hash format");
            }
            return true;
        }

        #endregion
    }
}
