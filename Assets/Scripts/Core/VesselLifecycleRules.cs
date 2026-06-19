using UnityEngine;

namespace Ginei
{
    /// <summary>艦のライフサイクル（就役から退役まで）の調整係数。</summary>
    public readonly struct VesselLifecycleParams
    {
        /// <summary>就役1年あたりの船体疲労増分（運用ゼロでもこれだけ蓄積）。</summary>
        public readonly float fatiguePerYear;
        /// <summary>運用の激しさが疲労を増幅する重み（高稼働ほど早く疲れる）。</summary>
        public readonly float tempoFatigueWeight;
        /// <summary>初期故障（バスタブ左肩）の就役時の高さ。</summary>
        public readonly float infantRate;
        /// <summary>初期故障が年とともに引く速さ（早く落ち着く）。</summary>
        public readonly float infantDecay;
        /// <summary>摩耗故障（バスタブ右肩）の年あたり係数。</summary>
        public readonly float wearRate;
        /// <summary>摩耗故障が年数で増える指数（2＝二次曲線で末期に跳ねる）。</summary>
        public readonly float wearExponent;
        /// <summary>整備で故障率を抑える最大割合（0..1。満額整備でこの割合だけ減る）。</summary>
        public readonly float maintenanceSuppression;
        /// <summary>改修1単位あたりの性能維持の上乗せ（古い艦も改修で粘る）。</summary>
        public readonly float refitRetentionGain;
        /// <summary>整備投資1単位あたりの延命効果（疲労が少ないほど効く）。</summary>
        public readonly float extensionPerInvestment;
        /// <summary>改修×技術差が近代化性能に効く重み。</summary>
        public readonly float modernizationWeight;
        /// <summary>維持費の年あたり基礎（整備水準ゼロでもかかる）。</summary>
        public readonly float baseCostPerYear;
        /// <summary>整備水準1単位あたりの維持費の上乗せ（手をかけるほど高い）。</summary>
        public readonly float maintCostWeight;
        /// <summary>残存価値が年数で目減りする速さ（古い艦ほど安い）。</summary>
        public readonly float depreciationRate;
        /// <summary>経済的寿命の既定閾値（維持コストが残存価値の何倍を超えたら退役か）。</summary>
        public readonly float economicLifeThreshold;

        public VesselLifecycleParams(float fatiguePerYear, float tempoFatigueWeight,
                                     float infantRate, float infantDecay, float wearRate, float wearExponent,
                                     float maintenanceSuppression, float refitRetentionGain,
                                     float extensionPerInvestment, float modernizationWeight,
                                     float baseCostPerYear, float maintCostWeight,
                                     float depreciationRate, float economicLifeThreshold)
        {
            this.fatiguePerYear = Mathf.Max(0f, fatiguePerYear);
            this.tempoFatigueWeight = Mathf.Max(0f, tempoFatigueWeight);
            this.infantRate = Mathf.Max(0f, infantRate);
            this.infantDecay = Mathf.Max(0f, infantDecay);
            this.wearRate = Mathf.Max(0f, wearRate);
            this.wearExponent = Mathf.Max(0f, wearExponent);
            this.maintenanceSuppression = Mathf.Clamp01(maintenanceSuppression);
            this.refitRetentionGain = Mathf.Max(0f, refitRetentionGain);
            this.extensionPerInvestment = Mathf.Max(0f, extensionPerInvestment);
            this.modernizationWeight = Mathf.Max(0f, modernizationWeight);
            this.baseCostPerYear = Mathf.Max(0f, baseCostPerYear);
            this.maintCostWeight = Mathf.Max(0f, maintCostWeight);
            this.depreciationRate = Mathf.Max(0f, depreciationRate);
            this.economicLifeThreshold = Mathf.Max(0f, economicLifeThreshold);
        }

        /// <summary>
        /// 既定＝疲労0.02/年・運用重み1・初期故障0.1(減衰0.5)・摩耗0.005(指数2)・整備抑制0.8・
        /// 改修維持0.2・延命0.5・近代化重み0.5・維持基礎1/年・整備費0.5・減価0.1・経済寿命閾値1.5。
        /// </summary>
        public static VesselLifecycleParams Default => new VesselLifecycleParams(
            0.02f, 1f, 0.1f, 0.5f, 0.005f, 2f, 0.8f, 0.2f, 0.5f, 0.5f, 1f, 0.5f, 0.1f, 1.5f);
    }

