using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の通貨状態（#通貨・配線）。固有名（<see cref="CurrencyNames"/>）＋為替/物価/通貨供給を持つ。
    /// budget/fiscal/politics と同じく在席のセッション状態（セーブ非対象＝復元時は名前を決定論で再割当）。
    /// 進行は <see cref="CurrencyRules"/>（財政→通貨増発→インフレ／財政健全度→為替）。純データ。
    /// </summary>
    [Serializable]
    public class CurrencyState
    {
        /// <summary>通貨の固有名（<see cref="CurrencyNames"/> から割り当て）。</summary>
        public string currencyName = "";
        /// <summary>対基準の為替係数（&gt;1=通貨高／&lt;1=通貨安。財政健全度で動く）。</summary>
        public float exchangeRate = 1f;
        /// <summary>物価水準（インフレ＝1.0基準・<see cref="InflationRules"/>）。</summary>
        public float priceLevel = 1f;
        /// <summary>年率インフレ（表示用）。</summary>
        public float inflationRate = 0f;
        /// <summary>通貨供給量（赤字の貨幣化で増える）。</summary>
        public float moneySupply = 1000f;
        /// <summary>政策金利（中央銀行・将来用）。</summary>
        public float policyRate = 0.02f;

        public CurrencyState() { }

        public CurrencyState(string currencyName)
        {
            this.currencyName = currencyName ?? "";
        }
    }
}
