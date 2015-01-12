using NBitcoin;
using Newtonsoft.Json;
using System;

namespace RapidBase.JsonConverters
{
    public class CoinJsonConverter : JsonConverter
    {
        public class CoinJson
        {
            public CoinJson()
            {

            }
            public CoinJson(ICoin coin)
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
                return RedeemScript == null ? new Coin(new OutPoint(TransactionId, Index), new TxOut(Value, ScriptPubKey)) : new ScriptCoin(new OutPoint(TransactionId, Index), new TxOut(Value, ScriptPubKey), RedeemScript);
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
            return reader.TokenType == JsonToken.Null ? null : serializer.Deserialize<CoinJson>(reader).ToCoin();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, new CoinJson((Coin)value));
        }
    }
}
