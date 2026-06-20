using UnityEngine;

namespace Ginei
{
    /// <summary>三位一体の緊張の調整係数（クラウゼヴィッツ「驚くべき三位一体」）。</summary>
    public readonly struct TrinitarianTensionParams
    {
        /// <summary>均衡度に最弱要素を効かせる重み（高いほど偏りが均衡度を強く削る）。</summary>
        public readonly float weakestWeight;
        /// <summary>戦死者が民衆支持（情念）を冷ます速さ/秒（厭戦）。</summary>
        public readonly float wearinessRate;
        /// <summary>戦争遂行能力が立ち上がる最低均衡度（これ未満は支えきれない）。</summary>
        public readonly float sustainFloor;
        /// <summary>均衡破綻（崩壊）とみなす均衡度の既定閾値。</summary>
        public readonly float collapseThreshold;

        public TrinitarianTensionParams(float weakestWeight, float wearinessRate,
                                        float sustainFloor, float collapseThreshold)
        {
            this.weakestWeight = Mathf.Clamp01(weakestWeight);
            this.wearinessRate = Mathf.Max(0f, wearinessRate);
            this.sustainFloor = Mathf.Clamp01(sustainFloor);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
        }

        /// <summary>既定＝最弱重み0.6・厭戦速度0.5・遂行下限0.25・崩壊閾値0.3。</summary>
        public static TrinitarianTensionParams Default => new TrinitarianTensionParams(0.6f, 0.5f, 0.25f, 0.3f);
    }

