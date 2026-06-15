using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 国債の年次進行（#161/#185 配線の唯一の窓口）。勢力の債務（<see cref="FiscalState.debt"/>）を債券（<see cref="Bond"/>）の
    /// 額面残高に同期し、財政健全度（<see cref="FiscalRules.FiscalHealthFactor"/>）からデフォルトリスク、市場金利
    /// （<see cref="FiscalRules.InterestRate"/>）から債券価格を <see cref="BondMarketRules"/> で収束させる
    /// ＝財政が悪化すると国債価格↓・利回り↑（借入コスト上昇＝市場の規律）。数式は既存純ロジックへ委譲し
    /// 二重実装しない。乱数なし・決定論・test-first。
    /// </summary>
    public static class SovereignBondRules
    {
        /// <summary>国債の表面利率（発行時の固定クーポン・額面比価格の中心を決める）。</summary>
        public const float DefaultCouponRate = 0.03f;

        /// <summary>勢力へ国債を用意する（冪等・既存はそのまま）。</summary>
        public static Bond Ensure(Bond existing, Faction issuer)
        {
            return existing ?? new Bond(issuer, faceValue: 0f, couponRate: DefaultCouponRate, price: 1f, defaultRisk: 0f, name: "国債");
        }

        /// <summary>
        /// 国債を1年ぶん進める：額面＝現在の債務に同期、財政健全度→デフォルトリスク、市場金利→価格収束
        /// （金利/信用が高いほど価格↓・利回り↑＝逆相関）。
        /// </summary>
        public static void TickYear(Bond b, FiscalState fs, float economy, float dt)
        {
            if (b == null || fs == null || dt <= 0f) return;

            var fp = FiscalRules.FiscalParams.Default;
            var bp = BondMarketRules.BondParams.Default;

            b.faceValue = Mathf.Max(0f, fs.debt);                          // 既発国債の残高＝累積債務
            float health = economy > 0f ? FiscalRules.FiscalHealthFactor(fs, economy, fp) : 1f;
            b.defaultRisk = Mathf.Clamp01(1f - health);                    // 財政悪化→デフォルトリスク↑
            float marketRate = FiscalRules.InterestRate(fs, economy, fp);  // 市場金利（債務比率のリスク込み）
            BondMarketRules.Tick(b, marketRate, bp, dt);                   // 価格を適正へ収束（逆相関）
        }

        /// <summary>
        /// 中央銀行の政策金利を織り込む版（#中銀↔国債 配線）：市場金利＝max(財政由来金利, 政策金利)＝
        /// 利上げ局面では国債の必要利回りが上がり価格↓利回り↑（金融政策が国債市場へ波及）。
        /// </summary>
        public static void TickYear(Bond b, FiscalState fs, float economy, float dt, float policyRate)
        {
            if (b == null || fs == null || dt <= 0f) return;

            var fp = FiscalRules.FiscalParams.Default;
            var bp = BondMarketRules.BondParams.Default;

            b.faceValue = Mathf.Max(0f, fs.debt);
            float health = economy > 0f ? FiscalRules.FiscalHealthFactor(fs, economy, fp) : 1f;
            b.defaultRisk = Mathf.Clamp01(1f - health);
            float marketRate = Mathf.Max(FiscalRules.InterestRate(fs, economy, fp), Mathf.Max(0f, policyRate));
            BondMarketRules.Tick(b, marketRate, bp, dt);
        }
    }
}
