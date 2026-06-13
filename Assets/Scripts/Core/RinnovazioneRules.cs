using UnityEngine;

namespace Ginei
{
    /// <summary>リノヴァツィオーネ（刷新＝初心への回帰）の調整係数（マキャヴェッリ『ディスコルシ』第3巻第1章・DISC-3 #1488）。</summary>
    public readonly struct RinnovazioneParams
    {
        /// <summary>制度年齢0・改革放置0でも進む基礎の疲弊蓄積/秒。</summary>
        public readonly float baseFatigueRate;
        /// <summary>制度の古さ(0..1)が疲弊蓄積を増幅する幅（古い制度ほど疲れる）。</summary>
        public readonly float ageFatigueScale;
        /// <summary>改革の放置(0..1)が疲弊蓄積を増幅する幅（怠るほど腐る）。</summary>
        public readonly float neglectFatigueScale;
        /// <summary>刷新を起こすための疲弊×危機の窓の開きやすさ係数。</summary>
        public readonly float windowScale;
        /// <summary>定期的な予防刷新が疲弊を削る基礎レート/秒（規則性1のとき）。</summary>
        public readonly float preventiveRate;
        /// <summary>これ以上の疲弊で刷新が手遅れ＝暴力的崩壊でしか直せない閾値。</summary>
        public readonly float overdueThreshold;
        /// <summary>刷新で創設時の徳をこの割合まで取り戻せば「刷新された制度」と見なす閾値。</summary>
        public readonly float renewedThreshold;

        public RinnovazioneParams(float baseFatigueRate, float ageFatigueScale, float neglectFatigueScale,
            float windowScale, float preventiveRate, float overdueThreshold, float renewedThreshold)
        {
            this.baseFatigueRate = Mathf.Max(0f, baseFatigueRate);
            this.ageFatigueScale = Mathf.Max(0f, ageFatigueScale);
            this.neglectFatigueScale = Mathf.Max(0f, neglectFatigueScale);
            this.windowScale = Mathf.Max(0f, windowScale);
            this.preventiveRate = Mathf.Max(0f, preventiveRate);
            this.overdueThreshold = Mathf.Clamp01(overdueThreshold);
            this.renewedThreshold = Mathf.Clamp01(renewedThreshold);
        }

        /// <summary>
        /// 既定＝基礎疲弊0.02・年齢増幅0.05・放置増幅0.05・窓係数1.0・予防刷新0.1・手遅れ閾値0.8・刷新判定0.85。
        /// 手遅れ閾値0.8＝疲弊が8割を超えたら予防の機を逃し暴力的崩壊（革命）でしか直せない。
        /// </summary>
        public static RinnovazioneParams Default =>
            new RinnovazioneParams(0.02f, 0.05f, 0.05f, 1f, 0.1f, 0.8f, 0.85f);
    }

