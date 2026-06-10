using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 株式市場の上場企業（CAP-1 #185・純データ）。少数の量で株価が創発＝タイクン化回避。
    /// 株価は <see cref="StockMarketRules.Tick"/> が適正株価（<see cref="StockMarketRules.FairPrice"/>）へ滑らかに収束させる。
    /// </summary>
    [Serializable]
    public class Company
    {
        /// <summary>1株あたり収益（>0で株価を押し上げる。負にはならない）。</summary>
        public float earnings;
        /// <summary>現在株価（収益×市場心理で揺れる。負にはならない）。</summary>
        public float sharePrice;
        /// <summary>1株あたり配当（配当利回りの分子。負にはならない）。</summary>
        public float dividend;
        /// <summary>市場心理（0..1。強気で割高・弱気で割安に評価される）。</summary>
        public float sentiment;

        public Company() { }

        public Company(float earnings, float sharePrice, float dividend, float sentiment)
        {
            this.earnings = Mathf.Max(0f, earnings);
            this.sharePrice = Mathf.Max(0f, sharePrice);
            this.dividend = Mathf.Max(0f, dividend);
            this.sentiment = Mathf.Clamp01(sentiment);
        }
    }
}
