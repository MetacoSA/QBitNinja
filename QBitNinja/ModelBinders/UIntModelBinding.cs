using NBitcoin;
using System;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;

namespace QBitNinja.ModelBinders
{
    public class UInt256ModelBinding : IModelBinder
    {
        #region IModelBinder Members

        public bool BindModel(System.Web.Http.Controllers.HttpActionContext actionContext, ModelBindingContext bindingContext)
        {
            if(!typeof(uint256).IsAssignableFrom(bindingContext.ModelType))
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
            if(key == null)
            {
                bindingContext.Model = null;
                return true;
            }
            bindingContext.Model = uint256.Parse(key);
            if(bindingContext.Model.ToString().StartsWith(uint160.Zero.ToString()))
                throw new FormatException("Invalid hash format");
            return true;
        }

        #endregion
    }

    public class UInt160ModelBinding : IModelBinder
    {
        #region IModelBinder Members

        public bool BindModel(System.Web.Http.Controllers.HttpActionContext actionContext, ModelBindingContext bindingContext)
        {
            if(!typeof(uint160).IsAssignableFrom(bindingContext.ModelType))
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
            if(key == null)
            {
                bindingContext.Model = null;
                return true;
            }
            bindingContext.Model = uint160.Parse(key);
            if(bindingContext.Model.ToString().StartsWith(uint160.Zero.ToString()))
                throw new FormatException("Invalid hash format");
            return true;
        }

        #endregion
    }
}
