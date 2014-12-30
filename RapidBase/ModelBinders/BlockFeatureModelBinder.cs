using NBitcoin;
using NBitcoin.DataEncoders;
using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;

namespace RapidBase.ModelBinders
{
    public class BlockFeatureModelBinder : IModelBinder
    {
        #region IModelBinder Members

        public bool BindModel(System.Web.Http.Controllers.HttpActionContext actionContext, ModelBindingContext bindingContext)
        {
            if (!typeof(BlockFeature).IsAssignableFrom(bindingContext.ModelType))
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

            BlockFeature feature = BlockFeature.Parse(key);
            bindingContext.Model = feature;
            return true;
        }

        #endregion
    }
}