    /// <summary>
    /// 艦のライフサイクル（就役から退役まで）の純ロジック。艦齢に伴う船体疲労、稼働年数と整備で決まる
    /// 故障率（バスタブ曲線＝就役直後の初期故障と末期の摩耗故障）、改修による性能維持と近代化、
    /// 整備投資による延命、生涯にわたる維持コストの累積、性能と年数で目減りする残存価値、そして
    /// 維持コストが残存価値を上回る「経済的寿命」を扱う＝「いつ直し、いつ退役させるか」の判断材料。
    /// 乱数なし・決定論。Mathf.Log/Exp 不使用（Pow/多項式で近似）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class VesselLifecycleRules
    {
        /// <summary>
        /// 船体疲労（0..1）＝就役年数×年あたり疲労×（1＋運用の激しさ×運用重み）。
        /// 長く・激しく使うほど疲れる。基準性能には触れず（実効値パターン）疲労スカラだけ返す。
        /// </summary>
        public static float HullFatigue(float serviceYears, float operationalTempo, VesselLifecycleParams p)
        {
            float years = Mathf.Max(0f, serviceYears);
            float tempo = Mathf.Clamp01(operationalTempo);
            float fatigue = years * p.fatiguePerYear * (1f + tempo * p.tempoFatigueWeight);
            return Mathf.Clamp01(fatigue);
        }

        public static float HullFatigue(float serviceYears, float operationalTempo)
            => HullFatigue(serviceYears, operationalTempo, VesselLifecycleParams.Default);

        /// <summary>
        /// 故障率（0..1）＝バスタブ曲線。初期故障（就役直後に高く年とともに引く）＋摩耗故障
        /// （末期に年数の指数で跳ねる）を、整備品質で最大 maintenanceSuppression ぶん抑える。
        /// 就役直後と末期が高く中盤が低い U字。Log/Exp は使わず多項式で近似。
        /// </summary>
        public static float FailureRate(float serviceYears, float maintenanceQuality, VesselLifecycleParams p)
        {
            float years = Mathf.Max(0f, serviceYears);
            float maint = Mathf.Clamp01(maintenanceQuality);
            // 初期故障：就役時 infantRate、年とともに線形に引いてゼロで止まる
            float infant = Mathf.Max(0f, p.infantRate * (1f - years * p.infantDecay));
            // 摩耗故障：年数の指数で増える
            float wear = p.wearRate * Mathf.Pow(years, p.wearExponent);
            float raw = infant + wear;
            float suppress = 1f - maint * p.maintenanceSuppression; // 整備で抑える
            return Mathf.Clamp01(raw * suppress);
        }

        public static float FailureRate(float serviceYears, float maintenanceQuality)
            => FailureRate(serviceYears, maintenanceQuality, VesselLifecycleParams.Default);

        /// <summary>
        /// 性能維持（0..1）＝（1－疲労）＋改修水準×改修維持ゲイン。疲労で落ちた性能を改修で粘らせる。
        /// 基準性能に掛ける倍率として使う（実効値パターン・基準非破壊）。
        /// </summary>
        public static float PerformanceRetention(float hullFatigue, float refitLevel, VesselLifecycleParams p)
        {
            float fatigue = Mathf.Clamp01(hullFatigue);
            float refit = Mathf.Clamp01(refitLevel);
            return Mathf.Clamp01((1f - fatigue) + refit * p.refitRetentionGain);
        }

        public static float PerformanceRetention(float hullFatigue, float refitLevel)
            => PerformanceRetention(hullFatigue, refitLevel, VesselLifecycleParams.Default);

        /// <summary>
        /// 延命効果（0..1）＝整備投資×延命係数×（1－疲労）。健全な艦ほど整備が効き、
        /// 疲れ切った艦はいくら投資しても延命しにくい＝「直す価値があるうちに直す」。
        /// </summary>
        public static float OverhaulExtension(float overhaulInvestment, float hullFatigue, VesselLifecycleParams p)
        {
            float invest = Mathf.Clamp01(overhaulInvestment);
            float fatigue = Mathf.Clamp01(hullFatigue);
            return Mathf.Clamp01(invest * p.extensionPerInvestment * (1f - fatigue));
        }

        public static float OverhaulExtension(float overhaulInvestment, float hullFatigue)
            => OverhaulExtension(overhaulInvestment, hullFatigue, VesselLifecycleParams.Default);

        /// <summary>
        /// 近代化による性能変化（-1..1）＝改修水準×技術差×近代化重み。技術差が正（自軍が新しい）なら
        /// 改修で向上、負（時代遅れ）なら改修しても陳腐化を映してマイナス＝符号付き。
        /// </summary>
        public static float ModernizationGain(float refitLevel, float technologyGap, VesselLifecycleParams p)
        {
            float refit = Mathf.Clamp01(refitLevel);
            float gap = Mathf.Clamp(technologyGap, -1f, 1f);
            return Mathf.Clamp(refit * gap * p.modernizationWeight, -1f, 1f);
        }

        public static float ModernizationGain(float refitLevel, float technologyGap)
            => ModernizationGain(refitLevel, technologyGap, VesselLifecycleParams.Default);

        /// <summary>
        /// 生涯維持コストの累積＝就役年数×（年あたり基礎＋整備水準×整備費重み）。
        /// 長く使い手をかけるほど積み上がる＝残存価値との綱引きで退役判断に効く。
        /// </summary>
        public static float LifecycleCost(float serviceYears, float maintenanceQuality, VesselLifecycleParams p)
        {
            float years = Mathf.Max(0f, serviceYears);
            float maint = Mathf.Clamp01(maintenanceQuality);
            return years * (p.baseCostPerYear + maint * p.maintCostWeight);
        }

        public static float LifecycleCost(float serviceYears, float maintenanceQuality)
            => LifecycleCost(serviceYears, maintenanceQuality, VesselLifecycleParams.Default);

        /// <summary>
        /// 残存価値（0..1）＝性能維持÷（1＋就役年数×減価率）。性能が残っていても古い艦ほど安い
        /// ＝下取り・スクラップ・経済寿命判定の基準。
        /// </summary>
        public static float ResidualValue(float performanceRetention, float serviceYears, VesselLifecycleParams p)
        {
            float perf = Mathf.Clamp01(performanceRetention);
            float years = Mathf.Max(0f, serviceYears);
            return Mathf.Clamp01(perf / (1f + years * p.depreciationRate));
        }

        public static float ResidualValue(float performanceRetention, float serviceYears)
            => ResidualValue(performanceRetention, serviceYears, VesselLifecycleParams.Default);

        /// <summary>
        /// 経済的寿命を超えたか＝維持コストが残存価値×閾値を上回るか。維持に金がかかりすぎ
        /// 価値が下がりきった艦は退役・解体すべき＝true。
        /// </summary>
        public static bool IsBeyondEconomicLife(float lifecycleCost, float residualValue, float threshold)
        {
            float cost = Mathf.Max(0f, lifecycleCost);
            float value = Mathf.Max(0f, residualValue);
            float thr = Mathf.Max(0f, threshold);
            return cost > value * thr;
        }

        public static bool IsBeyondEconomicLife(float lifecycleCost, float residualValue)
            => IsBeyondEconomicLife(lifecycleCost, residualValue, VesselLifecycleParams.Default.economicLifeThreshold);
    }
}
