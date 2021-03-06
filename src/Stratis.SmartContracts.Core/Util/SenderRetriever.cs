﻿using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core.Util
{
    public class SenderRetriever : ISenderRetriever
    {
        /// <inheritdoc />
        public GetSenderResult GetSender(Transaction tx, ICoinView coinView, IList<Transaction> blockTxs)
        {
            OutPoint prevOut = tx.Inputs[0].PrevOut;

            // Check the txes in this block first
            if (blockTxs != null && blockTxs.Count > 0)
            {
                foreach (Transaction btx in blockTxs)
                {
                    if (btx.GetHash() == prevOut.Hash)
                    {
                        Script script = btx.Outputs[prevOut.N].ScriptPubKey;
                        return GetAddressFromScript(script);
                    }
                }
            }

            // Check the utxoset for the p2pk of the unspent output for this transaction
            if (coinView != null)
            {
                FetchCoinsResponse fetchCoinResult = coinView.FetchCoinsAsync(new uint256[] { prevOut.Hash }).Result;
                UnspentOutputs unspentOutputs = fetchCoinResult.UnspentOutputs.FirstOrDefault();

                if (unspentOutputs == null)
                {
                    throw new Exception("Unspent outputs to smart contract transaction are not present in coinview");
                }

                Script script = unspentOutputs.Outputs[prevOut.N].ScriptPubKey;

                return GetAddressFromScript(script);
            }

            return GetSenderResult.CreateFailure("Unable to get the sender of the transaction");
        }

        public GetSenderResult GetSender(Transaction tx, MempoolCoinView coinView)
        {
            TxOut output = coinView.GetOutputFor(tx.Inputs[0]);
            return GetAddressFromScript(output.ScriptPubKey);
        }

        /// <inheritdoc />
        public GetSenderResult GetAddressFromScript(Script script)
        {
            PubKey payToPubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(script);

            if (payToPubKey != null)
            {
                var address = new uint160(payToPubKey.Hash.ToBytes());
                return GetSenderResult.CreateSuccess(address);
            }

            if (PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(script))
            {
                var address = new uint160(PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(script).ToBytes());
                return GetSenderResult.CreateSuccess(address);
            }
            return GetSenderResult.CreateFailure("Addresses can only be retrieved from Pay to Pub Key or Pay to Pub Key Hash");
        }
    }
}
