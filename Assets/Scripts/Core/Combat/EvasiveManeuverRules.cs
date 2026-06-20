using UnityEngine;

namespace Ginei
{
    /// <summary>回避機動（不規則機動で被弾を避ける）の調整係数。</summary>
    public readonly struct EvasiveManeuverParams
    {
        /// <summary>機動性×不規則さ→回避効果(0..1)の換算係数。機動・不規則さとも 0..100 入力で 100×100×これ＝1.0 となる素。</summary>
        public readonly float effectivenessScale;
        /// <summary>回避効果が満点のときに削れる被弾割合の上限（0..1）。</summary>
        public readonly float maxHitReduction;
        /// <summary>回避効果→自分の射撃精度低下の換算（激しく振るほど狙えない）。</summary>
        public readonly float accuracyPenaltyScale;
        /// <summary>機動性1あたり・dtあたりの推進剤消費（激しい回避は燃料を食う）。</summary>
        public readonly float energyPerAgility;
        /// <summary>艦体規模の基準質量（この規模で回避ペナルティ0.5）。大型ほど鈍い。</summary>
        public readonly float sizeReferenceMass;

        public EvasiveManeuverParams(float effectivenessScale, float maxHitReduction,
                                     float accuracyPenaltyScale, float energyPerAgility, float sizeReferenceMass)
        {
            this.effectivenessScale = Mathf.Max(0f, effectivenessScale);
            this.maxHitReduction = Mathf.Clamp01(maxHitReduction);
            this.accuracyPenaltyScale = Mathf.Clamp01(accuracyPenaltyScale);
            this.energyPerAgility = Mathf.Max(0f, energyPerAgility);
            this.sizeReferenceMass = Mathf.Max(0.0001f, sizeReferenceMass);
        }

        /// <summary>既定＝換算0.0001・最大被弾減0.6・精度低下0.5・推進剤0.0002/agility/dt・基準質量100。</summary>
        public static EvasiveManeuverParams Default
            => new EvasiveManeuverParams(0.0001f, 0.6f, 0.5f, 0.0002f, 100f);
    }

