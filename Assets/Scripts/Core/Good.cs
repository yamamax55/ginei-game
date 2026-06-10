using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 市場で取引される財の種別（M-1 #179-182）。兵站の <see cref="ResourceType"/> とは別系統＝
    /// 市場経済（需給で価格が動く財）専用に独立定義する（重複させない）。奢侈品は生活水準・支持に効く。
    /// </summary>
    public enum GoodType { 物資, 燃料, 弾薬, 奢侈品 }

    /// <summary>
    /// 財の定義（M-1 #180・純データ）。種別と基準価格（需給均衡の中心＝価格が戻る先）を持つ。
    /// 取引の在庫・需給・価格は <see cref="Market"/>、均衡解決は <see cref="MarketRules"/> が扱う。
    /// </summary>
    [Serializable]
    public class Good
    {
        /// <summary>財の種別。</summary>
        public GoodType goodType;
        /// <summary>基準価格（需給均衡の中心＝供給=需要のときに収束する価格）。負にはならない。</summary>
        public float basePrice;

        public Good() { }

        public Good(GoodType goodType, float basePrice)
        {
            this.goodType = goodType;
            this.basePrice = Mathf.Max(0f, basePrice);
        }
    }
}
