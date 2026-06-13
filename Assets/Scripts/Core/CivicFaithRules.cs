using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 市民宗教（religion civile）の純データ。政府が政治的結束のために定める世俗の信条。
    /// </summary>
    public struct CivicFaith
    {
        /// <summary>信奉（0..1）＝市民が市民宗教をどれだけ信じているか。</summary>
        public float devotion;
        /// <summary>儀礼の活力（0..1）＝式典・国家行事の盛んさ（外形）。</summary>
        public float ritualVitality;
        /// <summary>内面の真摯さ（0..1）＝本心からの信仰（信じている度合い）。</summary>
        public float sincerity;

        public CivicFaith(float devotion, float ritualVitality, float sincerity)
        {
            this.devotion = Mathf.Clamp01(devotion);
            this.ritualVitality = Mathf.Clamp01(ritualVitality);
            this.sincerity = Mathf.Clamp01(sincerity);
        }
    }

    /// <summary>市民宗教の調整係数（純構造体・既定 .Default）。マジックナンバーを1か所へ集約する。</summary>
    public readonly struct CivicFaithParams
    {
        /// <summary>政府の製造（儀礼・教育）が信奉を押し上げる速さ（/戦略秒）。</summary>
        public readonly float manufactureSpeed;
        /// <summary>製造で到達しうる信奉の上限（上からの信仰は天井がある）。</summary>
        public readonly float manufactureCeiling;
        /// <summary>真摯さが薄れる形骸化ドリフトの速さ（/戦略秒）。</summary>
        public readonly float ritualizationSpeed;
        /// <summary>急速崩壊と判定する空疎度の閾値（hollowFaith がこれ以上で崩壊しうる）。</summary>
        public readonly float collapseThreshold;
        /// <summary>崩壊時に信奉が剥がれ落ちる最大量（空疎×衝撃の係数）。</summary>
        public readonly float collapseScale;
        /// <summary>正統性の神聖化が法を聖化する最大ボーナス。</summary>
        public readonly float sanctificationScale;
        /// <summary>市民宗教が生きていると判定する真摯さ込みの閾値。</summary>
        public readonly float vitalityThreshold;

        public CivicFaithParams(float manufactureSpeed, float manufactureCeiling, float ritualizationSpeed,
            float collapseThreshold, float collapseScale, float sanctificationScale, float vitalityThreshold)
        {
            this.manufactureSpeed = Mathf.Max(0f, manufactureSpeed);
            this.manufactureCeiling = Mathf.Clamp01(manufactureCeiling);
            this.ritualizationSpeed = Mathf.Max(0f, ritualizationSpeed);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
            this.collapseScale = Mathf.Max(0f, collapseScale);
            this.sanctificationScale = Mathf.Max(0f, sanctificationScale);
            this.vitalityThreshold = Mathf.Clamp01(vitalityThreshold);
        }

        /// <summary>
        /// 既定＝製造速0.04・製造天井0.85・形骸化速0.02・崩壊閾値0.5・崩壊係数0.6・神聖化0.25・活力閾値0.4。
        /// </summary>
        public static CivicFaithParams Default => new CivicFaithParams(
            manufactureSpeed: 0.04f,
            manufactureCeiling: 0.85f,
            ritualizationSpeed: 0.02f,
            collapseThreshold: 0.5f,
            collapseScale: 0.6f,
            sanctificationScale: 0.25f,
            vitalityThreshold: 0.4f);
    }

    /// <summary>
    /// 市民宗教（religion civile）の純ロジック（ROUS-4 #1468・ルソー『社会契約論』第4篇 参考）。
    /// 政府が政治的結束のために製造する世俗の信仰（社会契約と法の神聖性・愛国・寛容）が、市民を国家へ
    /// 情緒的に結びつけるが、内面の真摯さが薄れて形骸化（儀礼だけが残る）すると、製造された信仰ゆえに
    /// 衝撃で急速に崩壊しうる——「上から作った信仰は、信じられなくなると一気に剥がれる」を式に出す。
    /// 改宗圧力と聖戦は <see cref="ReligionRules"/>、意味の喪失＝末人は <see cref="HopeRules"/>、
    /// 儀礼の空疎は <see cref="CeremonyRules"/>（IsHollow と整合）、一般意志は同EPIC ROUS の GeneralWillRules が扱い、
    /// ここはルソーの市民宗教（政治的結束の世俗信仰＝<see cref="CivicFaith"/> が中核データ）に限る。
    /// 乱数なし・決定論。Game層非依存＝Core 純ロジック（test-first）。
    /// </summary>
    public static class CivicFaithRules
    {
        /// <summary>
        /// 政治的結束(0..1)＝市民宗教への信奉×共有された信条（sharedCreed）。信奉が高くても信条が共有されて
        /// いなければ結束は生まれない（積＝どちらも要る）。国家への情緒的紐帯の強さ。
        /// </summary>
        public static float CivicCohesion(float devotion, float sharedCreed)
        {
            return Mathf.Clamp01(devotion) * Mathf.Clamp01(sharedCreed);
        }

        /// <summary>
        /// 政府が儀礼・教育で市民宗教を製造・強化する1tick（上からの信仰）。statePromotion(0..1) の力で
        /// 信奉を製造天井へ寄せる。dt 比例・天井あり＝上から作れる信仰には限界がある。
        /// </summary>
        public static float ManufacturedFaithTick(float devotion, float statePromotion, float deltaTime, CivicFaithParams p)
        {
            float d = Mathf.Clamp01(devotion);
            if (deltaTime <= 0f) return d;
            float promo = Mathf.Clamp01(statePromotion);
            float target = p.manufactureCeiling * promo;     // 推進が弱いほど到達点も低い
            if (target <= d) return d;                        // 製造は押し上げるだけ（自然減衰は別系統）
            return Mathf.MoveTowards(d, target, p.manufactureSpeed * promo * deltaTime);
        }

        public static float ManufacturedFaithTick(float devotion, float statePromotion, float deltaTime)
            => ManufacturedFaithTick(devotion, statePromotion, deltaTime, CivicFaithParams.Default);

        /// <summary>
        /// 形骸化ドリフトの1tick：儀礼が盛んでも内面の真摯さは時間で薄れる（信じてないが形だけ残る）。
        /// 儀礼活力が高いほど形だけの参加が促され真摯さの摩耗が速い＝新しい真摯さを返す（基準非破壊・実効値パターン）。
        /// </summary>
        public static float RitualizationDrift(float sincerity, float ritualVitality, float deltaTime, CivicFaithParams p)
        {
            float s = Mathf.Clamp01(sincerity);
            if (deltaTime <= 0f) return s;
            float vit = Mathf.Clamp01(ritualVitality);
            float drift = p.ritualizationSpeed * vit * deltaTime;   // 儀礼が盛んなほど形骸化が速い
            return Mathf.MoveTowards(s, 0f, drift);
        }

        public static float RitualizationDrift(float sincerity, float ritualVitality, float deltaTime)
            => RitualizationDrift(sincerity, ritualVitality, deltaTime, CivicFaithParams.Default);

        /// <summary>
        /// 空疎な信仰(0..1)＝儀礼は盛んだが内面が空っぽ。儀礼活力×(1−真摯さ)。
        /// 活力が高いほど・真摯さが低いほど空疎＝<see cref="CeremonyRules.IsHollow"/>（外形と実態の落差）と整合する。
        /// </summary>
        public static float HollowFaith(float ritualVitality, float sincerity)
        {
            return Mathf.Clamp01(ritualVitality) * (1f - Mathf.Clamp01(sincerity));
        }

        /// <summary>
        /// 正統性の神聖化(0..1)：市民宗教が法と社会契約を神聖化し正統性を高める。政治的結束(civicCohesion)
        /// ×法の神聖視(lawSacredness)×scale。結束も法の聖性も要る（積）＝ルソーの「法の神聖性」。
        /// </summary>
        public static float LegitimacySanctification(float civicCohesion, float lawSacredness, CivicFaithParams p)
        {
            float c = Mathf.Clamp01(civicCohesion);
            float law = Mathf.Clamp01(lawSacredness);
            return Mathf.Clamp01(c * law * p.sanctificationScale);
        }

        public static float LegitimacySanctification(float civicCohesion, float lawSacredness)
            => LegitimacySanctification(civicCohesion, lawSacredness, CivicFaithParams.Default);

        /// <summary>
        /// 形骸化した市民宗教の急速崩壊：空疎度(hollowFaith)が閾値以上のときだけ、衝撃(shock 0..1)に応じて
        /// 信奉が一気に剥がれ落ちる量(0..1)を返す。空疎でない（真摯な）信仰は衝撃に耐える＝崩壊量0。
        /// 製造された信仰の脆さ＝「信じてないと一気に剥がれる」。
        /// </summary>
        public static float SuddenCollapse(float hollowFaith, float shock, float threshold)
        {
            float hollow = Mathf.Clamp01(hollowFaith);
            float th = Mathf.Clamp01(threshold);
            if (hollow < th) return 0f;                          // 真摯な信仰は衝撃に耐える
            float sh = Mathf.Clamp01(shock);
            return Mathf.Clamp01(hollow * sh * CivicFaithParams.Default.collapseScale);
        }

        /// <summary>
        /// 形骸化した市民宗教の急速崩壊（Params 版＝collapseThreshold/collapseScale を使う）。
        /// </summary>
        public static float SuddenCollapse(float hollowFaith, float shock, CivicFaithParams p)
        {
            float hollow = Mathf.Clamp01(hollowFaith);
            if (hollow < p.collapseThreshold) return 0f;
            float sh = Mathf.Clamp01(shock);
            return Mathf.Clamp01(hollow * sh * p.collapseScale);
        }

        /// <summary>
        /// 寛容の要件(0..1)：ルソーの市民宗教は寛容を旨とし、不寛容な教義(dogmatism)は排除する。
        /// 市民宗教の強さ(civicFaith)が高いほど結束を生むが、教条主義が強いと市民宗教自体が狂信へ堕す危険を
        /// 差し引く＝civicFaith×(1−dogmatism)。寛容が保たれてこそ市民宗教は健全。
        /// </summary>
        public static float ToleranceRequirement(float civicFaith, float dogmatism)
        {
            float cf = Mathf.Clamp01(civicFaith);
            float dog = Mathf.Clamp01(dogmatism);
            return Mathf.Clamp01(cf * (1f - dog));
        }

        /// <summary>
        /// 市民宗教が生きて結束を生んでいるか：信奉と真摯さの両方が閾値を満たすか。
        /// 信奉が高くても内面が空疎（真摯さ低）なら「生きていない」＝形骸化した信仰は結束を生まない。
        /// </summary>
        public static bool IsCivicReligionVital(float devotion, float sincerity, float threshold)
        {
            float th = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(devotion) >= th && Mathf.Clamp01(sincerity) >= th;
        }
    }
}