    /// <summary>
    /// 回避機動（evasive maneuver）の純ロジック。直進すると的になる＝不規則な回避機動（jinking）で
    /// 敵の照準・弾道予測を外し被弾を減らす。だが回避中は自分の射撃精度も落ち、推進剤も食う
    /// （攻撃と回避のトレードオフ）。大型艦は的が大きく機動も鈍いので回避しにくい。
    /// <see cref="AccuracyRules"/>（命中計算・命中率の確定揺らぎ）とは別＝こちらは回避側の
    /// 被弾低減と射撃ペナルティのトレードオフを数値化する。実際の機動操作（移動・回頭）は
    /// Game 層の <c>FleetMovement</c> が担い、ここは盤面非依存の plain 引数・乱数なし（必要なら roll）。
    /// 実効値パターン（基準値非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EvasiveManeuverRules
    {
        /// <summary>
        /// 回避効果（0..1）＝機動性×不規則さ×換算係数。機敏で不規則なほど高い。入力は 0..100。
        /// どちらかが0なら回避にならない（積）。
        /// </summary>
        public static float EvasionEffectiveness(float maneuverAgility, float unpredictability, EvasiveManeuverParams p)
        {
            float agility = Mathf.Clamp(maneuverAgility, 0f, 100f);
            float unpred = Mathf.Clamp(unpredictability, 0f, 100f);
            return Mathf.Clamp01(agility * unpred * p.effectivenessScale);
        }

        public static float EvasionEffectiveness(float maneuverAgility, float unpredictability)
            => EvasionEffectiveness(maneuverAgility, unpredictability, EvasiveManeuverParams.Default);

        /// <summary>
        /// 被弾減（0..1）＝回避効果×最大被弾減×(1−敵の追尾精度)。敵の追尾が高いほど回避が効かない。
        /// 追尾精度は 0..100 入力。
        /// </summary>
        public static float IncomingHitReduction(float evasionEffectiveness, float enemyTrackingQuality, EvasiveManeuverParams p)
        {
            float eff = Mathf.Clamp01(evasionEffectiveness);
            float tracking = Mathf.Clamp(enemyTrackingQuality, 0f, 100f) / 100f;
            return Mathf.Clamp01(eff * p.maxHitReduction * (1f - tracking));
        }

        public static float IncomingHitReduction(float evasionEffectiveness, float enemyTrackingQuality)
            => IncomingHitReduction(evasionEffectiveness, enemyTrackingQuality, EvasiveManeuverParams.Default);

        /// <summary>
        /// 自分の射撃精度の低下（0..1）＝回避効果×精度低下係数。激しく回避するほど自分も狙えない
        /// （攻撃と回避のトレードオフ）。
        /// </summary>
        public static float OwnAccuracyPenalty(float evasionEffectiveness, EvasiveManeuverParams p)
            => Mathf.Clamp01(Mathf.Clamp01(evasionEffectiveness) * p.accuracyPenaltyScale);

        public static float OwnAccuracyPenalty(float evasionEffectiveness)
            => OwnAccuracyPenalty(evasionEffectiveness, EvasiveManeuverParams.Default);

        /// <summary>
        /// 回避に費やす推進剤（≥0）＝機動性×消費係数×dt。激しい回避ほど燃料/推進剤を食う。
        /// 機動性は 0..100 入力。
        /// </summary>
        public static float EvasionEnergyCost(float maneuverAgility, float dt, EvasiveManeuverParams p)
        {
            float agility = Mathf.Clamp(maneuverAgility, 0f, 100f);
            return agility * p.energyPerAgility * Mathf.Max(0f, dt);
        }

        public static float EvasionEnergyCost(float maneuverAgility, float dt)
            => EvasionEnergyCost(maneuverAgility, dt, EvasiveManeuverParams.Default);

        /// <summary>
        /// 攻撃／回避の振り分け（−1回避専念〜+1攻撃専念）。aggression(0..1)を中立0として線形写像。
        /// 0.5で拮抗(0)、0で全回避(−1)、1で全攻撃(+1)。
        /// </summary>
        public static float AttackVsEvadeBalance(float aggression)
            => Mathf.Clamp(Mathf.Clamp01(aggression) * 2f - 1f, -1f, 1f);

        /// <summary>
        /// ジンキング（弾道予測外し）の効き（0..1）＝不規則さ×(1−敵の弾道予測)。不規則で敵の予測が
        /// 低いほど外せる。入力は 0..100。敵が完全に予測すると0。
        /// </summary>
        public static float JinkingPattern(float unpredictability, float enemyPrediction)
        {
            float unpred = Mathf.Clamp(unpredictability, 0f, 100f) / 100f;
            float prediction = Mathf.Clamp(enemyPrediction, 0f, 100f) / 100f;
            return Mathf.Clamp01(unpred * (1f - prediction));
        }

        /// <summary>
        /// 艦体規模による回避ペナルティ（0..1）＝規模÷(規模＋基準質量)。大型艦は的が大きく機動も
        /// 鈍いので回避しにくい。基準質量で0.5、0で0。回避効果に (1−ペナルティ) を掛けて使う。
        /// </summary>
        public static float SizePenalty(float shipSize, EvasiveManeuverParams p)
        {
            float size = Mathf.Max(0f, shipSize);
            return Mathf.Clamp01(size / (size + p.sizeReferenceMass));
        }

        public static float SizePenalty(float shipSize)
            => SizePenalty(shipSize, EvasiveManeuverParams.Default);

        /// <summary>有効に回避できているか＝回避効果が閾値以上。</summary>
        public static bool IsEvading(float evasionEffectiveness, float threshold)
            => Mathf.Clamp01(evasionEffectiveness) >= threshold;

        /// <summary>閾値0.3で回避成立を判定。</summary>
        public static bool IsEvading(float evasionEffectiveness)
            => IsEvading(evasionEffectiveness, 0.3f);
    }
}
