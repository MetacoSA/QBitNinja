﻿using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;
using NBitcoin.Indexer;

namespace QBitNinja.ModelBinders
{
    public class BalanceLocatorModelBinder : IModelBinder
    {
        public bool BindModel(
            System.Web.Http.Controllers.HttpActionContext actionContext,
            ModelBindingContext bindingContext)
        {
            if (!typeof(BalanceLocator).IsAssignableFrom(bindingContext.ModelType))
            {
                return false;
            }

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if (val == null)
            {
                return true;
            }

            var key = val.RawValue as string;
            bindingContext.Model = BalanceLocator.Parse(key);
            return true;
        }
    }
}
