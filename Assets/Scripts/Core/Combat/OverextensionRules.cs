using UnityEngine;

namespace Ginei
{
    /// <summary>過剰拡張の調整係数。</summary>
    public readonly struct OverextensionParams
    {
        /// <summary>版図規模が公約負担へ寄与する重み（星系1つあたりの負担）。</summary>
        public readonly float territoryWeight;
        /// <summary>前線長が公約負担へ寄与する重み（守るべき正面1単位あたりの負担）。</summary>
        public readonly float frontierWeight;
        /// <summary>防衛公約（条約義務 0..1）が負担を増幅する重み。</summary>
        public readonly float treatyWeight;
        /// <summary>過伸張ペナルティの非線形度（超過比の冪指数・1以上）。守るものが増えるほど加速して薄まる。</summary>
        public readonly float overstretchExponent;
        /// <summary>過伸張ペナルティの最大値（0..1・各地が手薄になる上限）。</summary>
        public readonly float maxOverstretchPenalty;
        /// <summary>帝国衰退の速度（per dt・超過比に掛かる＝軍事費が経済を圧迫する率）。</summary>
        public readonly float imperialDecayRate;
        /// <summary>戦略的収縮の回復速度（per dt・収縮幅×超過の余地に掛かる）。</summary>
        public readonly float retrenchmentRate;

        public OverextensionParams(float territoryWeight, float frontierWeight, float treatyWeight,
            float overstretchExponent, float maxOverstretchPenalty,
            float imperialDecayRate, float retrenchmentRate)
        {
            this.territoryWeight = Mathf.Max(0f, territoryWeight);
            this.frontierWeight = Mathf.Max(0f, frontierWeight);
            this.treatyWeight = Mathf.Max(0f, treatyWeight);
            this.overstretchExponent = Mathf.Max(1f, overstretchExponent);
            this.maxOverstretchPenalty = Mathf.Clamp01(maxOverstretchPenalty);
            this.imperialDecayRate = Mathf.Max(0f, imperialDecayRate);
            this.retrenchmentRate = Mathf.Max(0f, retrenchmentRate);
        }

        /// <summary>既定＝版図重み1・前線重み2・条約重み0.5・過伸張冪指数2・ペナルティ上限0.9・衰退率0.05・収縮率0.2。</summary>
        public static OverextensionParams Default => new OverextensionParams(1f, 2f, 0.5f, 2f, 0.9f, 0.05f, 0.2f);
    }

