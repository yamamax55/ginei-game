using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 機動戦 vs 消耗戦（戦争様式の選択）の調整値。機動寄与の重み・消耗戦の削り速度・機動戦が要求するテンポの強さ・
    /// 費用対効果の損害下限（ゼロ除算回避）と上限。符号付き指標は -1..1。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct ManeuverWarfareParams
    {
        /// <summary>機動戦寄与の重み（自機動力にこの重みを掛けて火力と比べる＝機動を重く見るほど機動戦寄り）。</summary>
        public readonly float maneuverWeight;
        /// <summary>消耗戦の削り速度（1ターン・火力比1あたりに進む削り合いの量）。</summary>
        public readonly float grindRate;
        /// <summary>機動戦が要求するテンポの強さ（機動寄りなほど高速テンポを要求する倍率）。</summary>
        public readonly float tempoDemand;
        /// <summary>費用対効果の損害下限（これ未満の損害として扱わない＝ゼロ除算・発散回避）。</summary>
        public readonly float minLoss;
        /// <summary>費用対効果の上限（少ない損害で崩しても青天井にしない）。</summary>
        public readonly float maxEffectiveness;

        public ManeuverWarfareParams(float maneuverWeight, float grindRate, float tempoDemand,
            float minLoss, float maxEffectiveness)
        {
            this.maneuverWeight = Mathf.Clamp(maneuverWeight, 0.1f, 4f);
            this.grindRate = Mathf.Clamp(grindRate, 0.001f, 1f);
            this.tempoDemand = Mathf.Clamp(tempoDemand, 0.1f, 4f);
            this.minLoss = Mathf.Clamp(minLoss, 0.001f, 1f);
            this.maxEffectiveness = Mathf.Clamp(maxEffectiveness, 1f, 100f);
        }

        /// <summary>既定：機動寄与1.0・削り速度0.1・テンポ要求1.0・損害下限0.05・費用対効果上限10。</summary>
        public static ManeuverWarfareParams Default => new ManeuverWarfareParams(
            DefaultManeuverWeight, DefaultGrindRate, DefaultTempoDemand,
            DefaultMinLoss, DefaultMaxEffectiveness);

        public const float DefaultManeuverWeight = 1.0f;
        public const float DefaultGrindRate = 0.1f;
        public const float DefaultTempoDemand = 1.0f;
        public const float DefaultMinLoss = 0.05f;
        public const float DefaultMaxEffectiveness = 10f;
    }

    /// <summary>
    /// 機動戦 vs 消耗戦の純ロジック（唯一の窓口）＝<b>敵を物理的に削り尽くすか（消耗戦）、機動で敵の組織・意志を
    /// 瓦解させるか（機動戦）の戦争様式の選択</b>。機動戦は少ない損害で敵を瓦解させうるが、敵が固く/柔軟で噛み合わ
    /// ないと空振りに終わる。消耗戦は確実に効くが高コスト（双方が削れる）。
    /// <b>分担</b>：<see cref="AttritionExchangeRules"/>（あれば＝消耗の交換比＝どちらがどれだけ削れるか）とは別＝
    /// こちらは<b>機動戦と消耗戦という戦争様式の選択</b>。<see cref="ManeuverEnvelopmentRules"/>（あれば＝具体的な機動
    /// 包囲の幾何）とも別＝こちらは<b>戦争観の対比</b>（崩壊を狙うか削り合うか）。盤面非依存の plain 引数・
    /// 実効値パターン（基準値非破壊）・test-first。
    /// </summary>
    public static class ManeuverWarfareRules
    {
        /// <summary>既定パラメータで機動戦寄りか消耗戦寄りか。</summary>
        public static float ManeuverVsAttrition(float ownMobility, float ownFirepower)
            => ManeuverVsAttrition(ownMobility, ownFirepower, ManeuverWarfareParams.Default);

        /// <summary>
        /// 機動戦寄りか消耗戦寄りか（-1消耗 / +1機動）。機動力（×重み）が火力に勝るほど機動戦へ、火力が勝るほど
        /// 消耗戦へ傾く。approach＝(機動×重み − 火力)/(機動×重み ＋ 火力)。拮抗0／機動のみ+1／火力のみ−1。
        /// </summary>
        public static float ManeuverVsAttrition(float ownMobility, float ownFirepower, ManeuverWarfareParams p)
        {
            float mob = Mathf.Max(0f, ownMobility) * p.maneuverWeight;
            float fire = Mathf.Max(0f, ownFirepower);
            float denom = mob + fire;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp((mob - fire) / denom, -1f, 1f);
        }

        /// <summary>
        /// 機動で敵システムを崩壊させる度合い（0..1）。機動の優位（maneuverAdvantage&gt;0）が大きいほど、かつ敵の
        /// 結束が低いほど大きい。disruption＝max(0,優位)×(1−結束)。劣位（≤0）では崩せず（0）。結束1.0の敵は崩れない
        /// ＝固い敵には機動戦が効きにくい。
        /// </summary>
        public static float SystemDisruption(float maneuverAdvantage, float enemyCohesion)
        {
            float adv = Mathf.Max(0f, Mathf.Clamp(maneuverAdvantage, -1f, 1f));
            float cohesion = Mathf.Clamp01(enemyCohesion);
            return Mathf.Clamp01(adv * (1f - cohesion));
        }

        /// <summary>既定パラメータで消耗戦の削り合いの進行。</summary>
        public static float AttritionGrind(float firepowerRatio, float duration)
            => AttritionGrind(firepowerRatio, duration, ManeuverWarfareParams.Default);

        /// <summary>
        /// 消耗戦で削り合う進行（0..1）。火力比（自/敵）が高いほど、続けるほど着実に削り切る。
        /// grind＝clamp01(火力比×時間×削り速度)。火力比1で10ターン続ければ満（既定0.1×10）＝確実だが時間と損害を要する。
        /// </summary>
        public static float AttritionGrind(float firepowerRatio, float duration, ManeuverWarfareParams p)
        {
            float ratio = Mathf.Max(0f, firepowerRatio);
            float dur = Mathf.Max(0f, duration);
            return Mathf.Clamp01(ratio * dur * p.grindRate);
        }

        /// <summary>既定パラメータで機動戦が要求するテンポの速さ。</summary>
        public static float TempoRequirement(float maneuverApproach)
            => TempoRequirement(maneuverApproach, ManeuverWarfareParams.Default);

        /// <summary>
        /// 機動戦が要求するテンポの速さ（0..1）。機動寄り（approach&gt;0）なほど高速テンポを要求する＝機動戦は速さが命。
        /// tempo＝clamp01(max(0,approach)×テンポ要求倍率)。消耗寄り（≤0）はテンポを要求しない（0＝じっくり削れる）。
        /// </summary>
        public static float TempoRequirement(float maneuverApproach, ManeuverWarfareParams p)
        {
            float approach = Mathf.Max(0f, Mathf.Clamp(maneuverApproach, -1f, 1f));
            return Mathf.Clamp01(approach * p.tempoDemand);
        }

        /// <summary>
        /// 機動戦が敵に読まれ噛み合わない空振り（0..1）。機動寄り（approach&gt;0）なほど、かつ敵が柔軟なほど読まれて
        /// 空振る。penalty＝max(0,approach)×敵柔軟性。消耗寄り（≤0）や硬直した敵には空振りしない（0）。
        /// ＝機動戦は柔軟な敵相手だと当てが外れる諸刃。
        /// </summary>
        public static float MismatchPenalty(float maneuverApproach, float enemyFlexibility)
        {
            float approach = Mathf.Max(0f, Mathf.Clamp(maneuverApproach, -1f, 1f));
            float flex = Mathf.Clamp01(enemyFlexibility);
            return Mathf.Clamp01(approach * flex);
        }

        /// <summary>既定パラメータで機動戦の費用対効果。</summary>
        public static float CostEffectiveness(float systemDisruption, float ownLosses)
            => CostEffectiveness(systemDisruption, ownLosses, ManeuverWarfareParams.Default);

        /// <summary>
        /// 機動戦の費用対効果（崩壊量あたりの安さ）。少ない損害で大きく崩すほど高い。
        /// effectiveness＝clamp(崩壊量/max(損害下限,損害), 0, 上限)。損害が小さいほど跳ね上がる（機動戦の旨味＝
        /// 損害ゼロで敵を瓦解させるのが理想）が上限で頭打ち。
        /// </summary>
        public static float CostEffectiveness(float systemDisruption, float ownLosses, ManeuverWarfareParams p)
        {
            float disruption = Mathf.Clamp01(systemDisruption);
            float losses = Mathf.Max(p.minLoss, ownLosses);
            return Mathf.Clamp(disruption / losses, 0f, p.maxEffectiveness);
        }

        /// <summary>
        /// 消耗戦の確実性と機動戦の効率のトレードオフ（-1消耗の確実 / +1機動の効率）。機動の崩壊が大きいほど機動戦
        /// （効率）へ、消耗の削りが大きいほど消耗戦（確実）へ傾く。balance＝(崩壊 − 削り)/(崩壊 ＋ 削り)。
        /// 拮抗0／崩壊のみ+1（機動が効く）／削りのみ−1（消耗が確実）。
        /// </summary>
        public static float CertaintyVsEfficiency(float attritionGrind, float systemDisruption)
        {
            float grind = Mathf.Clamp01(attritionGrind);
            float disruption = Mathf.Clamp01(systemDisruption);
            float denom = grind + disruption;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp((disruption - grind) / denom, -1f, 1f);
        }

        /// <summary>
        /// 機動で敵を瓦解させたか（bool）＝システム崩壊度がしきい値以上。僅かな崩しでは敵はまだ立て直せる（既定0.6）。
        /// </summary>
        public static bool IsCollapseAchieved(float systemDisruption, float threshold = 0.6f)
        {
            float disruption = Mathf.Clamp01(systemDisruption);
            return disruption >= Mathf.Clamp01(threshold);
        }
    }
}
