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

            BlockFeature feature = new BlockFeature();
            uint height;
            if (key.Equals("last", StringComparison.OrdinalIgnoreCase) || key.Equals("tip", StringComparison.OrdinalIgnoreCase))
            {
                feature.Special = SpecialFeature.Last;
                bindingContext.Model = feature;
            }
            else if (uint.TryParse(key, out height))
            {
                feature.Height = (int)height;
                bindingContext.Model = feature;
            }
            else
            {
                if (key.Length == 0x40 && key.All(c => HexEncoder.IsDigit(c) != -1))
                {
                    feature.BlockId = new uint256(key);
                    bindingContext.Model = feature;
                }
                else
                    throw new FormatException("Invalid block feature, expecting block height or hash");
            }
            return true;
        }

        #endregion
    }
}
