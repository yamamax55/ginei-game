using UnityEngine;

namespace Ginei
{
    /// <summary>野戦築城・二重包囲の調整係数（GAL-4・カエサル＝アレシア）。</summary>
    public readonly struct MilitaryEngineeringParams
    {
        /// <summary>工兵1人あたりの基礎築城速度（per dt の進捗寄与の素）。</summary>
        public readonly float speedPerEngineer;
        /// <summary>地形難易度1あたりの速度減衰（難所ほど遅い）。</summary>
        public readonly float terrainPenalty;
        /// <summary>包囲線の基準長＝この長さで強度が等倍。長いほど薄まる基準。</summary>
        public readonly float referenceLineLength;
        /// <summary>二線へ兵を割く負担係数（薄く伸ばすほど脆くなる強さ）。</summary>
        public readonly float divisionStrainFactor;

        public MilitaryEngineeringParams(float speedPerEngineer, float terrainPenalty,
                                         float referenceLineLength, float divisionStrainFactor)
        {
            this.speedPerEngineer = Mathf.Max(0f, speedPerEngineer);
            this.terrainPenalty = Mathf.Max(0f, terrainPenalty);
            this.referenceLineLength = Mathf.Max(0.0001f, referenceLineLength);
            this.divisionStrainFactor = Mathf.Max(0f, divisionStrainFactor);
        }

        /// <summary>既定＝工兵速度0.01・地形減衰0.5・基準線長10・割兵負担0.5。</summary>
        public static MilitaryEngineeringParams Default
            => new MilitaryEngineeringParams(0.01f, 0.5f, 10f, 0.5f);
    }

    /// <summary>対内包囲線（contravallation＝包囲した敵を閉じ込める内向きの線）の純データ。</summary>
    public readonly struct Contravallation
    {
        /// <summary>線の長さ。</summary>
        public readonly float lineLength;
        /// <summary>築城進捗（0..1）。</summary>
        public readonly float fortProgress;
        /// <summary>守備に就いた兵力。</summary>
        public readonly float manned;

        public Contravallation(float lineLength, float fortProgress, float manned)
        {
            this.lineLength = Mathf.Max(0f, lineLength);
            this.fortProgress = Mathf.Clamp01(fortProgress);
            this.manned = Mathf.Max(0f, manned);
        }
    }

    /// <summary>
    /// 野戦工兵速度＋アレシア型二重包囲の純ロジック（GAL-4・カエサル）。工兵が堡塁線を築く速度と、
    /// 対内包囲線（contravallation＝包囲した敵を閉じ込める内向きの線）と対外包囲線
    /// （circumvallation＝救援軍を防ぐ外向きの線）の二重包囲を数値化する。アレシアの戦いで
    /// カエサルは内外二重の防御線で包囲軍と救援軍を同時に相手取った。長い線ほど兵が薄まり強度が落ち、
    /// 二線に兵を割くほど脆くなる。両線とも保てば二重包囲が成立する。
    /// 包囲度・降伏の抽象は <see cref="EncirclementRules"/>（部隊規模の包囲解決）とは別＝こちらは
    /// 野戦築城の速度と二重包囲線の数値モデル。惑星攻城の制空権/侵略は <see cref="PlanetSiegeRules"/>
    /// （恒星間の攻城）、恒久回廊要塞は <see cref="FortressRules"/>（イゼルローン型）が担い、
    /// ここは臨時の野戦堡塁線のみ。盤面非依存の plain 引数・乱数は roll で決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MilitaryEngineeringRules
    {
        /// <summary>
        /// 築城速度＝工兵数×工兵速度÷(1＋地形難易度×地形減衰)。難所ほど遅く、人海で速まる。
        /// </summary>
        public static float EngineeringSpeed(int engineerCount, float terrainDifficulty, MilitaryEngineeringParams p)
        {
            float engineers = Mathf.Max(0, engineerCount);
            float diff = Mathf.Max(0f, terrainDifficulty);
            return engineers * p.speedPerEngineer / (1f + diff * p.terrainPenalty);
        }

        public static float EngineeringSpeed(int engineerCount, float terrainDifficulty)
            => EngineeringSpeed(engineerCount, terrainDifficulty, MilitaryEngineeringParams.Default);

        /// <summary>築城の進捗（0..1）＝現在進捗＋築城速度×dt（時間で堡塁が積み上がる）。</summary>
        public static float FortificationProgress(float engineeringSpeed, float dt, float currentProgress)
        {
            float advance = Mathf.Max(0f, engineeringSpeed) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(currentProgress) + advance);
        }

        /// <summary>
        /// 線の長さ希釈係数（0..1）＝基準線長÷max(基準線長, 実線長)。基準以下は1.0、長いほど薄まる。
        /// </summary>
        public static float LineLengthDilution(float lineLength, MilitaryEngineeringParams p)
        {
            float len = Mathf.Max(0f, lineLength);
            return p.referenceLineLength / Mathf.Max(p.referenceLineLength, len);
        }

        public static float LineLengthDilution(float lineLength)
            => LineLengthDilution(lineLength, MilitaryEngineeringParams.Default);