    /// <summary>
    /// 過剰拡張の純ロジック（ポール・ケネディ型＝帝国の興亡）。版図と防衛公約が国力を超えると、
    /// 守るものが増えるほど各地が手薄になって弱くなる＝戦略的収縮の決断を迫る。負担が国力を超えた
    /// 「超過」は国力そのものを蝕み（軍事費が経済を圧迫）、撤退は恥だが負担を下げて国力を回復させる
    /// （賢明な撤退）。「過伸張は撤退でしか治らない」を式に出す。
    /// 物流（<see cref="LogisticsRules"/>＝版図が回廊で繋がる一体化度）とは別系統＝こちらは
    /// 負担と国力の比（規模そのものの重さ）を扱う。攻勢限界点（CulminatingPointRules・バックログ＝
    /// 1作戦の進撃が伸びきる瞬間）とも別＝こちらは国家規模の恒常的な過伸張。
    /// 倍率は各係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OverextensionRules
    {
        /// <summary>
        /// 防衛公約の総負担＝版図規模×版図重み＋前線長×前線重み＋（1＋条約義務×条約重み）の増幅。
        /// 広い版図・長い前線・多くの同盟公約ほど守るべきものが増え、負担が膨らむ。
        /// </summary>
        public static float CommitmentBurden(float territorySize, float frontierLength, float treatyObligations,
            OverextensionParams p)
        {
            float t = Mathf.Max(0f, territorySize);
            float f = Mathf.Max(0f, frontierLength);
            float ob = Mathf.Clamp01(treatyObligations);
            float baseBurden = t * p.territoryWeight + f * p.frontierWeight;
            return baseBurden * (1f + ob * p.treatyWeight);
        }

        public static float CommitmentBurden(float territorySize, float frontierLength, float treatyObligations)
            => CommitmentBurden(territorySize, frontierLength, treatyObligations, OverextensionParams.Default);

        /// <summary>
        /// 負担/国力の比（0以上）。1超で過伸張＝守るべき負担が国力を上回る。国力ゼロ以下は
        /// 巨大な比（事実上の崩壊）として大きな値を返す。
        /// </summary>
        public static float BurdenToCapacityRatio(float burden, float nationalPower)
        {
            float b = Mathf.Max(0f, burden);
            float power = nationalPower;
            if (power <= 0f) return b > 0f ? 999f : 0f;
            return b / power;
        }

        /// <summary>
        /// 過伸張ペナルティ（0..maxOverstretchPenalty）。比1.0までは0（負担が国力内なら無傷）、
        /// 1を超えた超過分を冪で非線形に効かせる＝守るものが増えるほど各地が手薄になって弱くなる。
        /// 各係数に（1−これ）を掛けて使う。
        /// </summary>
        public static float OverstretchPenalty(float ratio, OverextensionParams p)
        {
            float r = Mathf.Max(0f, ratio);
            if (r <= 1f) return 0f;
            float excess = r - 1f; // 過伸張＝1を超えた分だけが効く
            float raw = Mathf.Pow(excess, p.overstretchExponent);
            return Mathf.Min(p.maxOverstretchPenalty, raw);
        }

        public static float OverstretchPenalty(float ratio)
            => OverstretchPenalty(ratio, OverextensionParams.Default);

        /// <summary>
        /// 帝国の衰退（1tick後の国力）。過伸張（比1超）の超過分が国力そのものを蝕む
        /// （軍事費が経済を圧迫）＝衰退率×超過×dt を減じる。比1.0以下なら衰退なし。
        /// </summary>
        public static float ImperialDecayTick(float power, float ratio, float dt, OverextensionParams p)
        {
            float current = Mathf.Max(0f, power);
            float r = Mathf.Max(0f, ratio);
            float t = Mathf.Max(0f, dt);
            if (r <= 1f) return current;
            float excess = r - 1f;
            return Mathf.Max(0f, current - p.imperialDecayRate * excess * t);
        }

        public static float ImperialDecayTick(float power, float ratio, float dt)
            => ImperialDecayTick(power, ratio, dt, OverextensionParams.Default);

        /// <summary>
        /// 戦略的収縮の効果（負担を下げる回復量・0以上）＝収縮率×収縮幅×超過の余地×dt。
        /// 過伸張しているほど（比1を大きく超えるほど）撤退の見返りが大きい＝賢明な撤退。
        /// 過伸張していない（比1.0以下）なら撤退しても得るものはない＝負担を減らすだけの恥。
        /// 返り値は「下げられる負担量」として CommitmentBurden から差し引いて使う。
        /// </summary>
        public static float StrategicRetrenchmentGain(float ratio, float retrenchmentScope, OverextensionParams p)
        {
            float r = Mathf.Max(0f, ratio);
            float scope = Mathf.Clamp01(retrenchmentScope);
            if (r <= 1f) return 0f; // 過伸張していないなら撤退に旨味なし
            float excess = r - 1f;
            return p.retrenchmentRate * scope * excess;
        }

        public static float StrategicRetrenchmentGain(float ratio, float retrenchmentScope)
            => StrategicRetrenchmentGain(ratio, retrenchmentScope, OverextensionParams.Default);

        /// <summary>
        /// 1正面（セクター）あたりの防御力＝国力/正面数。戦線が伸びる（正面数が増える）ほど
        /// 各所が薄くなる＝同じ国力でも守る場所が多いと1か所あたりは弱い。正面0以下は0。
        /// </summary>
        public static float DefensibilityPerSector(float nationalPower, float sectorCount)
        {
            float power = Mathf.Max(0f, nationalPower);
            if (sectorCount <= 0f) return 0f;
            return power / sectorCount;
        }
    }
}
