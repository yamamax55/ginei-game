using UnityEngine;

namespace Ginei
{
    /// <summary>土地改革の調整係数。</summary>
    public readonly struct LandReformParams
    {
        /// <summary>小作が自作農化したときの生産意欲向上の最大（全範囲・全小作社会の上限）。
        /// 自分の土地は熱心に耕す＝意欲のプレミアム。</summary>
        public readonly float incentiveScale;
        /// <summary>農民支持の獲得の最大（全範囲再分配時）。</summary>
        public readonly float supportScale;
        /// <summary>地主層の反発の最大（全範囲・無補償時）。</summary>
        public readonly float backlashScale;
        /// <summary>短期混乱の最大（全範囲・即時実施時＝現場が最も乱れる）。</summary>
        public readonly float disruptionScale;
        /// <summary>過度な細分化による効率損失の最大（全範囲・機械化ゼロ時）。
        /// 零細農地は機械化できない＝行き過ぎた平等は非効率。</summary>
        public readonly float fragmentationScale;

        public LandReformParams(float incentiveScale, float supportScale, float backlashScale,
                                float disruptionScale, float fragmentationScale)
        {
            this.incentiveScale = Mathf.Max(0f, incentiveScale);
            this.supportScale = Mathf.Max(0f, supportScale);
            this.backlashScale = Mathf.Max(0f, backlashScale);
            this.disruptionScale = Mathf.Max(0f, disruptionScale);
            this.fragmentationScale = Mathf.Max(0f, fragmentationScale);
        }

        /// <summary>既定＝意欲0.4・支持0.5・反発0.7・混乱0.4・細分化0.3。</summary>
        public static LandReformParams Default => new LandReformParams(0.4f, 0.5f, 0.7f, 0.4f, 0.3f);
    }

    /// <summary>
    /// 土地改革の純ロジック（地主の土地を小作へ再分配＝農地改革）。再分配は小作の生産意欲と
    /// 農民支持を「買い」、地主層の反発・短期の現場混乱・過度な細分化の非効率を「払う」。
    /// 核＝小作が多い社会ほど改革で買える意欲が大きい＝割に合う（自作農は熱心に耕す）が、
    /// 行き過ぎた平等は零細化で機械化を殺す＝意欲の利得を一部食う（NetOutput で相殺）。
    /// 身分そのものの解放（農奴→自由民の労働の質・忠誠の時間動態）は <see cref="SerfdomRules"/> が、
    /// 税の階級別負担（カネの再分配）は <see cref="RedistributionRules"/> が扱い、ここは
    /// 土地という資産の再分配＝意欲と効率の交換のみを扱う。倍率・係数は基準値に掛けて使う
    /// （実効値パターン・基準非破壊）。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class LandReformRules
    {
        /// <summary>
        /// 再分配による生産意欲の向上（0..incentiveScale）。reformScope（再分配の範囲 0..1）と
        /// tenancyRate（小作率 0..1）の積に比例＝小作が多い社会ほど改革の余地が大きい
        /// （自作農化で熱心に耕す者が増える＝割に合う）。自作農だらけの社会では買える意欲は乏しい。
        /// </summary>
        public static float ProductivityGain(float reformScope, float tenancyRate, LandReformParams p)
        {
            float scope = Mathf.Clamp01(reformScope);
            float tenancy = Mathf.Clamp01(tenancyRate);
            return p.incentiveScale * scope * tenancy;
        }

        public static float ProductivityGain(float reformScope, float tenancyRate)
            => ProductivityGain(reformScope, tenancyRate, LandReformParams.Default);

        /// <summary>
        /// 農民支持の獲得（0..supportScale）。再分配の範囲に比例＝土地を配るほど農民は味方する。
        /// 支持（#113）の係数に使う。
        /// </summary>
        public static float PeasantSupport(float reformScope, LandReformParams p)
        {
            return p.supportScale * Mathf.Clamp01(reformScope);
        }

        public static float PeasantSupport(float reformScope)
            => PeasantSupport(reformScope, LandReformParams.Default);

        /// <summary>
        /// 地主層の反発の強さ（0..1）。失う土地が多い（reformScope）ほど強く、補償
        /// （compensation 0..1）で和らぐ＝カネで門閥の牙を抜く。反乱圧力・既得反発の係数に使う。
        /// </summary>
        public static float LandlordBacklash(float reformScope, float compensation, LandReformParams p)
        {
            float scope = Mathf.Clamp01(reformScope);
            float comp = Mathf.Clamp01(compensation);
            return Mathf.Clamp01(p.backlashScale * scope * (1f - comp));
        }

        public static float LandlordBacklash(float reformScope, float compensation)
            => LandlordBacklash(reformScope, compensation, LandReformParams.Default);

        /// <summary>
        /// 短期の混乱の大きさ（0..disruptionScale）。再分配の範囲（reformScope）に比例し、
        /// implementationSpeed（実施速度 0..1）が速いほど大きい＝急ぐほど現場が乱れる。
        /// 漸進改革（速度低）は混乱が浅い。安定度低下などの係数に使う。
        /// </summary>
        public static float ShortTermDisruption(float reformScope, float implementationSpeed, LandReformParams p)
        {
            float scope = Mathf.Clamp01(reformScope);
            float speed = Mathf.Clamp01(implementationSpeed);
            return p.disruptionScale * scope * speed;
        }

        public static float ShortTermDisruption(float reformScope, float implementationSpeed)
            => ShortTermDisruption(reformScope, implementationSpeed, LandReformParams.Default);

        /// <summary>
        /// 過度な細分化の弊害（0..fragmentationScale）。再分配の範囲（reformScope）が広いほど
        /// 零細農地が増え、mechanizationLevel（機械化水準 0..1）が低いほど痛い＝
        /// 機械化できない零細農は規模の経済を失う（行き過ぎた平等は非効率）。
        /// 機械化が進めば大型機械が細分化を相殺できる。
        /// </summary>
        public static float FragmentationPenalty(float reformScope, float mechanizationLevel, LandReformParams p)
        {
            float scope = Mathf.Clamp01(reformScope);
            float mech = Mathf.Clamp01(mechanizationLevel);
            return p.fragmentationScale * scope * (1f - mech);
        }

        public static float FragmentationPenalty(float reformScope, float mechanizationLevel)
            => FragmentationPenalty(reformScope, mechanizationLevel, LandReformParams.Default);

        /// <summary>
        /// 長期の純産出効果（意欲の向上−細分化の損失）。正なら改革は産出を増やし、負なら
        /// 行き過ぎた細分化が意欲の利得を食い潰す＝割に合わない。小作が多いほど意欲の利得が
        /// 大きく純効果は正に寄り、機械化なしで全範囲に配ると細分化で負へ振れる。
        /// 短期混乱（<see cref="ShortTermDisruption"/>）は含まない＝あくまで定常化後の長期効果。
        /// </summary>
        public static float NetOutput(float reformScope, float tenancyRate, float mechanizationLevel,
                                      LandReformParams p)
        {
            float gain = ProductivityGain(reformScope, tenancyRate, p);
            float penalty = FragmentationPenalty(reformScope, mechanizationLevel, p);
            return gain - penalty;
        }

        public static float NetOutput(float reformScope, float tenancyRate, float mechanizationLevel)
            => NetOutput(reformScope, tenancyRate, mechanizationLevel, LandReformParams.Default);
    }
}