        /// <summary>
        /// 対内包囲線の強度（0..1）＝築城進捗×線長希釈。長い線ほど薄まり、進捗が低いほど弱い。
        /// </summary>
        public static float ContravallationStrength(float fortProgress, float lineLength, MilitaryEngineeringParams p)
        {
            return Mathf.Clamp01(fortProgress) * LineLengthDilution(lineLength, p);
        }

        public static float ContravallationStrength(float fortProgress, float lineLength)
            => ContravallationStrength(fortProgress, lineLength, MilitaryEngineeringParams.Default);

        /// <summary>
        /// 対外包囲線の強度（0..1・救援軍を防ぐ外向きの線）＝築城進捗×線長希釈。算式は対内線と同型。
        /// </summary>
        public static float CircumvallationStrength(float fortProgress, float lineLength, MilitaryEngineeringParams p)
        {
            return Mathf.Clamp01(fortProgress) * LineLengthDilution(lineLength, p);
        }

        public static float CircumvallationStrength(float fortProgress, float lineLength)
            => CircumvallationStrength(fortProgress, lineLength, MilitaryEngineeringParams.Default);

        /// <summary>
        /// 二線に兵を割く負担（0..1）＝守備すべき総線長×割兵負担÷守備兵力。薄く伸ばすほど大きい。
        /// 兵力0は最大負担1。
        /// </summary>
        public static float ForceDivisionStrain(float garrisonSize, float twoLineLength, MilitaryEngineeringParams p)
        {
            float garrison = Mathf.Max(0f, garrisonSize);
            float demand = Mathf.Max(0f, twoLineLength) * p.divisionStrainFactor;
            if (garrison <= 0f) return demand > 0f ? 1f : 0f;
            return Mathf.Clamp01(demand / garrison);
        }

        public static float ForceDivisionStrain(float garrisonSize, float twoLineLength)
            => ForceDivisionStrain(garrisonSize, twoLineLength, MilitaryEngineeringParams.Default);

        /// <summary>
        /// 二重包囲の維持余力（0..1）＝両線強度の小さい方×(1−割兵負担)。弱い線と兵の薄まりが律速。
        /// 兵が二線に割かれるほど同時維持が難しい。
        /// </summary>
        public static float DoubleSiegeViability(float contravallation, float circumvallation,
                                                 float garrisonSize, float twoLineLength, MilitaryEngineeringParams p)
        {
            float weaker = Mathf.Min(Mathf.Clamp01(contravallation), Mathf.Clamp01(circumvallation));
            float strain = ForceDivisionStrain(garrisonSize, twoLineLength, p);
            return Mathf.Clamp01(weaker * (1f - strain));
        }

        public static float DoubleSiegeViability(float contravallation, float circumvallation,
                                                 float garrisonSize, float twoLineLength)
            => DoubleSiegeViability(contravallation, circumvallation, garrisonSize, twoLineLength,
                                    MilitaryEngineeringParams.Default);

        /// <summary>
        /// 対外線が救援軍を撥ね返す余力（−1..1）＝対外線強度−正規化した救援軍圧。
        /// 正なら撥ね返し、負なら突破される。救援軍圧は強度に対する救援戦力の比で 0..1 に飽和。
        /// </summary>
        public static float ReliefArmyRepulse(float circumvallationStrength, float reliefArmyStrength, float defenderStrength)
        {
            float strength = Mathf.Clamp01(circumvallationStrength);
            float defender = Mathf.Max(0.0001f, defenderStrength);
            float pressure = Mathf.Clamp01(Mathf.Max(0f, reliefArmyStrength) / defender);
            return Mathf.Clamp(strength - pressure, -1f, 1f);
        }

        /// <summary>
        /// 対内線が包囲された敵の突囲を抑える余力（−1..1）＝対内線強度−正規化した突囲圧。
        /// 正なら閉じ込め、負なら突破される。算式は対外線と同型（守備側＝包囲軍）。
        /// </summary>
        public static float BesiegedBreakoutResistance(float contravallationStrength, float besiegedStrength, float defenderStrength)
        {
            float strength = Mathf.Clamp01(contravallationStrength);
            float defender = Mathf.Max(0.0001f, defenderStrength);
            float pressure = Mathf.Clamp01(Mathf.Max(0f, besiegedStrength) / defender);
            return Mathf.Clamp(strength - pressure, -1f, 1f);
        }

        /// <summary>
        /// 二重包囲が成立しているか＝対外線が救援を撥ね返し（余力≥閾値）かつ対内線が突囲を抑える
        /// （余力≥閾値）。片方でも破られればアレシアの二重包囲は崩れる。
        /// </summary>
        public static bool IsDoubleSiegeHolding(float reliefRepulse, float breakoutResist, float threshold)
        {
            return reliefRepulse >= threshold && breakoutResist >= threshold;
        }

        /// <summary>閾値0（両線とも撥ね返し優位）で二重包囲成立を判定。</summary>
        public static bool IsDoubleSiegeHolding(float reliefRepulse, float breakoutResist)
            => IsDoubleSiegeHolding(reliefRepulse, breakoutResist, 0f);
    }
}
