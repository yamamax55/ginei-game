using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 文化・民族・ナショナリズムの調整係数（#194・宗教 #172 の姉妹）。Params 構造体に集約（既定 .Default）。
    /// </summary>
    public readonly struct CultureParams
    {
        /// <summary>同化圧力の基準（平時の多数派同化への引き）。</summary>
        public readonly float baseAssimilationPressure;
        /// <summary>戦時は同化圧力が下がる（恨み・分断）。引かれる量。</summary>
        public readonly float warAssimilationPenalty;
        /// <summary>同化が均衡へ寄る速さ（/戦略秒）。</summary>
        public readonly float assimilationSpeed;
        /// <summary>分離独立リスクがこの安定度を下回ると立ち上がる基準（0..100）。</summary>
        public readonly float separatismStabilityThreshold;
        /// <summary>ナショナリズム係数の最大上振れ（同化0＝民族意識最大での結束/士気ボーナス）。</summary>
        public readonly float nationalismMaxBonus;
        /// <summary>亡命傾向の最大確率（抑圧最大でこの値に達する）。</summary>
        public readonly float exileMaxChance;

        public CultureParams(float baseAssimilationPressure, float warAssimilationPenalty, float assimilationSpeed,
            float separatismStabilityThreshold, float nationalismMaxBonus, float exileMaxChance)
        {
            this.baseAssimilationPressure = baseAssimilationPressure;
            this.warAssimilationPenalty = warAssimilationPenalty;
            this.assimilationSpeed = assimilationSpeed;
            this.separatismStabilityThreshold = separatismStabilityThreshold;
            this.nationalismMaxBonus = nationalismMaxBonus;
            this.exileMaxChance = exileMaxChance;
        }

        public static CultureParams Default => new CultureParams(0.7f, 0.4f, 0.04f, 40f, 0.3f, 0.6f);
    }

    /// <summary>
    /// 文化・民族・ナショナリズムの数値解決（#194・純ロジック test-first）。地理/歴史から創発する民族の
    /// 同化・分離独立・亡命を扱う唯一の窓口。<see cref="GovernanceRules"/>(内政)の文化版・姉妹ロジック。
    /// 同化度は「目標値へ時間で収束」する：多数派と一致するほど同化圧力が高く、戦時は下がる。
    /// 同化が低い少数民族は安定が割れると分離独立リスクが高まり、ナショナリズムが結束/士気の実効係数として効く。
    /// 乱数は決定論のため roll(0..1) を引数で受ける（自前で引かない）。調整値は <see cref="CultureParams"/> に集約。
    /// </summary>
    public static class CultureRules
    {
        // --- 調整値（マジックナンバー禁止＝const に集約） ---
        public const float NoPressure = 0f;           // 多数派一致/非少数民族＝同化圧なし
        public const float NoSeparatism = 0f;         // 分離独立リスクの下限
        public const float NoExile = 0f;              // 亡命傾向の下限
        public const float FullAssimilation = 1f;     // 同化完了＝多数派一致時の収束先

        /// <summary>
        /// 多数派文化への同化圧力(0..1)。占領直後（同化低）ほど圧力が高く、時間で同化が進むと圧力は下がる。
        /// 戦時は分断・恨みで圧力が下がる。非少数民族（多数派側）には圧力なし。
        /// </summary>
        public static float AssimilationPressure(Culture minorityCulture, string dominantCulture, bool atWar, CultureParams p)
        {
            if (minorityCulture == null || !minorityCulture.isMinority) return NoPressure;
            // すでに多数派と同じ文化なら同化の余地なし
            if (!string.IsNullOrEmpty(dominantCulture) && minorityCulture.cultureName == dominantCulture) return NoPressure;

            // 未同化ぶん(1-assimilation)に比例して圧力が立つ＝占領直後ほど高い
            float pressure = p.baseAssimilationPressure * (1f - Mathf.Clamp01(minorityCulture.assimilation));
            if (atWar) pressure -= p.warAssimilationPenalty;
            return Mathf.Clamp01(pressure);
        }

        public static float AssimilationPressure(Culture minorityCulture, string dominantCulture, bool atWar)
            => AssimilationPressure(minorityCulture, dominantCulture, atWar, CultureParams.Default);

        /// <summary>
        /// 1tick の同化更新：多数派と一致する間だけ同化度を 1 へ寄せる（戦略時間に dt 比例＝timeScale 追従）。
        /// 戦時は速度が鈍る（同化圧力が下がるぶん）。不一致（多数派でない）なら同化は進まない。
        /// </summary>
        public static void Tick(Culture c, bool dominantCultureMatch, bool atWar, float deltaTime, CultureParams p)
        {
            if (c == null || deltaTime <= 0f) return;
            if (!dominantCultureMatch) return; // 多数派支配下でなければ同化は進まない

            // 戦時は同化が遅い（圧力低下を速度に反映）。基準値（assimilationSpeed）は非破壊＝実効速度をローカル計算。
            float effectiveSpeed = p.assimilationSpeed;
            if (atWar) effectiveSpeed *= Mathf.Clamp01(1f - p.warAssimilationPenalty);
            c.assimilation = Mathf.Clamp01(c.assimilation + effectiveSpeed * deltaTime);
        }

        public static void Tick(Culture c, bool dominantCultureMatch, bool atWar, float deltaTime)
            => Tick(c, dominantCultureMatch, atWar, deltaTime, CultureParams.Default);

        /// <summary>
        /// 少数民族の分離独立リスク(0..1)。同化が低く（民族意識が強く）安定が低いほど高い
        /// （<see cref="GovernanceRules.RebelPressure"/> 風）。非少数民族・しきい値以上の安定では 0。
        /// </summary>
        public static float SeparatismRisk(Culture c, float stability, CultureParams p)
        {
            if (c == null || !c.isMinority) return NoSeparatism;
            if (stability >= p.separatismStabilityThreshold) return NoSeparatism;

            // 安定の低さ(0..1)×未同化ぶん(0..1)＝両方が低いほど分離独立に傾く
            float instability = Mathf.Clamp01((p.separatismStabilityThreshold - stability) / p.separatismStabilityThreshold);
            float disaffection = 1f - Mathf.Clamp01(c.assimilation);
            return Mathf.Clamp01(instability * disaffection);
        }

        public static float SeparatismRisk(Culture c, float stability)
            => SeparatismRisk(c, stability, CultureParams.Default);

        /// <summary>
        /// ナショナリズムが結束/士気に与える実効係数（基準1.0＝係数倍率）。同化が低い少数民族ほど民族意識が高く、
        /// 防衛戦のような結束ボーナスが大きい（実効値パターン＝基準非破壊。返り値を士気/結束へ掛ける）。
        /// 非少数民族・完全同化では 1.0（中立）。
        /// </summary>
        public static float NationalismFactor(Culture c, CultureParams p)
        {
            if (c == null || !c.isMinority) return 1f;
            // 未同化ぶん(0..1)に比例してナショナリズムが立つ
            float fervor = 1f - Mathf.Clamp01(c.assimilation);
            return 1f + p.nationalismMaxBonus * fervor;
        }

        public static float NationalismFactor(Culture c) => NationalismFactor(c, CultureParams.Default);

        /// <summary>
        /// 亡命したか（決定論・roll で判定）。抑圧(0..1)が強いほど亡命傾向が高く、未同化な少数民族ほど傾きやすい。
        /// 亡命確率は exileMaxChance を上限にクランプし、roll がそれ未満なら亡命成立（true）。
        /// </summary>
        public static bool ExileLikelihood(Culture c, float oppression, float roll, CultureParams p)
        {
            if (c == null) return false;
            float chance = p.exileMaxChance * Mathf.Clamp01(oppression);
            // 未同化な少数民族ほど亡命に傾く（多数派/同化済みは留まりやすい）
            if (c.isMinority) chance *= 1f - Mathf.Clamp01(c.assimilation);
            else chance = NoExile;
            chance = Mathf.Clamp01(chance);
            return roll < chance;
        }

        public static bool ExileLikelihood(Culture c, float oppression, float roll)
            => ExileLikelihood(c, oppression, roll, CultureParams.Default);
    }
}
