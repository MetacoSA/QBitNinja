using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json.Linq;
using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RapidBase.Tests
{
    public class Class1
    {
        [Fact]
        public void CanGetTransaction()
        {
            using (var tester = ServerTester.Create())
            {
                var bob = new Key();
                var alice = new Key();

                //Not found should return 404 (Not found)
                var txId = new uint256(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)));
                AssetEx.HttpError(404, () => tester.SendGet<GetTransactionResponse>("transactions/" + txId));
                ////

                //Not correctly formatted should return 400 (Bad request)
                AssetEx.HttpError(400, () => tester.SendGet<GetTransactionResponse>("transactions/000lol"));
                ////

                //Found should return the correct transaction
                var tx = tester.ChainBuilder.EmitMoney("1.0", bob);
                txId = tx.GetHash();
                var response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
                Assert.NotNull(response);
                Assert.Equal(txId.ToString(), response.TransactionId.ToString());
                Assert.Equal(tx.ToString(), response.Transaction.ToString());
                ////

                //Previously spent coins should be in the response
                var prevTx = tx;
                TransactionBuilder txBuilder = new TransactionBuilder();
                tx =
                    txBuilder
                    .AddKeys(bob)
                    .AddCoins(new Coin(tx, 0))
                    .Send(alice, "0.99")
                    .SendFees("0.01")
                    .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);
                txId = tx.GetHash();
                response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
                Assert.Equal(txId.ToString(), response.TransactionId.ToString());
                Assert.Equal(tx.ToString(), response.Transaction.ToString());
                Assert.Null(response.Block);
                Assert.Equal(Money.Parse("0.01"), response.Fees);
                Assert.Equal(prevTx.GetHash(), response.SpentCoins[0].Outpoint.Hash);
                Assert.Equal(0U, response.SpentCoins[0].Outpoint.N);
                Assert.Equal(Money.Parse("1.00"), response.SpentCoins[0].TxOut.Value);
                Assert.Equal(bob.ScriptPubKey, response.SpentCoins[0].TxOut.ScriptPubKey);
                ////

                //Once mined, the transaction should have block information
                var block = tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
                Assert.NotNull(response.Block);
                Assert.True(response.Block.BlockId == block.GetHash());
                Assert.True(response.Block.Confirmations == 1);
                Assert.True(response.Block.Height == 1);

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
                Assert.NotNull(response.Block);
                Assert.True(response.Block.BlockId == block.GetHash());
                Assert.True(response.Block.BlockHeader.GetHash() == block.Header.GetHash());
                Assert.True(response.Block.Confirmations == 2);
                Assert.True(response.Block.Height == 1);
                var json = Serializer.ToString(response); //Can serialize without blowing up
                Assert.True(json.Contains(txId.ToString())); //Just ensure no big endian problem for id
                ////

                //Can get it raw
                var bytes = tester.SendGet<byte[]>("transactions/" + tx.GetHash() + "?format=raw");
                Assert.True(bytes.SequenceEqual(tx.ToBytes()));
                response = tester.SendGet<GetTransactionResponse>("transactions/" + txId + "?format=json");
                /////
            }
        }

        [Fact]
        public void CanGetBlock()
        {
            using (var tester = ServerTester.Create())
            {
                tester.ChainBuilder.UploadBlock = true;

                var bob = new Key();
                var tx = tester.ChainBuilder.EmitMoney("1.0", bob);
                var block = tester.ChainBuilder.EmitBlock();
                var response = tester.SendGet<byte[]>("blocks/" + block.GetHash() + "?format=raw");
                Assert.True(response.SequenceEqual(block.ToBytes()));

                //404 if not found
                AssetEx.HttpError(404, () => tester.SendGet<byte[]>("blocks/18179931ea977cc0030c7c3e3e4d457f384b9e00aee9d86e39fbff0c5d3f4c40?format=raw"));
                /////

                //Can get Block with additional data
                var response2 = tester.SendGet<GetBlockResponse>("blocks/" + block.GetHash());
                Assert.True(response2.Block.ToBytes().SequenceEqual(block.ToBytes()));
                Assert.True(response2.AdditionalInformation.BlockHeader.GetHash() == block.Header.GetHash());
                Assert.True(response2.AdditionalInformation.BlockId == block.Header.GetHash());
                Assert.True(response2.AdditionalInformation.Confirmations == 1);
                Assert.True(response2.AdditionalInformation.Height == 1);
                /////

                //Can get Block by height and special
                var response3 = tester.SendGet<GetBlockResponse>("blocks/1");
                Assert.Equal(Serializer.ToString(response2), Serializer.ToString(response3));
                response3 = tester.SendGet<GetBlockResponse>("blocks/last");
                Assert.Equal(Serializer.ToString(response2), Serializer.ToString(response3));
                ////

                //Can get header only
                response3 = tester.SendGet<GetBlockResponse>("blocks/1?headerOnly=true&format=json");
                Assert.Null(response3.Block);
                Assert.NotNull(response3.AdditionalInformation);

                response = tester.SendGet<byte[]>("blocks/1?headerOnly=true&format=raw");
                Assert.True(response.SequenceEqual(response3.AdditionalInformation.BlockHeader.ToBytes()));
                ////
            }
        }

        [Fact]
        public void CanGetBalance()
        {
            using (var tester = ServerTester.Create())
            {
                var bob = new Key().GetBitcoinSecret(Network.TestNet);
                var balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                Assert.True(balance.Total == 0m);
                Assert.True(balance.Operations.Count == 0);

                tester.ChainBuilder.EmitMoney(Money.Coins(1.00m), bob);
                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                Assert.True(balance.Total == Money.Coins(1.00m));
                Assert.True(balance.Operations.Count == 1);


                AssetEx.HttpError(400, () => tester.SendGet<GetTransactionResponse>("balances/000lol"));
            }
        }

        [Fact]
        public void CanWhatIsIt()
        {
            //TODO: These tests are fragile, a simple api change can break them. We should try to be more relax.
            using (var tester = ServerTester.Create())
            {
                tester.ChainBuilder.UploadBlock = true;
                var bob = new BitcoinSecret("KxMVn7SRNkWTfVa78UXCmsc6Kyp3aQZydnyzGzNBrRg2T9X1u4er");
                //whatisit/[address|txId|blockId|blockheader|base58|transaction|script|scriptbytes]

                //Can parse base58 secret (depends on MainNet)
                AssertWhatIsIt(
                    tester,
                    bob.ToString(),
                    "{  \"PublicKey\": {    \"Hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",    \"IsCompressed\": true,    \"Address\": {      \"IsP2SH\": false,      \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",      \"ScriptPubKey\": {        \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",        \"Address\": \"35Dwzds797NcNuP21oL2c2yqB62yM7TBrf\",        \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",        \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"      },      \"RedeemScript\": null,      \"PublicKey\": null,      \"Base58\": \"18GDK7Arwo1y7DnCab2QzheYXK6rqW8zzM\",      \"Type\": \"PUBKEY_ADDRESS\",      \"Network\": \"MainNet\"    },    \"P2SHAddress\": {      \"IsP2SH\": true,      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"ScriptPubKey\": {        \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",        \"Address\": \"36tbbjetMDKRFuvFb6CkXrfiymNhceuw5H\",        \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",        \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"      },      \"RedeemScript\": {        \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",        \"Address\": \"3NxUy3EJq1WPPkwq212y1KB8etpD4aTgCh\",        \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",        \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"      },      \"PublicKey\": null,      \"Base58\": \"3NxUy3EJq1WPPkwq212y1KB8etpD4aTgCh\",      \"Type\": \"SCRIPT_ADDRESS\",      \"Network\": \"MainNet\"    },    \"ScriptPubKey\": {      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"Address\": \"3NxUy3EJq1WPPkwq212y1KB8etpD4aTgCh\",      \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    }  },  \"Base58\": \"KxMVn7SRNkWTfVa78UXCmsc6Kyp3aQZydnyzGzNBrRg2T9X1u4er\",  \"Type\": \"SECRET_KEY\",  \"Network\": \"MainNet\"}"
                    );
                //Can parse address (depends on TestNet)
                bob = bob.Key.GetBitcoinSecret(Network.TestNet);
                AssertWhatIsIt(
                    tester,
                    bob.GetAddress().ToString(),
                    "{  \"IsP2SH\": false,  \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",  \"ScriptPubKey\": {    \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",    \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",    \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",    \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"  },  \"RedeemScript\": null,  \"PublicKey\": null,  \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",  \"Type\": \"PUBKEY_ADDRESS\",  \"Network\": \"TestNet\"}"
                    );

                //Can find transaction
                var tx = tester.ChainBuilder.EmitMoney("1.0", bob, false);
                AssertWhatIsIt(
                    tester,
                    tx.GetHash().ToString(),
                    null
                    );
                tester.ChainBuilder.Broadcast(tx);
                AssertWhatIsIt(
                    tester,
                    tx.GetHash().ToString(),
                    "{  \"Transaction\": \"01000000000100e1f505000000001976a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac00000000\",  \"TransactionId\": \"2a73ffc6cedfa8f7d807f2448decde899ca924efead70ccccc5ab70f028492da\",  \"IsCoinbase\": false,  \"Block\": null,  \"SpentCoins\": [],  \"Fees\": -100000000}"
                    );
                /////

                //Can find block by id
                AssertWhatIsIt(
                    tester,
                    "fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5",
                    null
                    );
                var b = tester.ChainBuilder.EmitBlock(1); //fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5
                tester.UpdateServerChain();
                AssertWhatIsIt(
                    tester,
                    "fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5",
                    "{  \"AdditionalInformation\": {    \"BlockId\": \"fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5\",    \"BlockHeader\": \"0200000043497fd7f826957108f4a30fd9cec3aeba79972084e90ead01ea3309000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000\",    \"Height\": 1,    \"Confirmations\": 1  },  \"Block\": null}"
                    );
                /////

                //Can find block by height
                AssertWhatIsIt(
                    tester,
                    "1",
                    "{  \"AdditionalInformation\": {    \"BlockId\": \"fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5\",    \"BlockHeader\": \"0200000043497fd7f826957108f4a30fd9cec3aeba79972084e90ead01ea3309000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000\",    \"Height\": 1,    \"Confirmations\": 1  },  \"Block\": null}"
                    );
                AssertWhatIsIt(
                   tester,
                   "2",
                   null
                   );
                /////

                //Can find public key
                AssertWhatIsIt(
                    tester,
                    bob.Key.PubKey.ToHex(),
                    "{  \"Hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",  \"IsCompressed\": true,  \"Address\": {    \"IsP2SH\": false,    \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",    \"ScriptPubKey\": {      \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",      \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",      \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",      \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"    },    \"RedeemScript\": null,    \"PublicKey\": null,    \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",    \"Type\": \"PUBKEY_ADDRESS\",    \"Network\": \"TestNet\"  },  \"P2SHAddress\": {    \"IsP2SH\": true,    \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"ScriptPubKey\": {      \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",      \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",      \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",      \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"    },    \"RedeemScript\": {      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    },    \"PublicKey\": null,    \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"Type\": \"SCRIPT_ADDRESS\",    \"Network\": \"TestNet\"  },  \"ScriptPubKey\": {    \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",    \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"  }}"
                    );
                ////

                //Can find public key from address if divulged
                tx = new TransactionBuilder()
                        .AddKeys(bob)
                        .AddCoins(new Coin(tx, 0U))
                        .SendFees(Money.Coins(0.05m))
                        .SetChange(bob)
                        .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);
                AssertWhatIsIt(
                    tester,
                    bob.GetAddress().ToString(),
                    "{  \"IsP2SH\": false,  \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",  \"ScriptPubKey\": {    \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",    \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",    \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",    \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"  },  \"RedeemScript\": null,  \"PublicKey\": {    \"Hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",    \"IsCompressed\": true,    \"Address\": {      \"IsP2SH\": false,      \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",      \"ScriptPubKey\": {        \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",        \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",        \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",        \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"      },      \"RedeemScript\": null,      \"PublicKey\": null,      \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",      \"Type\": \"PUBKEY_ADDRESS\",      \"Network\": \"TestNet\"    },    \"P2SHAddress\": {      \"IsP2SH\": true,      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"ScriptPubKey\": {        \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",        \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",        \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",        \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"      },      \"RedeemScript\": {        \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",        \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",        \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",        \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"      },      \"PublicKey\": null,      \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Type\": \"SCRIPT_ADDRESS\",      \"Network\": \"TestNet\"    },    \"ScriptPubKey\": {      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    }  },  \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",  \"Type\": \"PUBKEY_ADDRESS\",  \"Network\": \"TestNet\"}"
                    );
                //Should also find with the pub key hash
                AssertWhatIsIt(
                    tester,
                    "4fa965c94a53aaa0d87d1d05a826d77906ff5219",
                    "{  \"IsP2SH\": false,  \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",  \"ScriptPubKey\": {    \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",    \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",    \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",    \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"  },  \"RedeemScript\": null,  \"PublicKey\": {    \"Hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",    \"IsCompressed\": true,    \"Address\": {      \"IsP2SH\": false,      \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",      \"ScriptPubKey\": {        \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",        \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",        \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",        \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"      },      \"RedeemScript\": null,      \"PublicKey\": null,      \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",      \"Type\": \"PUBKEY_ADDRESS\",      \"Network\": \"TestNet\"    },    \"P2SHAddress\": {      \"IsP2SH\": true,      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"ScriptPubKey\": {        \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",        \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",        \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",        \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"      },      \"RedeemScript\": {        \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",        \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",        \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",        \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"      },      \"PublicKey\": null,      \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Type\": \"SCRIPT_ADDRESS\",      \"Network\": \"TestNet\"    },    \"ScriptPubKey\": {      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    }  },  \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",  \"Type\": \"PUBKEY_ADDRESS\",  \"Network\": \"TestNet\"}"
                    );
                /////

                //Can find redeem script if divulged
                tx = tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), bob.Key.PubKey.ScriptPubKey.Hash);
                tx = new TransactionBuilder()
                        .AddKeys(bob)
                        .AddCoins(new Coin(tx, 0U))
                        .AddKnownRedeems(bob.Key.PubKey.ScriptPubKey)
                        .SendFees(Money.Coins(0.05m))
                        .SetChange(bob)
                        .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);
                AssertWhatIsIt(
                   tester,
                   bob.Key.PubKey.ScriptPubKey.GetScriptAddress(Network.TestNet).ToString(),
                   "{  \"IsP2SH\": true,  \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"ScriptPubKey\": {    \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",    \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",    \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",    \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"  },  \"RedeemScript\": {    \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",    \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"  },  \"PublicKey\": null,  \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"Type\": \"SCRIPT_ADDRESS\",  \"Network\": \"TestNet\"}"
                   );
                //Should also find with the script hash
                AssertWhatIsIt(
                   tester,
                   "e947748c6687299740a448d524dc7aef830023a7",
                   "{  \"IsP2SH\": true,  \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"ScriptPubKey\": {    \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",    \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",    \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",    \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"  },  \"RedeemScript\": {    \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",    \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"  },  \"PublicKey\": null,  \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"Type\": \"SCRIPT_ADDRESS\",  \"Network\": \"TestNet\"}"
                   );
                ////

                //Can decode script
                AssertWhatIsIt(
                  tester,
                  "21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac",
                  "{  \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",  \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"}"
                  );
                AssertWhatIsIt(
                  tester,
                  "025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG",
                  "{  \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",  \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"}"
                  );
                ////

                //Can decode signature
                AssertWhatIsIt(
                    tester,
                    "3045022100a8a45e762fbda89f16a08de25274257eb2b7d9fbf481d359b28e47205c8bdc2f022007917ee618ae55a8936c75ad603623671f27ce8591010b769718ebc5ff295cf001",
                    "{  \"Raw\": \"3045022100a8a45e762fbda89f16a08de25274257eb2b7d9fbf481d359b28e47205c8bdc2f022007917ee618ae55a8936c75ad603623671f27ce8591010b769718ebc5ff295cf001\",  \"R\": \"a8a45e762fbda89f16a08de25274257eb2b7d9fbf481d359b28e47205c8bdc2f\",  \"S\": \"7917ee618ae55a8936c75ad603623671f27ce8591010b769718ebc5ff295cf0\",  \"AnyoneCanPay\": false,  \"SigHash\": \"All\"}"
                    );
                /////
                
                //Can decode block header
                AssertWhatIsIt(
                    tester,
                    Encoders.Hex.EncodeData(Network.Main.GetGenesis().Header.ToBytes()),
                    "{  \"Version\": \"1\",  \"Hash\": \"000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f\",  \"Previous\": \"0000000000000000000000000000000000000000000000000000000000000000\",  \"Time\": \"2009-01-03T19:15:05+01:00\",  \"Nonce\": 2083236893,  \"HashMerkelRoot\": \"4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b\",  \"Bits\": \"00000000ffff0000000000000000000000000000000000000000000000000000\"}");
                /////
            }
        }

        //[DebuggerHidden]
        private void AssertWhatIsIt(ServerTester tester, string data, string expected)
        {
            var actual = tester.SendGet<string>("whatisit/" + data);
            if (expected == null)
            {
                Assert.Equal("\"Good question Holmes !\"", actual);
            }
            else
            {
                actual = JObject.Parse(actual).ToString().Replace("\r\n", "").Replace("\"", "\\\"");
                expected = JObject.Parse(expected).ToString().Replace("\r\n", "").Replace("\"", "\\\"");
                Assert.Equal(expected, actual);
            }
        }
    }
}
