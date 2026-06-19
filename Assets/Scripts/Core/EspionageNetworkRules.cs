using UnityEngine;

namespace Ginei
{
    /// <summary>諜報網（HUMINT）の調整係数。マジックナンバー禁止＝ここに集約。全フィールド ctor で Clamp。</summary>
    public readonly struct EspionageNetworkParams
    {
        /// <summary>諜報網の到達範囲を飽和させる工作員数の基準（この数で到達が頭打ちに近づく）。</summary>
        public readonly float maxAgents;
        /// <summary>工作員数→到達範囲の収穫逓減の指数（0..1・小さいほど少人数でも一定の網になる）。</summary>
        public readonly float reachExponent;
        /// <summary>到達範囲における工作員の質の重み（0..1・残りは頭数が担う）。</summary>
        public readonly float qualityWeight;
        /// <summary>到達範囲を実露見リスクへ写すときの基準倍率（0..1）。</summary>
        public readonly float exposureScale;
        /// <summary>露見リスクが単位時間あたり工作員を失わせる率（0..1）。</summary>
        public readonly float attritionRate;
        /// <summary>深く潜入した工作員の価値の下駄＝最低価値（0..1・残りを潜入度が積む）。</summary>
        public readonly float embeddednessFloor;
        /// <summary>露見と二重スパイから網の汚染（信頼喪失）への基準倍率（0..1）。</summary>
        public readonly float compromiseWeight;
        /// <summary>諜報網が摘発されたと判定する露見リスクの既定閾値（0..1）。</summary>
        public readonly float blownThreshold;

        public EspionageNetworkParams(float maxAgents, float reachExponent, float qualityWeight,
                                      float exposureScale, float attritionRate, float embeddednessFloor,
                                      float compromiseWeight, float blownThreshold)
        {
            this.maxAgents = Mathf.Max(1f, maxAgents);
            this.reachExponent = Mathf.Clamp01(reachExponent);
            this.qualityWeight = Mathf.Clamp01(qualityWeight);
            this.exposureScale = Mathf.Clamp01(exposureScale);
            this.attritionRate = Mathf.Clamp01(attritionRate);
            this.embeddednessFloor = Mathf.Clamp01(embeddednessFloor);
            this.compromiseWeight = Mathf.Clamp01(compromiseWeight);
            this.blownThreshold = Mathf.Clamp01(blownThreshold);
        }

        /// <summary>
        /// 既定＝飽和工作員数50/到達指数0.5/質重み0.5/露見倍率1.0/摘発率0.5/潜入下駄0.3/汚染倍率1.0/摘発閾値0.6。
        /// </summary>
        public static EspionageNetworkParams Default =>
            new EspionageNetworkParams(50f, 0.5f, 0.5f, 1f, 0.5f, 0.3f, 1f, 0.6f);
    }

    /// <summary>
    /// 諜報網（HUMINT＝人的諜報）の純ロジック。敵勢力内に潜入させたスパイ・協力者の網から軍事・政治・経済の
    /// 機密を得る。網が大きいほど情報は増えるが、目立つぶん敵の防諜に露見・摘発されるリスクも増す＝拡大と
    /// 秘匿のトレードオフ。<see cref="SignalIntelligenceRules"/>（通信傍受・SIGINT）/<see cref="ReconRules"/>
    /// （軍事偵察）とは別レイヤー＝こちらは人を潜り込ませる諜報。得た機密は <see cref="IntelFusionRules"/> の
    /// 情報源の一つとして他系統と統合される。盤面非依存の plain 引数。値は徹底して 0..1 に clamp・乱数なし決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。本ルールが諜報網の構築・収量・露見・損耗を出す唯一の窓口。
    /// </summary>
    public static class EspionageNetworkRules
    {
        /// <summary>
        /// 諜報網の到達範囲 0..1：敵地に潜む工作員数 agentCount と質 agentQuality から、機密へ届く広さを出す。
        /// 頭数は収穫逓減（reachExponent）で効き、質は qualityWeight ぶんを底上げ＝少数精鋭でも広く届く。
        /// Pow((count/maxAgents)^reachExponent) × (1 − qualityWeight + qualityWeight × quality)。
        /// </summary>
        public static float NetworkReach(float agentCount, float agentQuality, EspionageNetworkParams p)
        {
            float countNorm = Mathf.Clamp01(Mathf.Max(0f, agentCount) / p.maxAgents);
            float countFactor = Mathf.Pow(countNorm, p.reachExponent);
            float q = Mathf.Clamp01(agentQuality);
            float qualityFactor = (1f - p.qualityWeight) + p.qualityWeight * q;
            return Mathf.Clamp01(countFactor * qualityFactor);
        }

        public static float NetworkReach(float agentCount, float agentQuality)
            => NetworkReach(agentCount, agentQuality, EspionageNetworkParams.Default);

