﻿using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;
using QBitNinja.Models;

namespace QBitNinja.ModelBinders
{
    public class BlockFeatureModelBinder : IModelBinder
    {
        #region IModelBinder Members

        public bool BindModel(
            System.Web.Http.Controllers.HttpActionContext actionContext,
            ModelBindingContext bindingContext)
        {
            if (!typeof(BlockFeature).IsAssignableFrom(bindingContext.ModelType))
            {
                return false;
            }

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if (val == null)
            {
                return true;
            }

            if (!(val.RawValue is string key))
            {
                bindingContext.Model = null;
                return true;
            }

            BlockFeature feature = BlockFeature.Parse(key);
            bindingContext.Model = feature;
            return true;
        }

        #endregion
    }
}
