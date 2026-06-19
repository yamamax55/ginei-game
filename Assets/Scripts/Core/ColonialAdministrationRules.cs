using UnityEngine;

namespace Ginei
{
    /// <summary>植民地統治の調整係数。</summary>
    public readonly struct ColonialAdministrationParams
    {
        /// <summary>距離が総督裁量へ寄与する重み（遠いほど独立）。</summary>
        public readonly float distanceWeight;
        /// <summary>通信遅延が総督裁量へ寄与する重み（連絡が遅いほど現地任せ）。</summary>
        public readonly float delayWeight;
        /// <summary>文化政策が同化へ寄与する重み（同化政策の効き）。</summary>
        public readonly float culturalWeight;
        /// <summary>統治年数による同化の飽和率（per time・長期統治で頭打ち）。</summary>
        public readonly float assimilationRate;
        /// <summary>反発の基礎率（税負担×強制同化×非自治に掛かる）。</summary>
        public readonly float resentmentRate;
        /// <summary>現地名士の取り込み上限（0..1）。</summary>
        public readonly float maxCooption;
        /// <summary>取り込みが行政実効性へ寄与する重み。</summary>
        public readonly float cooptionWeight;
        /// <summary>反発が行政実効性を削る重み。</summary>
        public readonly float resentmentDrag;

        public ColonialAdministrationParams(float distanceWeight, float delayWeight, float culturalWeight,
            float assimilationRate, float resentmentRate, float maxCooption, float cooptionWeight, float resentmentDrag)
        {
            this.distanceWeight = Mathf.Max(0f, distanceWeight);
            this.delayWeight = Mathf.Max(0f, delayWeight);
            this.culturalWeight = Mathf.Max(0f, culturalWeight);
            this.assimilationRate = Mathf.Max(0f, assimilationRate);
            this.resentmentRate = Mathf.Max(0f, resentmentRate);
            this.maxCooption = Mathf.Clamp01(maxCooption);
            this.cooptionWeight = Mathf.Max(0f, cooptionWeight);
            this.resentmentDrag = Mathf.Max(0f, resentmentDrag);
        }

        /// <summary>既定＝距離重み1・遅延重み1・文化重み1・同化率0.001・反発率1・取り込み上限1・取り込み重み0.5・反発抑え1。</summary>
        public static ColonialAdministrationParams Default =>
            new ColonialAdministrationParams(1f, 1f, 1f, 0.001f, 1f, 1f, 0.5f, 1f);
    }