    /// <summary>
    /// リノヴァツィオーネ（刷新＝初心への回帰）の純ロジック（マキャヴェッリ『ディスコルシ』第3巻第1章・DISC-3 #1488）。
    /// 混合政体・共和国・宗教は<b>定期的に始原（創設時の原理・徳）へ立ち返ること</b>で存続する＝rinnovazione。
    /// 制度は放置すると疲弊し腐敗するが、初心へ立ち返る刷新を行えば若返る。刷新は<b>危機（衝撃）・傑出した
    /// 個人・優れた法</b>のいずれかによって起きる。疲弊が刷新ウィンドウを開き、危機を待たぬ予防的な自己刷新が
    /// 暴力的崩壊を防ぐ＝予防を逃すと革命でしか直せなくなる。
    /// <see cref="DynastyRules"/>.Reform（王朝の制度更新＝腐敗を下げ正統性を回復する事後対処）・
    /// <see cref="RegimeRules"/>（腐敗・正統性・徳そのものの動態）・<c>InstitutionalCorrectionRules</c>（誤りの
    /// 修正）・<c>FounderTrajectoryRules</c>（同EPIC DISC＝建国者の軌跡）とは分担し、ここは<b>危機前の予防的刷新
    /// （マキャヴェッリの「初心に立ち返る」rinnovazione）＝疲弊の蓄積・刷新ウィンドウ・始原回帰・手遅れリスク</b>を
    /// 扱う。すべて plain な float で受け渡す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RinnovazioneRules
    {
        /// <summary>
        /// 制度疲弊の蓄積（dt後のfatigue 0..1）。蓄積率＝基礎＋制度の古さ×年齢増幅＋改革放置×放置増幅。
        /// 制度が古く改革を怠るほど速く疲弊が溜まる＝<b>放置すると腐る</b>。建国直後（年齢0・放置0）でも
        /// 基礎ぶんだけは疲弊する。
        /// </summary>
        public static float InstitutionalFatigue(float institutionAge, float neglectedReform, float dt,
            RinnovazioneParams p)
        {
            float age = Mathf.Clamp01(institutionAge);
            float neglect = Mathf.Clamp01(neglectedReform);
            float step = Mathf.Max(0f, dt);
            float rate = p.baseFatigueRate
                + age * p.ageFatigueScale
                + neglect * p.neglectFatigueScale;
            // fatigue は現在値ではなく蓄積分のみ返さず、現在疲弊に積む形にしない＝この関数は
            // 「dt経過で増える疲弊量」を返さず、呼び出し側が現在疲弊を渡す（始原回帰で減る）。
            return Mathf.Clamp01(rate * step);
        }

        public static float InstitutionalFatigue(float institutionAge, float neglectedReform, float dt)
            => InstitutionalFatigue(institutionAge, neglectedReform, dt, RinnovazioneParams.Default);

        /// <summary>
        /// 刷新ウィンドウ（0..1＝刷新の機会の窓の開き具合）＝疲弊×危機×窓係数。疲弊が溜まり危機が来たとき
        /// ほど刷新が可能になる＝<b>危機が改革を可能にする</b>。疲弊も危機もなければ窓は開かない（平時の安泰は
        /// 刷新の動機を奪う）。
        /// </summary>
        public static float RenewalWindow(float institutionalFatigue, float crisisShock, RinnovazioneParams p)
        {
            float fatigue = Mathf.Clamp01(institutionalFatigue);
            float shock = Mathf.Clamp01(crisisShock);
            return Mathf.Clamp01(fatigue * shock * p.windowScale);
        }

        public static float RenewalWindow(float institutionalFatigue, float crisisShock)
            => RenewalWindow(institutionalFatigue, crisisShock, RinnovazioneParams.Default);

        /// <summary>
        /// 始原回帰の効果（dt後の徳ではなく刷新後の現在徳 0..1）＝現在徳から創設時の徳へ刷新努力ぶん近づく。
        /// 創設時の原理・徳へ立ち返る刷新＝<b>初心に戻ると若返る</b>。努力が大きいほど founding へ寄り、努力0なら
        /// 据え置き（自然には戻らない＝能動的な刷新が要る）。創設時より高くは戻らない（始原＝上限）。
        /// </summary>
        public static float ReturnToOrigins(float currentVirtue, float foundingVirtue, float renewalEffort)
        {
            float cur = Mathf.Clamp01(currentVirtue);
            float founding = Mathf.Clamp01(foundingVirtue);
            float effort = Mathf.Clamp01(renewalEffort);
            // 創設時の徳へ effort ぶん補間（始原へ立ち返る）。founding<cur のときも founding へ寄る＝
            // 過剰肥大した制度を初心の簡素へ戻す意味も含む。
            return Mathf.Clamp01(Mathf.Lerp(cur, founding, effort));
        }

        /// <summary>
        /// 予防的刷新（dt後のfatigue 0..1）＝定期的な刷新の規則性に比例して疲弊を継続的に削る。危機を待たず
        /// 平時から規則的に初心へ立ち返れば疲弊が積み上がらず<b>若返らせ続けられる</b>。規則性0なら削れない
        /// （刷新を怠れば疲弊は溜まる一方）。
        /// </summary>
        public static float PreventiveRenewalTick(float institutionalFatigue, float renewalRegularity, float dt,
            RinnovazioneParams p)
        {
            float fatigue = Mathf.Clamp01(institutionalFatigue);
            float regularity = Mathf.Clamp01(renewalRegularity);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(fatigue - p.preventiveRate * regularity * step);
        }

        public static float PreventiveRenewalTick(float institutionalFatigue, float renewalRegularity, float dt)
            => PreventiveRenewalTick(institutionalFatigue, renewalRegularity, dt, RinnovazioneParams.Default);

        /// <summary>
        /// 刷新の契機（0..1＝刷新を起こす力の強さ）＝危機・傑出した個人・優れた法の三つのうち最も強いもの。
        /// マキャヴェッリの三つの契機（危機の衝撃／傑物／良法）のいずれか一つでも強ければ刷新は起こりうる＝
        /// <b>OR で最大値</b>。三つとも弱ければ刷新の力は弱い。
        /// </summary>
        public static float RenewalTrigger(float crisisShock, float exceptionalIndividual, float goodLaws)
        {
            float crisis = Mathf.Clamp01(crisisShock);
            float individual = Mathf.Clamp01(exceptionalIndividual);
            float laws = Mathf.Clamp01(goodLaws);
            return Mathf.Max(crisis, Mathf.Max(individual, laws));
        }

        /// <summary>
        /// 手遅れリスク（true＝刷新が手遅れ＝暴力的崩壊・再起動でしか直せない）。疲弊が閾値を超えると予防の機を
        /// 逃した＝<b>予防を逃すと革命でしか直せなくなる</b>。閾値内なら予防的刷新でまだ救える。
        /// </summary>
        public static bool OverdueRenewalRisk(float institutionalFatigue, float threshold)
            => Mathf.Clamp01(institutionalFatigue) >= Mathf.Clamp01(threshold);

        public static bool OverdueRenewalRisk(float institutionalFatigue)
            => OverdueRenewalRisk(institutionalFatigue, RinnovazioneParams.Default.overdueThreshold);

        /// <summary>
        /// 刷新コスト（0..1＝刷新に要するコスト）＝疲弊が深いほど高くつく（恒等写像＝疲弊そのもの）。
        /// 早く手を打つほど安く、放置して疲弊を募らせるほど刷新は高くつく＝<b>予防は安く治療は高い</b>。
        /// </summary>
        public static float RenewalCost(float institutionalFatigue) => Mathf.Clamp01(institutionalFatigue);

        /// <summary>
        /// 刷新された制度の判定（true＝刷新されて創設時の徳を取り戻した）。現在の徳が創設時の徳の閾値割合
        /// 以上を回復していれば成立＝<b>初心に立ち返り若返った</b>。創設時の徳がゼロなら取り戻すべき始原が
        /// 無く成立しない。
        /// </summary>
        public static bool IsRenewedInstitution(float currentVirtue, float foundingVirtue, float threshold,
            RinnovazioneParams p)
        {
            float cur = Mathf.Clamp01(currentVirtue);
            float founding = Mathf.Clamp01(foundingVirtue);
            float th = Mathf.Clamp01(threshold);
            if (founding <= 0f) return false;
            return cur >= founding * th;
        }

        public static bool IsRenewedInstitution(float currentVirtue, float foundingVirtue, float threshold)
            => IsRenewedInstitution(currentVirtue, foundingVirtue, threshold, RinnovazioneParams.Default);

        public static bool IsRenewedInstitution(float currentVirtue, float foundingVirtue)
            => IsRenewedInstitution(currentVirtue, foundingVirtue, RinnovazioneParams.Default.renewedThreshold,
                RinnovazioneParams.Default);
    }
}
