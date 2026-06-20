using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 封鎖の経済的影響の純ロジック（兵糧攻めの国家版）。
    /// 敵勢力を封鎖して貿易・資源輸入を断ち、敵経済を締め上げる。
    /// 封鎖が長引くほど物資が枯れ、生産・民生・戦争遂行が衰える。
    /// だが自給自足・闇貿易・代替で敵は抗う。
    ///
    /// 分担（重複実装しない）：
    /// - <see cref="BlockadeRules"/>（封鎖の軍事的な面＝艦隊で星系を扼す既存）とは別＝
    ///   こちらは封鎖がもたらす「経済的影響」（貿易遮断→資源不足→経済縮小）。
    /// - <see cref="CommerceRaidingRules"/>（通商破壊＝個々の船団狩り）とは別＝
    ///   こちらは敵経済全体への面的な締め上げ。
    /// 盤面非依存の plain 引数・乱数なし・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class BlockadeEconomyRules
    {
        /// <summary>封鎖経済の係数。トップレベル readonly struct・全 Clamp。</summary>
        public readonly struct BlockadeEconomyParams
        {
            /// <summary>封鎖強度の効き（締め上げの利き）。</summary>
            public readonly float strangleScale;
            /// <summary>どんなに封鎖しても抜ける貿易の下限割合（0..1）。</summary>
            public readonly float tradeFloor;
            /// <summary>資源不足が経済縮小へ伝わる率。</summary>
            public readonly float contractionRate;
            /// <summary>封鎖長期化で縮小が深まる時間係数。</summary>
            public readonly float durationScale;
            /// <summary>資源不足が戦争遂行能力を削る率。</summary>
            public readonly float warEffortRate;
            /// <summary>闇貿易網の抜け穴の効き。</summary>
            public readonly float leakScale;
            /// <summary>自給自足適応の進む速さ（時間あたり）。</summary>
            public readonly float autarkyRate;
            /// <summary>配給による民生困窮の緩和の効き。</summary>
            public readonly float rationingScale;

            public BlockadeEconomyParams(
                float strangleScale,
                float tradeFloor,
                float contractionRate,
                float durationScale,
                float warEffortRate,
                float leakScale,
                float autarkyRate,
                float rationingScale)
            {
                this.strangleScale = Mathf.Max(0f, strangleScale);
                this.tradeFloor = Mathf.Clamp01(tradeFloor);
                this.contractionRate = Mathf.Max(0f, contractionRate);
                this.durationScale = Mathf.Max(0f, durationScale);
                this.warEffortRate = Mathf.Max(0f, warEffortRate);
                this.leakScale = Mathf.Max(0f, leakScale);
                this.autarkyRate = Mathf.Max(0f, autarkyRate);
                this.rationingScale = Mathf.Max(0f, rationingScale);
            }

            public static BlockadeEconomyParams Default => new BlockadeEconomyParams(
                strangleScale: 1.0f,
                tradeFloor: 0.1f,
                contractionRate: 0.6f,
                durationScale: 0.05f,
                warEffortRate: 0.8f,
                leakScale: 1.0f,
                autarkyRate: 0.1f,
                rationingScale: 0.5f);
        }

        /// <summary>
        /// 貿易遮断の度合い（0..1）。封鎖強度（×利き）と敵の貿易依存の積。
        /// 貿易依存が高いほど封鎖が効く。下限ぶんは必ず抜ける。
        /// </summary>
        public static float TradeStrangulation(float blockadeStrength, float enemyTradeDependence, BlockadeEconomyParams p)
        {
            float b = Mathf.Clamp01(Mathf.Max(0f, blockadeStrength) * p.strangleScale);
            float dep = Mathf.Clamp01(enemyTradeDependence);
            float raw = b * dep;
            return Mathf.Clamp01(raw * (1f - p.tradeFloor));
        }

        public static float TradeStrangulation(float blockadeStrength, float enemyTradeDependence)
            => TradeStrangulation(blockadeStrength, enemyTradeDependence, BlockadeEconomyParams.Default);

        /// <summary>
        /// 資源不足（0..1）。貿易遮断ぶんを国内生産（自給）が打ち消す。
        /// 自給が高いほど不足は和らぐ。
        /// </summary>
        public static float ResourceShortage(float tradeStrangulation, float domesticProduction)
        {
            float strangle = Mathf.Clamp01(tradeStrangulation);
            float dom = Mathf.Clamp01(domesticProduction);
            return Mathf.Clamp01(strangle * (1f - dom));
        }

        /// <summary>
        /// 経済の縮小（0..1）。資源不足×縮小率に、封鎖長期化で深まる係数を掛ける。
        /// 長引くほど縮小が深まる。
        /// </summary>
        public static float EconomicContraction(float resourceShortage, float blockadeDuration, BlockadeEconomyParams p)
        {
            float shortage = Mathf.Clamp01(resourceShortage);
            float dur = Mathf.Max(0f, blockadeDuration);
            float durationFactor = 1f + p.durationScale * dur;
            return Mathf.Clamp01(shortage * p.contractionRate * durationFactor);
        }

        public static float EconomicContraction(float resourceShortage, float blockadeDuration)
            => EconomicContraction(resourceShortage, blockadeDuration, BlockadeEconomyParams.Default);

        /// <summary>
        /// 戦争遂行能力の低下（0..1）。資源不足が前線・建艦・補給を削る。
        /// </summary>
        public static float WarEffortDegradation(float resourceShortage, BlockadeEconomyParams p)
        {
            float shortage = Mathf.Clamp01(resourceShortage);
            return Mathf.Clamp01(shortage * p.warEffortRate);
        }

        public static float WarEffortDegradation(float resourceShortage)
            => WarEffortDegradation(resourceShortage, BlockadeEconomyParams.Default);

        /// <summary>
        /// 闇貿易による抜け穴（0..1）。密輸網（×利き）と封鎖強度の比。
        /// 密輸網が太く封鎖が薄いほど抜け穴が大きい。
        /// </summary>
        public static float BlackMarketLeakage(float blockadeStrength, float smugglingNetworks, BlockadeEconomyParams p)
        {
            float b = Mathf.Max(0f, blockadeStrength);
            float s = Mathf.Max(0f, smugglingNetworks) * p.leakScale;
            float denom = b + s;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(s / denom);
        }

        public static float BlackMarketLeakage(float blockadeStrength, float smugglingNetworks)
            => BlackMarketLeakage(blockadeStrength, smugglingNetworks, BlockadeEconomyParams.Default);

        /// <summary>
        /// 自給自足への適応（0..1）。代替努力（国内代替能力）に時間が掛かって進む。
        /// 封鎖が長引くほど代替が根付く（飽和）。
        /// </summary>
        public static float AutarkyAdaptation(float domesticSubstitution, float blockadeDuration, BlockadeEconomyParams p)
        {
            float sub = Mathf.Clamp01(domesticSubstitution);
            float dur = Mathf.Max(0f, blockadeDuration);
            float progress = Mathf.Clamp01(p.autarkyRate * dur);
            return Mathf.Clamp01(sub * progress);
        }

        public static float AutarkyAdaptation(float domesticSubstitution, float blockadeDuration)
            => AutarkyAdaptation(domesticSubstitution, blockadeDuration, BlockadeEconomyParams.Default);

        /// <summary>
        /// 民生の困窮（0..1）。資源不足ぶんを配給効率が和らげる。
        /// 配給が巧いほど民の苦しみが減る。
        /// </summary>
        public static float CivilianSuffering(float resourceShortage, float rationingEfficiency, BlockadeEconomyParams p)
        {
            float shortage = Mathf.Clamp01(resourceShortage);
            float ration = Mathf.Clamp01(rationingEfficiency);
            float relief = Mathf.Clamp01(ration * p.rationingScale);
            return Mathf.Clamp01(shortage * (1f - relief));
        }

        public static float CivilianSuffering(float resourceShortage, float rationingEfficiency)
            => CivilianSuffering(resourceShortage, rationingEfficiency, BlockadeEconomyParams.Default);

        /// <summary>
        /// 経済が締め上げられたか（経済縮小が閾値以上）。
        /// </summary>
        public static bool IsEconomyStrangled(float economicContraction, float threshold)
        {
            return Mathf.Clamp01(economicContraction) >= Mathf.Clamp01(threshold);
        }
    }
}