    /// <summary>
    /// 三位一体の緊張の純ロジック（#1135・クラウゼヴィッツ『戦争論』の「驚くべき三位一体」）。
    /// 戦争は①政府（理性・政策の意志 govWill）②軍（武勇・蓋然性と偶然の領域 militaryStrength）
    /// ③国民（情念・憎悪と敵意 popularSupport）の三要素の均衡で成り立ち、その均衡が崩れると戦争遂行が破綻する。
    /// 核は <see cref="TrinityBalance"/>（三者が揃うほど高く偏ると低い＝最弱要素が効く）と
    /// <see cref="WeakestPillar"/>（破綻の起点）。
    /// 国家状態の合成は <see cref="FactionStateRules"/>、出兵の政治は <see cref="WarPoliticsRules"/>、
    /// 偶然・摩擦は <see cref="FrictionRules"/>（同 EPIC CLZ）、戦争目標・厭戦の実体は <see cref="WarGoalRules"/> が担い、
    /// ここは「政府×軍×民衆の三角形の均衡破綻検知」のみを扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TrinitarianTensionRules
    {
        /// <summary>三位一体の要素（破綻の起点を指す）。</summary>
        public enum TrinityPillar { 政府, 軍, 国民 }

        /// <summary>
        /// 三位一体の均衡度（0..1）。三要素の算術平均を、最弱要素 min との重み付き混合で割り引く
        /// （平均×(1−w)＋最弱×w）。3つが揃うほど高く、どれか1つが欠けると最弱要素が均衡度を引き下げる
        /// ＝偏った三位一体は崩れやすい。
        /// </summary>
        public static float TrinityBalance(float govWill, float militaryStrength, float popularSupport,
                                           TrinitarianTensionParams p)
        {
            float g = Mathf.Clamp01(govWill);
            float m = Mathf.Clamp01(militaryStrength);
            float pop = Mathf.Clamp01(popularSupport);
            float mean = (g + m + pop) / 3f;
            float weakest = Mathf.Min(g, Mathf.Min(m, pop));
            return Mathf.Clamp01(mean * (1f - p.weakestWeight) + weakest * p.weakestWeight);
        }

        public static float TrinityBalance(float govWill, float militaryStrength, float popularSupport)
            => TrinityBalance(govWill, militaryStrength, popularSupport, TrinitarianTensionParams.Default);

        /// <summary>
        /// 最も弱い要素＝破綻の起点。同値なら 政府＞軍＞国民 の順で返す（決定論）。
        /// </summary>
        public static TrinityPillar WeakestPillar(float govWill, float militaryStrength, float popularSupport)
        {
            float g = Mathf.Clamp01(govWill);
            float m = Mathf.Clamp01(militaryStrength);
            float pop = Mathf.Clamp01(popularSupport);
            // 政府を起点に、より弱い要素があれば置き換える（同値は先勝ち＝政府＞軍＞国民）。
            TrinityPillar weakest = TrinityPillar.政府;
            float min = g;
            if (m < min) { min = m; weakest = TrinityPillar.軍; }
            if (pop < min) { weakest = TrinityPillar.国民; }
            return weakest;
        }

        /// <summary>
        /// 戦争遂行能力（0..1）＝三位一体の均衡が戦争を支える度合い。均衡度が遂行下限 sustainFloor 未満なら0
        /// （三角形が崩れた戦争は遂行できない）、下限以上では下限〜1.0 を線形に立ち上げる。
        /// </summary>
        public static float WarSustainability(float trinityBalance, TrinitarianTensionParams p)
        {
            float b = Mathf.Clamp01(trinityBalance);
            if (b <= p.sustainFloor) return 0f;
            return Mathf.Clamp01((b - p.sustainFloor) / Mathf.Max(1e-4f, 1f - p.sustainFloor));
        }

        public static float WarSustainability(float trinityBalance)
            => WarSustainability(trinityBalance, TrinitarianTensionParams.Default);

        /// <summary>
        /// 政府の意志と軍事力の乖離（-1..1）＝govWill−militaryStrength。
        /// 正＝政治目的に対して軍が足りない（意志先行）、負＝軍が政治の制御を超える（軍の暴走）。
        /// 0 が政軍の釣り合い。
        /// </summary>
        public static float PoliticalMilitaryGap(float govWill, float militaryStrength)
        {
            return Mathf.Clamp01(govWill) - Mathf.Clamp01(militaryStrength);
        }

        /// <summary>
        /// 戦死者が民衆支持（情念）を削る＝厭戦の冷却。新しい支持(0..1)＝現支持−厭戦速度×損耗 casualties(0..1)×dt。
        /// 憎悪・敵意は犠牲の累積で冷め、国民という柱が痩せていく。
        /// </summary>
        public static float PopularWarWeariness(float popularSupport, float casualties, float dt,
                                                TrinitarianTensionParams p)
        {
            if (dt <= 0f) return Mathf.Clamp01(popularSupport);
            float drain = p.wearinessRate * Mathf.Clamp01(casualties) * dt;
            return Mathf.Clamp01(Mathf.Clamp01(popularSupport) - drain);
        }

        public static float PopularWarWeariness(float popularSupport, float casualties, float dt)
            => PopularWarWeariness(popularSupport, casualties, dt, TrinitarianTensionParams.Default);

        /// <summary>
        /// 均衡が崩れて戦争遂行が破綻するか＝三位一体の均衡度が崩壊閾値未満。
        /// </summary>
        public static bool CollapseDetection(float trinityBalance, float threshold)
        {
            return Mathf.Clamp01(trinityBalance) < Mathf.Clamp01(threshold);
        }

        public static bool CollapseDetection(float trinityBalance)
            => CollapseDetection(trinityBalance, TrinitarianTensionParams.Default.collapseThreshold);

        /// <summary>
        /// 民衆の情念（憎悪）と政府の理性（政策）の緊張（-1..1）＝popularSupport−govWill。
        /// 正＝情念が政策を超える（無制限戦争へ暴走しがち）、負＝理性が情念を欠く（政府が支持を失う）。
        /// 0 が情念と理性の調和。
        /// </summary>
        public static float PassionRationalityTension(float popularSupport, float govWill)
        {
            return Mathf.Clamp01(popularSupport) - Mathf.Clamp01(govWill);
        }

        /// <summary>
        /// どの要素を補強すべきか＝最弱要素を返す（破綻の起点を補うのが再均衡の定石）。
        /// <see cref="WeakestPillar"/> への委譲。
        /// </summary>
        public static TrinityPillar RebalancingNeed(float govWill, float militaryStrength, float popularSupport)
        {
            return WeakestPillar(govWill, militaryStrength, popularSupport);
        }
    }
}
