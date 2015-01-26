using NBitcoin;
using System;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;

namespace RapidBase.ModelBinders
{
    public class Base58ModelBinder : IModelBinder
    {
        #region IModelBinder Members

        public bool BindModel(System.Web.Http.Controllers.HttpActionContext actionContext, ModelBindingContext bindingContext)
        {
            if (!typeof(Base58Data).IsAssignableFrom(bindingContext.ModelType))
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

            var data = Base58Data.GetFromBase58Data(key);
            if (!bindingContext.ModelType.IsAssignableFrom(data.GetType()))
            {
                throw new FormatException("Invalid base58 type");
            }
            bindingContext.Model = data;
            return true;
        }

        #endregion
    }
}
