using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.JsonConverters
{
    public class CoinJsonConverter : JsonConverter
    {
        public class CoinJson
        {
            public CoinJson()
            {

            }
            public CoinJson(Coin coin)
            {
                TransactionId = coin.Outpoint.Hash;
                Index = coin.Outpoint.N;
                ScriptPubKey = coin.ScriptPubKey;
                Value = coin.Amount;
                if (coin is ScriptCoin)
                {
                    RedeemScript = ((ScriptCoin)coin).Redeem;
                }
            }
            public Coin ToCoin()
            {
                if (RedeemScript == null)
                    return new Coin(new OutPoint(TransactionId, Index), new TxOut(Value, ScriptPubKey));
                else
                    return new ScriptCoin(new OutPoint(TransactionId, Index), new TxOut(Value, ScriptPubKey), RedeemScript);
            }
            public uint256 TransactionId
            {
                get;
                set;
            }
            public uint Index
            {
                get;
                set;
            }
            public Money Value
            {
                get;
                set;
            }

            public Script ScriptPubKey
            {
                get;
                set;
            }

            public Script RedeemScript
            {
                get;
                set;
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(Coin).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            return serializer.Deserialize<CoinJson>(reader).ToCoin();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, new CoinJson((Coin)value));
        }
    }
}
