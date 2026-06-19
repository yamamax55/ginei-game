using UnityEngine;

namespace Ginei
{
    /// <summary>植民地開発の調整係数。</summary>
    public readonly struct ColonyDevelopmentParams
    {
        /// <summary>環境の過酷さが初期投資を目減りさせる重み（過酷さ×これだけ分母が増える）。</summary>
        public readonly float hostilityWeight;
        /// <summary>入植者数が定着へ寄与する係数（人数×これで0..1の入植者寄与）。</summary>
        public readonly float settlerWeight;
        /// <summary>インフラ整備の基礎率（投資×人口×時間係数に掛かる）。</summary>
        public readonly float infraRate;
        /// <summary>インフラ整備の時間逓増の冪指数（0..1・1未満で逓増が鈍る＝Sqrt 相当）。</summary>
        public readonly float timeExponent;
        /// <summary>自給自足度のスケール（√(インフラ×現地資源)×これで0..1へ）。</summary>
        public readonly float sufficiencyScale;
        /// <summary>開発段階に占めるインフラの重み（0..1・残りが自給の重み）。</summary>
        public readonly float stageInfraWeight;
        /// <summary>開発段階を 0..1 へ正規化する飽和インフラ量（これでインフラが上限段階に達する）。</summary>
        public readonly float stageInfraNorm;
        /// <summary>本国への経済的見返りの基礎率（開発段階×現地資源×これ）。</summary>
        public readonly float returnRate;

        public ColonyDevelopmentParams(float hostilityWeight, float settlerWeight, float infraRate,
            float timeExponent, float sufficiencyScale, float stageInfraWeight, float stageInfraNorm,
            float returnRate)
        {
            this.hostilityWeight = Mathf.Max(0f, hostilityWeight);
            this.settlerWeight = Mathf.Max(0f, settlerWeight);
            this.infraRate = Mathf.Max(0f, infraRate);
            this.timeExponent = Mathf.Clamp01(timeExponent);
            this.sufficiencyScale = Mathf.Max(0f, sufficiencyScale);
            this.stageInfraWeight = Mathf.Clamp01(stageInfraWeight);
            this.stageInfraNorm = Mathf.Max(0.0001f, stageInfraNorm);
            this.returnRate = Mathf.Max(0f, returnRate);
        }

        /// <summary>既定＝過酷重み1・入植係数0.01・インフラ率0.001・時間冪0.5・自給スケール0.005・段階インフラ重み0.5・飽和インフラ200・見返り率0.1。</summary>
        public static ColonyDevelopmentParams Default =>
            new ColonyDevelopmentParams(1f, 0.01f, 0.001f, 0.5f, 0.005f, 0.5f, 200f, 0.1f);
    }

