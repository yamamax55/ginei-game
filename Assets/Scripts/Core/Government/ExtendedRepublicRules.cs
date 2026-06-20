using UnityEngine;

namespace Ginei
{
    /// <summary>拡大共和国（マディソンのパラドックス）の調整係数。</summary>
    public readonly struct ExtendedRepublicParams
    {
        /// <summary>利害多様性に版図規模が寄与する重み（広いほど利害が多元化する）。</summary>
        public readonly float territoryDiversityWeight;
        /// <summary>利害多様性に人口の多様性が寄与する重み（人口が多彩なほど党派が分かれる）。</summary>
        public readonly float populationDiversityWeight;
        /// <summary>多数派形成の困難の非線形度（多様性の冪指数・1以上）。バラバラなほど加速して結束しにくい。</summary>
        public readonly float majorityExponent;
        /// <summary>結託の困難に連絡コストが効く重み（広域＋高コストほど共謀が妨げられる）。</summary>
        public readonly float collusionWeight;
        /// <summary>派閥中和が安定へ与える最大ボーナス（実効値の上振れ幅・0以上）。</summary>
        public readonly float maxStabilityBonus;
        /// <summary>代表制が直接民主の派閥熱を冷ます濾過の強さ（0..1）。</summary>
        public readonly float representationFilterStrength;

        public ExtendedRepublicParams(float territoryDiversityWeight, float populationDiversityWeight,
            float majorityExponent, float collusionWeight, float maxStabilityBonus,
            float representationFilterStrength)
        {
            this.territoryDiversityWeight = Mathf.Max(0f, territoryDiversityWeight);
            this.populationDiversityWeight = Mathf.Max(0f, populationDiversityWeight);
            this.majorityExponent = Mathf.Max(1f, majorityExponent);
            this.collusionWeight = Mathf.Max(0f, collusionWeight);
            this.maxStabilityBonus = Mathf.Max(0f, maxStabilityBonus);
            this.representationFilterStrength = Mathf.Clamp01(representationFilterStrength);
        }

        /// <summary>既定＝版図重み0.6・人口重み0.4・多数派冪指数1.5・結託重み0.7・安定ボーナス上限0.5・代表制濾過0.5。</summary>
        public static ExtendedRepublicParams Default =>
            new ExtendedRepublicParams(0.6f, 0.4f, 1.5f, 0.7f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 拡大共和国の安定の純ロジック（『ザ・フェデラリスト』第10篇・マディソン #1485）。
    /// 「大きな共和国（extended republic）の方が小さな共和国より派閥の害に強い」を式に出す＝
    /// 版図と人口が大きいほど利害・党派が多様化し、単一の多数派（多数派専制）が形成されにくく、
    /// 形成されても広域では連絡コストゆえに結託しにくい。多様な派閥が互いに中和し合い政体が安定する
    /// （規模が多数派専制を防ぐマディソンのパラドックス）。ただし大きすぎると今度は統治が及ばない
    /// （規模vs統治のトレードオフ）。従来の通念（共和国は小さくあるべき）への反論。
    /// 物流（<see cref="LogisticsRules"/>＝版図が回廊で物理的に繋がる一体化度）とは別系統＝こちらは
    /// 規模が派閥を中和する政治的効果を扱う。政体規模の適合（PolityScaleRules・別EPIC ROUS）とも別。
    /// 大きすぎる版図の統治難は過拡張（<see cref="OverextensionRules"/>＝負担と国力の比）へ接続する。
    /// 派閥の増殖そのもの（FactionMultiplicityRules・同EPIC FED）とも別＝こちらは多様性が害を薄める側。
    /// 倍率は各係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ExtendedRepublicRules
    {
        /// <summary>
        /// 利害の多様性（0..1）＝版図規模×版図重み＋人口の多様性×人口重み（重み和で正規化）。
        /// 版図と人口が大きいほど利害・党派が多様化する＝広いほど多元的。
        /// </summary>
        public static float InterestDiversity(float territorySize, float populationVariety, ExtendedRepublicParams p)
        {
            float t = Mathf.Clamp01(territorySize);
            float v = Mathf.Clamp01(populationVariety);
            float weightSum = p.territoryDiversityWeight + p.populationDiversityWeight;
            if (weightSum <= 0f) return 0f;
            float raw = (t * p.territoryDiversityWeight + v * p.populationDiversityWeight) / weightSum;
            return Mathf.Clamp01(raw);
        }

        public static float InterestDiversity(float territorySize, float populationVariety)
            => InterestDiversity(territorySize, populationVariety, ExtendedRepublicParams.Default);

        /// <summary>
        /// 単一の多数派が形成される困難（0..1）。多様性が高いほど利害がバラバラで一つにまとまらない
        /// ＝多数派専制が生まれにくい。多様性を冪で非線形に効かせる（散らばるほど加速して結束しにくい）。
        /// </summary>
        public static float MajorityFormationDifficulty(float interestDiversity, ExtendedRepublicParams p)
        {
            float d = Mathf.Clamp01(interestDiversity);
            return Mathf.Clamp01(Mathf.Pow(d, 1f / p.majorityExponent));
        }

        public static float MajorityFormationDifficulty(float interestDiversity)
            => MajorityFormationDifficulty(interestDiversity, ExtendedRepublicParams.Default);

        /// <summary>
        /// 派閥の中和（0..1）＝拡大共和国の核。多様な派閥が互いに中和し合い、派閥の脅威を薄める。
        /// 多様性が高いほど（大きいほど）脅威が中和される＝中和度＝多様性×脅威の打ち消し。
        /// 脅威が大きくても多様性が高ければ薄まる（大共和国ほど派閥の害が中和される）。
        /// </summary>
        public static float FactionNeutralization(float interestDiversity, float factionalThreat)
        {
            float d = Mathf.Clamp01(interestDiversity);
            float threat = Mathf.Clamp01(factionalThreat);
            // 多様性が脅威を打ち消す＝脅威のうち (1−多様性) ぶんしか害として残らない。中和度＝消えた割合×脅威。
            return Mathf.Clamp01(threat * d);
        }

        /// <summary>
        /// 結託の困難（0..1）。広域では派閥が結託しにくい＝版図規模×連絡コストが共謀を妨げる。
        /// 形成されても広い領域では各地の派閥が連携できない（フェデラリスト第10篇の第二論点）。
        /// </summary>
        public static float CollusionDifficulty(float territorySize, float communicationCost, ExtendedRepublicParams p)
        {
            float t = Mathf.Clamp01(territorySize);
            float cost = Mathf.Clamp01(communicationCost);
            return Mathf.Clamp01(t * cost * p.collusionWeight);
        }

        public static float CollusionDifficulty(float territorySize, float communicationCost)
            => CollusionDifficulty(territorySize, communicationCost, ExtendedRepublicParams.Default);

        /// <summary>
        /// 安定ボーナス（≥1.0付近）＝派閥の中和が政体の安定に寄与する。中和が高いほど 1.0＋中和×上限。
        /// 各安定係数に掛けて使う（実効値パターン・基準非破壊）。中和ゼロなら 1.0（無補正）。
        /// </summary>
        public static float StabilityBonus(float factionNeutralization, ExtendedRepublicParams p)
        {
            float n = Mathf.Clamp01(factionNeutralization);
            return 1f + n * p.maxStabilityBonus;
        }

        public static float StabilityBonus(float factionNeutralization)
            => StabilityBonus(factionNeutralization, ExtendedRepublicParams.Default);

        /// <summary>
        /// 規模vs統治のトレードオフ（実効ファクター・0..1＋）＝規模の利点と統治の難しさの綱引き。
        /// 統治能力が版図規模に追いつかないと大きすぎて統治が及ばない＝規模−（規模−統治能力）の不足を引く。
        /// 統治能力が規模以上なら 1.0（規模の利点を活かせる）、不足ぶんだけ 1.0 を割る（過拡張へ接続）。
        /// </summary>
        public static float ScaleVsCohesion(float territorySize, float governanceCapacity)
        {
            float t = Mathf.Clamp01(territorySize);
            float g = Mathf.Clamp01(governanceCapacity);
            float shortfall = Mathf.Max(0f, t - g); // 統治が規模に追いつかない不足
            return Mathf.Clamp01(1f - shortfall);
        }

        /// <summary>
        /// 代表制の濾過（0..1）。大共和国は代表制で民意を濾過し、直接民主の派閥熱を冷ます。
        /// 版図が広いほど（直接民主が不可能なほど）代表制比率と濾過の強さで派閥熱を抑える。
        /// 返り値は「冷まされた派閥熱の割合」＝大きいほど熱が濾過される。
        /// </summary>
        public static float RepresentationFilter(float territorySize, float representativeRatio, ExtendedRepublicParams p)
        {
            float t = Mathf.Clamp01(territorySize);
            float ratio = Mathf.Clamp01(representativeRatio);
            return Mathf.Clamp01(t * ratio * p.representationFilterStrength);
        }

        public static float RepresentationFilter(float territorySize, float representativeRatio)
            => RepresentationFilter(territorySize, representativeRatio, ExtendedRepublicParams.Default);

        /// <summary>
        /// 拡大共和国が安定状態か＝派閥を中和しつつ統治も及ぶ。中和が閾値以上かつ統治能力が閾値以上の双方が要る
        /// （中和だけ高くても統治が及ばなければ崩れる＝規模vs統治の両立）。
        /// </summary>
        public static bool IsStableExtendedRepublic(float factionNeutralization, float governanceCapacity, float threshold)
        {
            float n = Mathf.Clamp01(factionNeutralization);
            float g = Mathf.Clamp01(governanceCapacity);
            float th = Mathf.Clamp01(threshold);
            return n >= th && g >= th;
        }
    }
}
