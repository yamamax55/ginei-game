using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 追撃戦の損害収支の調整値（#追撃戦）。背走する敵への追加損害の効き・深追いのリスク勾配・敗走判定の閾値。
    /// すべて ctor で Clamp し、極端な入力でも収支が破綻しない（実効値パターン＝基準値非破壊）。
    /// </summary>
    public readonly struct PursuitBattleParams
    {
        /// <summary>追撃損害の全体係数（敗走側の混乱度に掛かる総倍率）。</summary>
        public readonly float disorderScale;
        /// <summary>混乱度→損害の効きの指数（0.5＝平方根で穏やか／1.0＝線形）。Pow に渡す。</summary>
        public readonly float damageExponent;
        /// <summary>深追い距離1単位あたりの反撃リスク増分。</summary>
        public readonly float overpursuitDepthScale;
        /// <summary>反撃リスクの上限（深追いでもこれ以上には上げない＝暴走防止）。</summary>
        public readonly float maxOverpursuitRisk;
        /// <summary>追撃で大損害を与えられる混乱度の閾値（これを超えた敗走は追撃可能）。</summary>
        public readonly float routThreshold;
        /// <summary>逃走距離1単位あたりの結束喪失の基準量（統率で軽減される前）。</summary>
        public readonly float cohesionDistanceScale;

        public PursuitBattleParams(
            float disorderScale,
            float damageExponent,
            float overpursuitDepthScale,
            float maxOverpursuitRisk,
            float routThreshold,
            float cohesionDistanceScale)
        {
            this.disorderScale = Mathf.Max(0f, disorderScale);
            this.damageExponent = Mathf.Clamp(damageExponent, 0f, 4f);
            this.overpursuitDepthScale = Mathf.Max(0f, overpursuitDepthScale);
            this.maxOverpursuitRisk = Mathf.Clamp01(maxOverpursuitRisk);
            this.routThreshold = Mathf.Clamp01(routThreshold);
            this.cohesionDistanceScale = Mathf.Max(0f, cohesionDistanceScale);
        }

        /// <summary>
        /// 既定：損害係数1.0・指数0.5（平方根）・深追いリスク0.1/単位（上限0.9）・敗走閾値0.5・結束喪失0.05/単位。
        /// </summary>
        public static PursuitBattleParams Default => new PursuitBattleParams(
            DefaultDisorderScale,
            DefaultDamageExponent,
            DefaultOverpursuitDepthScale,
            DefaultMaxOverpursuitRisk,
            DefaultRoutThreshold,
            DefaultCohesionDistanceScale);

        public const float DefaultDisorderScale = 1.0f;
        public const float DefaultDamageExponent = 0.5f;
        public const float DefaultOverpursuitDepthScale = 0.1f;
        public const float DefaultMaxOverpursuitRisk = 0.9f;
        public const float DefaultRoutThreshold = 0.5f;
        public const float DefaultCohesionDistanceScale = 0.05f;
    }

    /// <summary>
    /// 追撃戦＝撤退する敵への追加損害の収支（#追撃戦・Core 純ロジック・test-first・盤面非依存）。
    /// 会戦で崩れて背走する側は秩序を失い、追撃を受けると正面戦闘より大きな損害を被る。
    /// だが深追いは追撃側も伸びきり、敗走を装った伏兵・温存された予備への反撃を招く（収支の綱引き）。
    /// <b>撤退方向や離脱判断の <see cref="BattleWithdrawalRules"/>、捨てがまり＝殿(しんがり)の <see cref="SutegamariRules"/> とは別系統</b>＝
    /// 本ルールは「追撃側の損害収支（与える損害 vs 深追いの反撃リスク）」に特化する。
    /// 各メソッドは Params 明示版と既定 Params 委譲版を持つ。入力はクランプし、乱数は使わない（必要な確率は呼び出し側が roll を渡す）。
    /// </summary>
    public static class PursuitBattleRules
    {
        // --- 敗走時の混乱度 ---

        /// <summary>
        /// 敗走時の混乱度（0..1）。士気崩壊が大きく・陣形の崩れが大きいほど混乱が深い。
        /// 士気崩壊を重め（0.6）、陣形崩れ（0.4）の加重和。背走は隊形を失うほど制御不能になる。
        /// </summary>
        public static float DisorderOnRout(float moraleCollapse, float formationIntegrityLoss)
        {
            float morale = Mathf.Clamp01(moraleCollapse);
            float formation = Mathf.Clamp01(formationIntegrityLoss);
            return Mathf.Clamp01(0.6f * morale + 0.4f * formation);
        }

        // --- 追撃で与える追加損害 ---

        /// <summary>既定パラメータで追撃損害を返す。</summary>
        public static float PursuitDamage(float pursuerStrength, float fleeingDisorder)
            => PursuitDamage(pursuerStrength, fleeingDisorder, PursuitBattleParams.Default);

        /// <summary>
        /// 追撃側戦力×敗走側の混乱度で与える追加損害。混乱が深いほど1兵力あたりの効きが増す（背走時の損害は正面戦闘より大）。
        /// `damage = pursuerStrength * pow(disorder, exponent) * disorderScale`。混乱0なら損害0。
        /// </summary>
        public static float PursuitDamage(float pursuerStrength, float fleeingDisorder, PursuitBattleParams p)
        {
            float strength = Mathf.Max(0f, pursuerStrength);
            float disorder = Mathf.Clamp01(fleeingDisorder);
            float disorderFactor = Mathf.Pow(disorder, p.damageExponent);
            return Mathf.Max(0f, strength * disorderFactor * p.disorderScale);
        }

        // --- 逃げ切れる割合 ---

        /// <summary>
        /// 足の速さ差で逃げ切れる割合（0..1）。敗走側が速いほど多く逃げ、追撃側が速いほど捕捉される。
        /// `escape = fleeingSpeed / (fleeingSpeed + pursuerSpeed)`。等速で0.5、追撃側速度0で全逃走。
        /// </summary>
        public static float EscapeFraction(float fleeingSpeed, float pursuerSpeed)
        {
            float fleeing = Mathf.Max(0f, fleeingSpeed);
            float pursuer = Mathf.Max(0f, pursuerSpeed);
            float total = fleeing + pursuer;
            if (total <= 0f) return 1f; // 双方停止＝接触せず逃げ切る
            return Mathf.Clamp01(fleeing / total);
        }

        // --- 深追いの反撃リスク ---

        /// <summary>既定パラメータで深追いの反撃リスクを返す。</summary>
        public static float OverpursuitRisk(float pursuitDepth)
            => OverpursuitRisk(pursuitDepth, PursuitBattleParams.Default);

        /// <summary>
        /// 深追いで伸びきる反撃リスク（0..1）。追撃距離が伸びるほど補給線・隊列が伸び、リスクが線形に上がる（上限あり）。
        /// `risk = clamp(depth * overpursuitDepthScale, 0, maxOverpursuitRisk)`。
        /// </summary>
        public static float OverpursuitRisk(float pursuitDepth, PursuitBattleParams p)
        {
            float depth = Mathf.Max(0f, pursuitDepth);
            return Mathf.Clamp(depth * p.overpursuitDepthScale, 0f, p.maxOverpursuitRisk);
        }

        // --- 伏兵・予備への反撃成立 ---

        /// <summary>
        /// 敗走を装った伏兵／温存された予備への反撃成立度（0..1）。深追いリスクと敵の予備規模の積。
        /// 伸びきっていても敵に予備が無ければ反撃は成立しない（積＝両方要る）。
        /// </summary>
        public static float CounterAmbushChance(float overpursuitRisk, float enemyReserve)
        {
            float risk = Mathf.Clamp01(overpursuitRisk);
            float reserve = Mathf.Clamp01(enemyReserve);
            return Mathf.Clamp01(risk * reserve);
        }

        // --- 追撃の正味利得 ---

        /// <summary>
        /// 追撃の正味利得＝与えた損害から深追いリスクぶんを差し引いた値。
        /// `gain = pursuitDamage * (1 - overpursuitRisk)`。リスクが高い深追いは得た損害が割り引かれる（過剰な追撃は割に合わない）。
        /// </summary>
        public static float PursuitGain(float pursuitDamage, float overpursuitRisk)
        {
            float damage = Mathf.Max(0f, pursuitDamage);
            float risk = Mathf.Clamp01(overpursuitRisk);
            return Mathf.Max(0f, damage * (1f - risk));
        }

        // --- 逃走による結束喪失 ---

        /// <summary>既定パラメータで逃走による結束喪失を返す。</summary>
        public static float CohesionLossFromFlight(float distance, float leadership)
            => CohesionLossFromFlight(distance, leadership, PursuitBattleParams.Default);

        /// <summary>
        /// 逃走距離で部隊が散る量（結束喪失）。距離に比例し、統率(0..100)が高いほど散逸を抑える（最大半減）。
        /// `loss = distance * cohesionDistanceScale * (1 - leadership/200)`（統率係数は0.5〜1.0でクランプ）。
        /// </summary>
        public static float CohesionLossFromFlight(float distance, float leadership, PursuitBattleParams p)
        {
            float dist = Mathf.Max(0f, distance);
            float lead = Mathf.Clamp(leadership, 0f, 100f);
            float leadershipFactor = Mathf.Clamp(1f - lead / 200f, 0.5f, 1f);
            return Mathf.Max(0f, dist * p.cohesionDistanceScale * leadershipFactor);
        }

        // --- 追撃可能判定 ---

        /// <summary>既定の敗走閾値で追撃可能かを返す。</summary>
        public static bool IsRoutPursuable(float fleeingDisorder)
            => IsRoutPursuable(fleeingDisorder, PursuitBattleParams.DefaultRoutThreshold);

        /// <summary>
        /// 混乱が閾値を超えた敗走か（true＝追撃で大損害を与えられる）。秩序ある後退（混乱低）は追撃しても割に合わない。
        /// </summary>
        public static bool IsRoutPursuable(float fleeingDisorder, float threshold)
        {
            float disorder = Mathf.Clamp01(fleeingDisorder);
            float t = Mathf.Clamp01(threshold);
            return disorder > t;
        }
    }
}