        /// <summary>
        /// 機密情報量 0..1：諜報網の到達範囲 networkReach のうち実際に得られる機密。敵の防諜・秘匿 targetSecurity
        /// が高いほど機密へ近づけず収量が落ちる。networkReach × (1 − targetSecurity)。Params 非依存（純粋な内訳）。
        /// </summary>
        public static float IntelYield(float networkReach, float targetSecurity)
        {
            float reach = Mathf.Clamp01(networkReach);
            float security = Mathf.Clamp01(targetSecurity);
            return Mathf.Clamp01(reach * (1f - security));
        }

        /// <summary>
        /// 露見リスク 0..1：諜報網が敵の防諜 enemyCounterintel に見つかる度合い。網が大きい（reach高）ほど目立ち、
        /// 敵の防諜が強いほど暴かれる。networkReach × enemyCounterintel × exposureScale。
        /// </summary>
        public static float ExposureRisk(float networkReach, float enemyCounterintel, EspionageNetworkParams p)
        {
            float reach = Mathf.Clamp01(networkReach);
            float ci = Mathf.Clamp01(enemyCounterintel);
            return Mathf.Clamp01(reach * ci * p.exposureScale);
        }

        public static float ExposureRisk(float networkReach, float enemyCounterintel)
            => ExposureRisk(networkReach, enemyCounterintel, EspionageNetworkParams.Default);

        /// <summary>
        /// 工作員損耗 0..1：露見リスク exposureRisk が時間 dt のあいだに摘発で失わせる工作員の割合。
        /// exposureRisk × attritionRate × dt。dt は非負へクランプ。
        /// </summary>
        public static float NetworkAttrition(float exposureRisk, float dt, EspionageNetworkParams p)
        {
            float risk = Mathf.Clamp01(exposureRisk);
            float t = Mathf.Max(0f, dt);
            return Mathf.Clamp01(risk * p.attritionRate * t);
        }

        public static float NetworkAttrition(float exposureRisk, float dt)
            => NetworkAttrition(exposureRisk, dt, EspionageNetworkParams.Default);

        /// <summary>
        /// 深層潜入の価値 0..1：敵組織に深く潜り込んだ工作員 agentEmbeddedness ほど価値が高い（露見しにくく機密に近い）。
        /// embeddednessFloor を下駄に、潜入度ぶんを積む。embeddednessFloor + (1 − embeddednessFloor) × embeddedness。
        /// </summary>
        public static float DeepCoverValue(float agentEmbeddedness, EspionageNetworkParams p)
        {
            float emb = Mathf.Clamp01(agentEmbeddedness);
            return Mathf.Clamp01(p.embeddednessFloor + (1f - p.embeddednessFloor) * emb);
        }

        public static float DeepCoverValue(float agentEmbeddedness)
            => DeepCoverValue(agentEmbeddedness, EspionageNetworkParams.Default);

        /// <summary>
        /// 構築所要時間：目標の網規模 targetNetworkSize を毎時の勧誘速度 recruitmentRate で割った所要時間。
        /// 勧誘が遅いほど時間がかかる。recruitmentRate は微小下限でゼロ割を防ぐ。Params 非依存（時間ゆえ 0..1 化しない）。
        /// </summary>
        public static float NetworkBuildTime(float targetNetworkSize, float recruitmentRate)
        {
            float size = Mathf.Max(0f, targetNetworkSize);
            float rate = Mathf.Max(0.0001f, recruitmentRate);
            return size / rate;
        }

        /// <summary>
        /// 網の汚染 0..1：露見リスク exposureRisk と二重スパイ・寝返りリスク doubleAgentRisk から、網が敵に
        /// 取り込まれ偽情報源化する度合い。exposureRisk × doubleAgentRisk × compromiseWeight。
        /// </summary>
        public static float Compromise(float exposureRisk, float doubleAgentRisk, EspionageNetworkParams p)
        {
            float risk = Mathf.Clamp01(exposureRisk);
            float dar = Mathf.Clamp01(doubleAgentRisk);
            return Mathf.Clamp01(risk * dar * p.compromiseWeight);
        }

        public static float Compromise(float exposureRisk, float doubleAgentRisk)
            => Compromise(exposureRisk, doubleAgentRisk, EspionageNetworkParams.Default);

        /// <summary>
        /// 摘発判定：露見リスク exposureRisk が threshold 以上なら諜報網が摘発された（true）。
        /// </summary>
        public static bool IsNetworkBlown(float exposureRisk, float threshold)
        {
            return Mathf.Clamp01(exposureRisk) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="EspionageNetworkParams.blownThreshold"/>）での摘発判定。</summary>
        public static bool IsNetworkBlown(float exposureRisk)
            => IsNetworkBlown(exposureRisk, EspionageNetworkParams.Default.blownThreshold);
    }
}
