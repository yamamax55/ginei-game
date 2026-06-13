using UnityEngine;

namespace Ginei
{
    /// <summary>通貨改鋳・品位（Specie）の調整係数。</summary>
    public readonly struct CoinageParams
    {
        /// <summary>地金（銀）1単位の額面換算価値。品位1.0の硬貨はこの×額面の地金を含むのが「満貨」。</summary>
        public readonly float silverValueScale;
        /// <summary>1枚を鋳造する基礎コスト（額面比）。発行益＝額面−地金−これ。</summary>
        public readonly float mintingCostScale;
        /// <summary>改鋳露見が信用を蝕む基礎速度（実含有が期待を下回る乖離×これ×dt）。</summary>
        public readonly float trustErosionRate;
        /// <summary>品位回復（満貨化）が信用を回復させる基礎速度。改鋳より遅い＝壊すのは速く戻すのは遅い。</summary>
        public readonly float trustRecoveryRate;
        /// <summary>悪貨が良貨を駆逐する強度（旧貨と新貨の品位差×これ＝退蔵される良貨の割合）。</summary>
        public readonly float greshamStrength;

        public CoinageParams(float silverValueScale, float mintingCostScale, float trustErosionRate, float trustRecoveryRate, float greshamStrength)
        {
            this.silverValueScale = Mathf.Clamp01(silverValueScale);
            this.mintingCostScale = Mathf.Clamp01(mintingCostScale);
            this.trustErosionRate = Mathf.Max(0f, trustErosionRate);
            this.trustRecoveryRate = Mathf.Max(0f, trustRecoveryRate);
            this.greshamStrength = Mathf.Max(0f, greshamStrength);
        }

        /// <summary>既定＝地金価値1.0・鋳造費0.05・信用浸食0.5・信用回復0.15・グレシャム強度1.0。</summary>
        public static CoinageParams Default => new CoinageParams(1f, 0.05f, 0.5f, 0.15f, 1f);
    }

    /// <summary>
    /// 通貨改鋳と品位の純ロジック（#1072）。硬貨の貴金属（銀）含有量を減らせば一時的に発行益
    /// （シニョリッジ）を得られるが、品位低下が露見すると通貨の信用が落ち、額面通りには通らなくなる
    /// ＝「品位を下げれば今日儲かるが、信用が落ちれば額面は紙になる」。さらに良貨は退蔵され市場には
    /// 悪貨が残る（グレシャムの法則）。<see cref="InflationRules"/>（通貨増発による物価上昇）とは別系統で、
    /// こちらは硬貨そのものの品位劣化を扱う。<see cref="FiscalRules"/>（国債・金利・債務）・
    /// <see cref="ReserveCurrencyRules"/>（基軸通貨の信認）とも分担する。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CoinageRules
    {
        /// <summary>
        /// 硬貨の地金価値＝銀含有量(0..1)×額面×地金価値係数。品位を下げるほど中身は痩せる。
        /// </summary>
        public static float IntrinsicValue(float silverContent, float faceValue, CoinageParams p)
        {
            return Mathf.Clamp01(silverContent) * Mathf.Max(0f, faceValue) * p.silverValueScale;
        }

        public static float IntrinsicValue(float silverContent, float faceValue)
            => IntrinsicValue(silverContent, faceValue, CoinageParams.Default);

        /// <summary>
        /// 発行益（シニョリッジ）＝額面−地金価値−鋳造費。品位を下げる（含有を減らす）ほど地金が痩せて
        /// 儲けが増える＝改鋳の短期の誘惑。負（満貨で鋳造費が乗る）にもなりうる。
        /// </summary>
        public static float Seigniorage(float faceValue, float silverContent, CoinageParams p)
        {
            float face = Mathf.Max(0f, faceValue);
            float intrinsic = IntrinsicValue(silverContent, face, p);
            float mintingCost = face * p.mintingCostScale;
            return face - intrinsic - mintingCost;
        }

        public static float Seigniorage(float faceValue, float silverContent)
            => Seigniorage(faceValue, silverContent, CoinageParams.Default);

        /// <summary>
        /// 改鋳の信用への影響＝1tick後の公衆信用(0..1)。実含有が期待含有を下回るほど信用が落ち、
        /// 満貨（実含有≥期待）なら時間で信用が戻る（回復は浸食より遅い）。グレシャムの法則の土壌。
        /// </summary>
        public static float DebasementTick(float publicTrust, float silverContent, float expectedContent, float dt, CoinageParams p)
        {
            float trust = Mathf.Clamp01(publicTrust);
            float actual = Mathf.Clamp01(silverContent);
            float expected = Mathf.Clamp01(expectedContent);
            float step = Mathf.Max(0f, dt);
            float shortfall = expected - actual; // 正＝品位不足
            if (shortfall > 0f)
                return Mathf.Clamp01(trust - shortfall * p.trustErosionRate * step);
            // 満貨・期待超え＝信用回復
            return Mathf.Clamp01(trust + (-shortfall) * p.trustRecoveryRate * step);
        }

        public static float DebasementTick(float publicTrust, float silverContent, float expectedContent, float dt)
            => DebasementTick(publicTrust, silverContent, expectedContent, dt, CoinageParams.Default);

        /// <summary>
        /// グレシャムの法則＝悪貨が良貨を駆逐する強度（0..1）。品位の高い旧貨ほど退蔵され、市場に残る
        /// のは品位の低い新貨。返り値＝退蔵される良貨の割合（旧貨が新貨より良いほど大きい）。
        /// 新貨が旧貨と同等以上なら駆逐は起きない（0）。
        /// </summary>
        public static float GreshamEffect(float silverContent, float oldCoinContent, CoinageParams p)
        {
            float gap = Mathf.Clamp01(oldCoinContent) - Mathf.Clamp01(silverContent);
            return Mathf.Clamp01(Mathf.Max(0f, gap) * p.greshamStrength);
        }

        public static float GreshamEffect(float silverContent, float oldCoinContent)
            => GreshamEffect(silverContent, oldCoinContent, CoinageParams.Default);

        /// <summary>
        /// 実質購買力＝額面×公衆信用(0..1)。信用が落ちれば額面通りには通らない（額面は紙になる）。
        /// 信用1.0で額面通り、0で無価値。
        /// </summary>
        public static float RealPurchasingPower(float faceValue, float publicTrust)
        {
            return Mathf.Max(0f, faceValue) * Mathf.Clamp01(publicTrust);
        }

        /// <summary>
        /// 改鋳の巻き戻しコスト＝品位を現状から目標へ戻すのに要する地金注入。
        /// ＝(目標含有−現状含有)×流通量×地金価値係数。品位を上げる場合のみコストが発生し、
        /// 既に目標以上なら0＝改鋳は片道切符に近い（下げるのは儲かり、戻すのは身銭を切る）。
        /// </summary>
        public static float RestorationCost(float currentContent, float targetContent, float moneySupply, CoinageParams p)
        {
            float gap = Mathf.Clamp01(targetContent) - Mathf.Clamp01(currentContent);
            return Mathf.Max(0f, gap) * Mathf.Max(0f, moneySupply) * p.silverValueScale;
        }

        public static float RestorationCost(float currentContent, float targetContent, float moneySupply)
            => RestorationCost(currentContent, targetContent, moneySupply, CoinageParams.Default);
    }
}