    /// <summary>
    /// 植民地統治の純ロジック（平時の遠隔地行政＝属州・植民地を治める）。総督の裁量、本国の統制と
    /// 現地の自治のバランス、現地民の同化と反発、植民地税の収奪と還元、距離による統制の希薄化、
    /// 現地エリートの登用を扱う。距離と通信遅延で総督裁量が増し本国統制が薄れる。文化政策と統治年数で
    /// 同化が進むが、強制同化と重税は反発を生む。現地名士を登用すれば行政が回り、反発が高ければ実効性を削る。
    /// 占領統治（OccupationGovernanceRules・軍事占領）とは別＝こちらは確立した植民地行政。
    /// 乱数なし・決定論・実効値パターン（基準非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ColonialAdministrationRules
    {
        /// <summary>
        /// 総督の裁量（0..1）＝距離×距離重み＋通信遅延×遅延重み を 0..1 へ飽和（v/(v+1)）。
        /// 首都から遠く連絡が遅いほど総督は本国の指示を待てず独自に動く（遠隔地の自立）。
        /// 距離も遅延もゼロなら裁量0（本国の目が届く）。
        /// </summary>
        public static float GovernorAutonomy(float distanceFromCapital, float communicationDelay,
            ColonialAdministrationParams p)
        {
            float dist = Mathf.Clamp01(distanceFromCapital);
            float delay = Mathf.Clamp01(communicationDelay);
            float v = dist * p.distanceWeight + delay * p.delayWeight;
            if (v <= 0f) return 0f;
            return Mathf.Clamp01(v / (v + 1f));
        }

        public static float GovernorAutonomy(float distanceFromCapital, float communicationDelay)
            => GovernorAutonomy(distanceFromCapital, communicationDelay, ColonialAdministrationParams.Default);

        /// <summary>
        /// 中央統制（0..1）＝本国監督/(本国監督＋総督裁量)。本国の監督が総督裁量を上回るほど統制が利く。
        /// 監督も裁量もゼロなら統制0（無主の地）。裁量0なら統制1（完全な中央集権）。
        /// </summary>
        public static float CentralControl(float imperialOversight, float governorAutonomy,
            ColonialAdministrationParams p)
        {
            float oversight = Mathf.Clamp01(imperialOversight);
            float autonomy = Mathf.Clamp01(governorAutonomy);
            float denom = oversight + autonomy;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(oversight / denom);
        }

        public static float CentralControl(float imperialOversight, float governorAutonomy)
            => CentralControl(imperialOversight, governorAutonomy, ColonialAdministrationParams.Default);

        /// <summary>
        /// 現地の同化（0..1）＝文化政策×文化重み×（統治年数で飽和する成長）。文化政策を打ち統治が
        /// 長引くほど現地は本国文化に馴染む。時間は 1−1/(1+率×時間) で飽和（初期は伸びにくく長期で頭打ち）。
        /// 文化政策ゼロなら同化は進まない。
        /// </summary>
        public static float LocalAssimilation(float culturalPolicy, float timeUnderRule,
            ColonialAdministrationParams p)
        {
            float policy = Mathf.Clamp01(culturalPolicy);
            float t = Mathf.Max(0f, timeUnderRule);
            float timeFactor = 1f - 1f / (1f + p.assimilationRate * t); // 0→1へ飽和
            float raw = policy * p.culturalWeight * timeFactor;
            return Mathf.Clamp01(raw);
        }

        public static float LocalAssimilation(float culturalPolicy, float timeUnderRule)
            => LocalAssimilation(culturalPolicy, timeUnderRule, ColonialAdministrationParams.Default);

        /// <summary>
        /// 現地の反発（0..1）＝税負担×強制同化×(1−現地自治)×反発率。重税を課し同化を強制し、なお
        /// 自治を与えないほど恨みが積もる。自治を認めれば（localAutonomy が高い）反発は和らぐ。
        /// 税負担・強制同化のどれかがゼロなら反発0。
        /// </summary>
        public static float ColonialResentment(float taxBurden, float assimilationForce, float localAutonomy,
            ColonialAdministrationParams p)
        {
            float tax = Mathf.Clamp01(taxBurden);
            float force = Mathf.Clamp01(assimilationForce);
            float autonomy = Mathf.Clamp01(localAutonomy);
            float raw = tax * force * (1f - autonomy) * p.resentmentRate;
            return Mathf.Clamp01(raw);
        }

        public static float ColonialResentment(float taxBurden, float assimilationForce, float localAutonomy)
            => ColonialResentment(taxBurden, assimilationForce, localAutonomy, ColonialAdministrationParams.Default);

        /// <summary>
        /// 植民地収入（0以上）＝現地経済×税率×(1−反発)。現地経済が豊かで税率が高いほど取れるが、
        /// 反発が高いと徴税が滞り実収益が落ちる（恨みは収奪を妨げる）。税率0なら収入0、反発1なら収入0。
        /// </summary>
        public static float ColonialRevenue(float localEconomy, float taxRate, float resentment,
            ColonialAdministrationParams p)
        {
            float econ = Mathf.Max(0f, localEconomy);
            float rate = Mathf.Clamp01(taxRate);
            float res = Mathf.Clamp01(resentment);
            return Mathf.Max(0f, econ * rate * (1f - res));
        }

        public static float ColonialRevenue(float localEconomy, float taxRate, float resentment)
            => ColonialRevenue(localEconomy, taxRate, resentment, ColonialAdministrationParams.Default);

        /// <summary>
        /// 現地名士の取り込み（0..maxCooption）＝現地エリート数×登用機会 を 0..1 へ飽和（v/(v+1)）。
        /// 登用すべき名士がいて登用の門戸を開くほど現地有力者を体制に取り込める。
        /// エリート不在か門戸ゼロなら取り込み0。
        /// </summary>
        public static float EliteCooption(float localEliteCount, float opportunityOffered,
            ColonialAdministrationParams p)
        {
            float elite = Mathf.Max(0f, localEliteCount);
            float opp = Mathf.Clamp01(opportunityOffered);
            float v = elite * opp;
            if (v <= 0f) return 0f;
            float raw = v / (v + 1f); // 0..1へ飽和
            return Mathf.Clamp(raw, 0f, p.maxCooption);
        }

        public static float EliteCooption(float localEliteCount, float opportunityOffered)
            => EliteCooption(localEliteCount, opportunityOffered, ColonialAdministrationParams.Default);

        /// <summary>
        /// 行政の実効性（-1..1）＝中央統制＋取り込み×取り込み重み − 反発×反発抑え。本国の統制と現地名士の
        /// 協力が反発を上回れば正（行政が回る）、反発が勝てば負（統治の空洞化）。現地登用は統制を補う。
        /// </summary>
        public static float AdministrativeEffectiveness(float centralControl, float eliteCooption,
            float colonialResentment, ColonialAdministrationParams p)
        {
            float control = Mathf.Clamp01(centralControl);
            float coopt = Mathf.Clamp01(eliteCooption);
            float res = Mathf.Clamp01(colonialResentment);
            float net = control + coopt * p.cooptionWeight - res * p.resentmentDrag;
            return Mathf.Clamp(net, -1f, 1f);
        }

        public static float AdministrativeEffectiveness(float centralControl, float eliteCooption,
            float colonialResentment)
            => AdministrativeEffectiveness(centralControl, eliteCooption, colonialResentment,
                ColonialAdministrationParams.Default);

        /// <summary>
        /// 植民地が安定か（bool）＝行政実効性 ≥ 閾値 かつ 反発 &lt; (1−閾値の下限)。行政が一定以上回り、
        /// なお反発が高すぎない（1−閾値 未満）なら安定。閾値が高いほど安定の条件が厳しくなる。
        /// </summary>
        public static bool IsColonyStable(float administrativeEffectiveness, float colonialResentment,
            float threshold)
        {
            float eff = Mathf.Clamp(administrativeEffectiveness, -1f, 1f);
            float res = Mathf.Clamp01(colonialResentment);
            float th = Mathf.Clamp01(threshold);
            return eff >= th && res < (1f - th);
        }

        /// <summary>既定閾値0.3（行政実効性0.3以上かつ反発0.7未満）で安定判定。</summary>
        public static bool IsColonyStable(float administrativeEffectiveness, float colonialResentment)
            => IsColonyStable(administrativeEffectiveness, colonialResentment, 0.3f);
    }
}
