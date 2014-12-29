using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;

namespace RapidBase.ModelBinders
{
    public class BitcoinSerializableModelBinder : IModelBinder
    {
        #region IModelBinder Members

        public bool BindModel(System.Web.Http.Controllers.HttpActionContext actionContext, ModelBindingContext bindingContext)
        {
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

            bindingContext.Model = Activator.CreateInstance(bindingContext.ModelType, key);
            if (bindingContext.Model is uint256 || bindingContext.Model is uint160)
            {
                if (bindingContext.Model.ToString().StartsWith(new uint160("0").ToString()))
                    throw new FormatException("Invalid Hex String");
            }
            return true;
        }

        #endregion
    }
}
