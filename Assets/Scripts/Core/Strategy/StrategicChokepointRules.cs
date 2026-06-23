using UnityEngine;

namespace Ginei
{
    /// <summary>戦略的隘路（イゼルローン回廊・フェザーン回廊型の戦略的要衝）の地政学的価値の調整係数。</summary>
    public readonly struct StrategicChokepointParams
    {
        /// <summary>狭さ×通行困難→通行の制約のスケール。</summary>
        public readonly float constraintScale;
        /// <summary>制約×迂回不能→迂回不能による価値のスケール。</summary>
        public readonly float irreplaceabilityScale;
        /// <summary>価値×支配力→隘路を握る優位のスケール。</summary>
        public readonly float holdingScale;
        /// <summary>隣接勢力×係争度→接点の緊張のスケール。</summary>
        public readonly float tensionScale;
        /// <summary>制約×要塞化適性→防御の適性のスケール。</summary>
        public readonly float defensiveScale;
        /// <summary>握る優位×緊張→攻防の激しさのスケール。</summary>
        public readonly float contestScale;
        /// <summary>価値×優位→地政学的てこのスケール。</summary>
        public readonly float leverageScale;
        /// <summary>てこ×防御適性→戦略的決定性のスケール（出力は0..1へ飽和）。</summary>
        public readonly float decisivenessScale;

        public StrategicChokepointParams(float constraintScale, float irreplaceabilityScale,
                                         float holdingScale, float tensionScale, float defensiveScale,
                                         float contestScale, float leverageScale, float decisivenessScale)
        {
            this.constraintScale = Mathf.Max(0f, constraintScale);
            this.irreplaceabilityScale = Mathf.Max(0f, irreplaceabilityScale);
            this.holdingScale = Mathf.Max(0f, holdingScale);
            this.tensionScale = Mathf.Max(0f, tensionScale);
            this.defensiveScale = Mathf.Max(0f, defensiveScale);
            this.contestScale = Mathf.Max(0f, contestScale);
            this.leverageScale = Mathf.Max(0f, leverageScale);
            this.decisivenessScale = Mathf.Max(0f, decisivenessScale);
        }

        /// <summary>既定＝全係数1.0。</summary>
        public static StrategicChokepointParams Default
            => new StrategicChokepointParams(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 戦略的隘路（チョークポイント）の地政学的価値の純ロジック（イゼルローン回廊・フェザーン回廊型）。
    /// 星間航路の狭所そのものが持つ「動かせない地形の価値」を扱う＝通行の制約、迂回不能性がもたらす価値、
    /// その隘路を握る側の優位、両勢力の接点としての緊張、防御に向く地形適性、隘路を巡る攻防の激しさ、
    /// 地政学的てこ、そして隘路がどれだけ戦略の帰趨を決めるか（決定性）。
    /// <see cref="CorridorControlRules"/>（回廊の戦略支配＝補給線・通行料・封鎖）／
    /// <see cref="ChokeholdBattleRules"/>（隘路の会戦戦術＝狭所での正面制限）とは別＝こちらは
    /// **戦略的隘路そのものの地政学的価値**（地形が動かないことに由来する不変の重み）。
    /// 盤面非依存の plain 引数・入力クランプ（0..1正規化）・実効値パターン・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class StrategicChokepointRules
    {
        /// <summary>戦略的決定性とみなす既定閾値（これ以上なら隘路が戦局を左右する要衝）。</summary>
        public const float DefaultDecisiveThreshold = 0.5f;

        /// <summary>
        /// 通行の制約（0..1）＝(1-回廊の広さ corridorWidth) × 通行困難 transitDifficulty × 制約係数。
        /// 回廊が狭く（広さが小さい）通行が困難なほど制約は強い。両入力は0..1へクランプ。
        /// </summary>
        public static float PassageConstraint(float corridorWidth, float transitDifficulty,
                                              StrategicChokepointParams p)
        {
            float narrowness = 1f - Mathf.Clamp01(corridorWidth);
            float difficulty = Mathf.Clamp01(transitDifficulty);
            return Mathf.Clamp01(p.constraintScale * narrowness * difficulty);
        }

        public static float PassageConstraint(float corridorWidth, float transitDifficulty)
            => PassageConstraint(corridorWidth, transitDifficulty, StrategicChokepointParams.Default);

        /// <summary>
        /// 迂回不能による価値（0..1）＝通行の制約 passageConstraint × (1-迂回路 alternativeRoutes) × 係数。
        /// 制約が強く迂回路が少ないほど「代替の利かない隘路」として価値が高い。両入力は0..1へクランプ。
        /// </summary>
        public static float IrreplaceabilityValue(float passageConstraint, float alternativeRoutes,
                                                  StrategicChokepointParams p)
        {
            float constraint = Mathf.Clamp01(passageConstraint);
            float noBypass = 1f - Mathf.Clamp01(alternativeRoutes);
            return Mathf.Clamp01(p.irreplaceabilityScale * constraint * noBypass);
        }

        public static float IrreplaceabilityValue(float passageConstraint, float alternativeRoutes)
            => IrreplaceabilityValue(passageConstraint, alternativeRoutes, StrategicChokepointParams.Default);

        /// <summary>
        /// 隘路を握る優位（0..1）＝迂回不能価値 irreplaceabilityValue × 支配力 controlStrength × 係数。
        /// 価値ある隘路を強く支配しているほど優位は大きい。両入力は0..1へクランプ。
        /// </summary>
        public static float HoldingAdvantage(float irreplaceabilityValue, float controlStrength,
                                             StrategicChokepointParams p)
        {
            float value = Mathf.Clamp01(irreplaceabilityValue);
            float control = Mathf.Clamp01(controlStrength);
            return Mathf.Clamp01(p.holdingScale * value * control);
        }

        public static float HoldingAdvantage(float irreplaceabilityValue, float controlStrength)
            => HoldingAdvantage(irreplaceabilityValue, controlStrength, StrategicChokepointParams.Default);

        /// <summary>
        /// 接点の緊張（0..1）＝隣接勢力数 adjacentPowers の正規化 × 係争度 contestedStatus × 係数。
        /// 隣接勢力が多く（飽和的に効く）係争状態にあるほど両勢力の接点としての緊張が高い。
        /// adjacentPowers≥0、正規化は n/(1+n)。contestedStatus は0..1へクランプ。
        /// </summary>
        public static float FrontierTension(float adjacentPowers, float contestedStatus,
                                            StrategicChokepointParams p)
        {
            float powers = Mathf.Max(0f, adjacentPowers);
            float powerFactor = powers / (1f + powers);
            float contested = Mathf.Clamp01(contestedStatus);
            return Mathf.Clamp01(p.tensionScale * powerFactor * contested);
        }

        public static float FrontierTension(float adjacentPowers, float contestedStatus)
            => FrontierTension(adjacentPowers, contestedStatus, StrategicChokepointParams.Default);

        /// <summary>
        /// 防御の適性（0..1）＝通行の制約 passageConstraint × 要塞化適性 fortifiability × 係数。
        /// 狭く通りにくい地形ほど、また要塞を築きやすいほど守りに向く（イゼルローン要塞型）。両入力は0..1へクランプ。
        /// </summary>
        public static float DefensiveSuitability(float passageConstraint, float fortifiability,
                                                 StrategicChokepointParams p)
        {
            float constraint = Mathf.Clamp01(passageConstraint);
            float fort = Mathf.Clamp01(fortifiability);
            return Mathf.Clamp01(p.defensiveScale * constraint * fort);
        }

        public static float DefensiveSuitability(float passageConstraint, float fortifiability)
            => DefensiveSuitability(passageConstraint, fortifiability, StrategicChokepointParams.Default);

        /// <summary>
        /// 隘路を巡る攻防の激しさ（0..1）＝握る優位 holdingAdvantage × 接点の緊張 frontierTension × 係数。
        /// 握る側の優位が大きく接点の緊張が高いほど、奪い合いが激化する。両入力は0..1へクランプ。
        /// </summary>
        public static float ContestIntensity(float holdingAdvantage, float frontierTension,
                                             StrategicChokepointParams p)
        {
            float advantage = Mathf.Clamp01(holdingAdvantage);
            float tension = Mathf.Clamp01(frontierTension);
            return Mathf.Clamp01(p.contestScale * advantage * tension);
        }

        public static float ContestIntensity(float holdingAdvantage, float frontierTension)
            => ContestIntensity(holdingAdvantage, frontierTension, StrategicChokepointParams.Default);

        /// <summary>
        /// 地政学的てこ（0..1）＝迂回不能価値 irreplaceabilityValue × 握る優位 holdingAdvantage × 係数。
        /// 代替の利かない価値と確実な支配が揃うほど、外交・進攻で相手に効かせられる圧力（てこ）が大きい。
        /// 両入力は0..1へクランプ。
        /// </summary>
        public static float GeopoliticalLeverage(float irreplaceabilityValue, float holdingAdvantage,
                                                 StrategicChokepointParams p)
        {
            float value = Mathf.Clamp01(irreplaceabilityValue);
            float advantage = Mathf.Clamp01(holdingAdvantage);
            return Mathf.Clamp01(p.leverageScale * value * advantage);
        }

        public static float GeopoliticalLeverage(float irreplaceabilityValue, float holdingAdvantage)
            => GeopoliticalLeverage(irreplaceabilityValue, holdingAdvantage, StrategicChokepointParams.Default);

        /// <summary>
        /// 隘路の戦略的決定性（0..1）＝地政学的てこ leverage と防御適性 defensiveSuitability の相乗平均×係数。
        /// てこ（攻めの価値）と防御適性（守りの堅さ）の両方が高い隘路ほど、戦局の帰趨を決める要衝になる。
        /// 相乗平均（幾何平均）ゆえ片方が低いと決定性は伸びない（攻守のどちらかが欠ければ要衝にならない）。
        /// 両入力は0..1へクランプ。
        /// </summary>
        public static float ChokepointDecisiveness(float geopoliticalLeverage, float defensiveSuitability,
                                                   StrategicChokepointParams p)
        {
            float leverage = Mathf.Clamp01(geopoliticalLeverage);
            float defensive = Mathf.Clamp01(defensiveSuitability);
            float geomean = Mathf.Sqrt(leverage * defensive);
            return Mathf.Clamp01(p.decisivenessScale * geomean);
        }

        public static float ChokepointDecisiveness(float geopoliticalLeverage, float defensiveSuitability)
            => ChokepointDecisiveness(geopoliticalLeverage, defensiveSuitability, StrategicChokepointParams.Default);

        /// <summary>
        /// 戦局を左右する要衝か＝戦略的決定性 decisiveness が閾値 threshold 以上なら true。
        /// </summary>
        public static bool IsDecisiveChokepoint(float decisiveness, float threshold)
            => Mathf.Clamp01(decisiveness) >= Mathf.Clamp01(threshold);

        public static bool IsDecisiveChokepoint(float decisiveness)
            => IsDecisiveChokepoint(decisiveness, DefaultDecisiveThreshold);
    }
}
