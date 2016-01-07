#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public enum DataFormat
    {
        Json,
        Raw
    }
}