    /// <summary>
    /// 植民地開発の純ロジック（新天地の開拓と発展段階）。投下資本で開拓を立ち上げ、入植者と生活基盤で
    /// 人口を定着させ、投資・人口・時間でインフラを整え（逓増）、インフラと現地資源で自給自足へ到達し、
    /// 開発段階（前哨→入植地→植民地→属州）が進む。自給が低いうちは本国へ依存し、発展すると本国へ
    /// 経済的見返りを返す。「新天地は投資が要り、自立すると本国を富ませる」を式に出す。
    /// 入植・開拓の最初の一押し（ColonizationRules）の後段＝定着してからの発展段階を扱う。
    /// 乱数なし・決定論。実効値パターン（基準非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ColonyDevelopmentRules
    {
        /// <summary>
        /// 開拓初期の立ち上がり＝投下資本/(1+環境の過酷さ×過酷重み)。
        /// 同じ資本でも過酷な惑星ほど目減りする（命がけの開拓）。資本0以下は0。
        /// </summary>
        public static float InitialInvestment(float capitalInjected, float planetHostility, ColonyDevelopmentParams p)
        {
            float capital = Mathf.Max(0f, capitalInjected);
            float hostility = Mathf.Max(0f, planetHostility);
            return capital / (1f + hostility * p.hostilityWeight);
        }

        public static float InitialInvestment(float capitalInjected, float planetHostility)
            => InitialInvestment(capitalInjected, planetHostility, ColonyDevelopmentParams.Default);

        /// <summary>
        /// 人口の定着＝初期投資×入植者寄与(0..1)×生活基盤(0..1)。投資があっても入植者か生活基盤の
        /// どちらかが欠ければ人は根付かない（定着＝投資・人・暮らしの積）。入力は非負/01へクランプ。
        /// </summary>
        public static float PopulationSettlement(float initialInvestment, float settlerCount, float amenities,
            ColonyDevelopmentParams p)
        {
            float invest = Mathf.Max(0f, initialInvestment);
            float settlerFactor = Mathf.Clamp01(Mathf.Max(0f, settlerCount) * p.settlerWeight);
            float amenityFactor = Mathf.Clamp01(amenities);
            return invest * settlerFactor * amenityFactor;
        }

        public static float PopulationSettlement(float initialInvestment, float settlerCount, float amenities)
            => PopulationSettlement(initialInvestment, settlerCount, amenities, ColonyDevelopmentParams.Default);

        /// <summary>
        /// インフラ整備（逓増）＝インフラ率×投資×定着人口×時間^冪。時間は冪0.5（既定）で逓増が鈍る＝
        /// 整えるほど一手の伸びが小さくなる。投資・人口が多いほど速く整う。入力は非負へクランプ。
        /// </summary>
        public static float InfrastructureGrowth(float investment, float populationSettlement, float timeElapsed,
            ColonyDevelopmentParams p)
        {
            float invest = Mathf.Max(0f, investment);
            float pop = Mathf.Max(0f, populationSettlement);
            float t = Mathf.Max(0f, timeElapsed);
            float timeFactor = Mathf.Pow(t, p.timeExponent); // 逓増（冪<1）
            return p.infraRate * invest * pop * timeFactor;
        }

        public static float InfrastructureGrowth(float investment, float populationSettlement, float timeElapsed)
            => InfrastructureGrowth(investment, populationSettlement, timeElapsed, ColonyDevelopmentParams.Default);

        /// <summary>
        /// 自給自足度（0..1）＝スケール×√(インフラ×現地資源)。インフラと資源の両方が要る（積＝
        /// どちらか欠けると自給できない）。√で逓増を鈍らせ上限へ滑らかに飽和。入力は非負へクランプ。
        /// </summary>
        public static float SelfSufficiency(float infrastructureGrowth, float localResources, ColonyDevelopmentParams p)
        {
            float infra = Mathf.Max(0f, infrastructureGrowth);
            float res = Mathf.Max(0f, localResources);
            float raw = p.sufficiencyScale * Mathf.Sqrt(infra * res);
            return Mathf.Clamp01(raw);
        }

        public static float SelfSufficiency(float infrastructureGrowth, float localResources)
            => SelfSufficiency(infrastructureGrowth, localResources, ColonyDevelopmentParams.Default);

        /// <summary>
        /// 開発段階（0..1・前哨→入植地→植民地→属州）＝インフラ正規化(0..1)とインフラ重みで自給と混ぜる。
        /// インフラが飽和量(stageInfraNorm)で1へ達する。インフラだけでも自給だけでも段階は進まず両輪。
        /// </summary>
        public static float DevelopmentStage(float infrastructureGrowth, float selfSufficiency,
            ColonyDevelopmentParams p)
        {
            float infraNorm = Mathf.Clamp01(Mathf.Max(0f, infrastructureGrowth) / p.stageInfraNorm);
            float suff = Mathf.Clamp01(selfSufficiency);
            float stage = p.stageInfraWeight * infraNorm + (1f - p.stageInfraWeight) * suff;
            return Mathf.Clamp01(stage);
        }

        public static float DevelopmentStage(float infrastructureGrowth, float selfSufficiency)
            => DevelopmentStage(infrastructureGrowth, selfSufficiency, ColonyDevelopmentParams.Default);

        /// <summary>
        /// 本国への依存度（0..1）＝1−自給自足度。自給できないほど本国からの補給に頼る（依存）。
        /// 自給1.0で依存0＝完全自立。Params 不要（純粋な補数）。
        /// </summary>
        public static float HomelandDependency(float selfSufficiency)
        {
            float suff = Mathf.Clamp01(selfSufficiency);
            return Mathf.Clamp01(1f - suff);
        }

        /// <summary>
        /// 本国への経済的見返り（0以上）＝開発段階×現地資源×見返り率。発展した植民地ほど、
        /// 資源が豊かなほど本国を富ませる（属州化＝富の源泉）。前哨地（段階0）は見返りなし。
        /// </summary>
        public static float EconomicReturn(float developmentStage, float localResources, ColonyDevelopmentParams p)
        {
            float stage = Mathf.Clamp01(developmentStage);
            float res = Mathf.Max(0f, localResources);
            return stage * res * p.returnRate;
        }

        public static float EconomicReturn(float developmentStage, float localResources)
            => EconomicReturn(developmentStage, localResources, ColonyDevelopmentParams.Default);

        /// <summary>
        /// 植民地が成り立つか＝自給自足度が閾値以上 かつ 本国依存度が閾値未満（自立できている）。
        /// 自立の見込みが立つまでは本国が支え続ける必要がある（成り立たない＝撤退/支援継続の判断）。
        /// </summary>
        public static bool IsColonyViable(float selfSufficiency, float homelandDependency, float threshold)
        {
            float suff = Mathf.Clamp01(selfSufficiency);
            float dep = Mathf.Clamp01(homelandDependency);
            float th = Mathf.Clamp01(threshold);
            return suff >= th && dep < th;
        }

        /// <summary>既定閾値0.5で植民地の成立を判定。</summary>
        public static bool IsColonyViable(float selfSufficiency, float homelandDependency)
            => IsColonyViable(selfSufficiency, homelandDependency, 0.5f);
    }
}
