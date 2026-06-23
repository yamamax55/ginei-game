using UnityEngine;

namespace Ginei
{
    /// <summary>建艦競争（軍拡レース）の調整係数。</summary>
    public readonly struct NavalArmsRaceParams
    {
        /// <summary>相手の建艦速度に対する対抗建艦の追従度（1で等速対抗）。</summary>
        public readonly float responseGain;
        /// <summary>脅威認識が対抗建艦を増幅する強さ（脅威1で何倍まで）。</summary>
        public readonly float threatGain;
        /// <summary>建艦予算が経済を圧迫する強さ（予算×GDP比に対する係数）。</summary>
        public readonly float burdenScale;
        /// <summary>エスカレーションが双方の対抗建艦の積に反応する強さ。</summary>
        public readonly float escalationGain;
        /// <summary>軍縮誘因が経済圧迫×相互疲弊に反応する強さ。</summary>
        public readonly float disarmGain;
        /// <summary>安定度における軍縮誘因（緩和側）の重み。</summary>
        public readonly float stabilityDisarmWeight;
        /// <summary>帰結における技術優越（0.5基準）の重み。</summary>
        public readonly float outcomeTechWeight;
        /// <summary>帰結における経済圧迫（疲弊）の重み。</summary>
        public readonly float outcomeBurdenWeight;
        /// <summary>帰結における暴走（消耗）の重み。</summary>
        public readonly float outcomeEscalationWeight;

        public NavalArmsRaceParams(float responseGain, float threatGain, float burdenScale,
                                   float escalationGain, float disarmGain, float stabilityDisarmWeight,
                                   float outcomeTechWeight, float outcomeBurdenWeight, float outcomeEscalationWeight)
        {
            this.responseGain = Mathf.Max(0f, responseGain);
            this.threatGain = Mathf.Max(0f, threatGain);
            this.burdenScale = Mathf.Max(0f, burdenScale);
            this.escalationGain = Mathf.Max(0f, escalationGain);
            this.disarmGain = Mathf.Max(0f, disarmGain);
            this.stabilityDisarmWeight = Mathf.Clamp01(stabilityDisarmWeight);
            this.outcomeTechWeight = Mathf.Max(0f, outcomeTechWeight);
            this.outcomeBurdenWeight = Mathf.Max(0f, outcomeBurdenWeight);
            this.outcomeEscalationWeight = Mathf.Max(0f, outcomeEscalationWeight);
        }

        /// <summary>既定＝対抗1.0/脅威0.5/圧迫1.0/暴走1.0/軍縮1.0・安定の軍縮重み0.5・帰結の技術1.0/圧迫1.0/暴走1.0。</summary>
        public static NavalArmsRaceParams Default
            => new NavalArmsRaceParams(1f, 0.5f, 1f, 1f, 1f, 0.5f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 建艦競争（海軍軍拡レース）の純ロジック＝勢力間で艦隊を増強し合う相互作用。
    /// 相手の建艦への対抗・質と量の競争・軍事費の経済圧迫・技術的優越の追求・
    /// 軍拡のエスカレーション・軍縮の誘因・軍拡の安定/不安定を扱う。
    /// 核心＝**軍拡は自己強化のループ**：相手の建艦が脅威認識を上げ、対抗建艦を呼び、
    /// それが相手の対抗を呼んで暴走（エスカレーション・スパイラル）する。一方で建艦は経済を圧迫し、
    /// 相互に疲弊すると軍縮の誘因が生まれる。エスカレーションと軍縮誘因の綱引きが
    /// レースの安定/不安定を決め、技術優越−経済圧迫−暴走が最終的な帰結（優勢/疲弊）を決める。
    /// 全入力 clamp・符号付き出力は -1..1。決定論（乱数なし）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class NavalArmsRaceRules
    {
        /// <summary>
        /// 対抗建艦（0..1）＝相手の建艦速度 rivalBuildRate（0..1）×（1＋脅威認識 threatPerception×脅威係数）。
        /// 相手が速く建艦するほど、また脅威に思うほど、こちらの建艦も加速する。
        /// </summary>
        public static float BuildupResponse(float rivalBuildRate, float threatPerception, NavalArmsRaceParams p)
        {
            float rate = Mathf.Clamp01(rivalBuildRate);
            float threat = Mathf.Clamp01(threatPerception);
            return Mathf.Clamp01(rate * p.responseGain * (1f + threat * p.threatGain));
        }

        public static float BuildupResponse(float rivalBuildRate, float threatPerception)
            => BuildupResponse(rivalBuildRate, threatPerception, NavalArmsRaceParams.Default);

        /// <summary>
        /// 質量バランス（0..1）＝技術投資 techInvestment/(技術投資＋量産 massProduction)。
        /// 0=量で押す／1=質を磨く。両0は均衡0.5。技術への配分が高いほど質に寄る。
        /// </summary>
        public static float QualityVsQuantity(float techInvestment, float massProduction, NavalArmsRaceParams p)
        {
            float tech = Mathf.Clamp01(techInvestment);
            float mass = Mathf.Clamp01(massProduction);
            float total = tech + mass;
            if (total <= 0f) return 0.5f;
            return Mathf.Clamp01(tech / total);
        }

        public static float QualityVsQuantity(float techInvestment, float massProduction)
            => QualityVsQuantity(techInvestment, massProduction, NavalArmsRaceParams.Default);

        /// <summary>
        /// 経済圧迫（0..1）＝建艦予算 navalBudget（0..1）×GDP比 gdpShare（0..1）×圧迫係数。
        /// 予算が大きく、かつGDPに占める比率が高いほど、軍拡が経済を蝕む。
        /// </summary>
        public static float EconomicBurden(float navalBudget, float gdpShare, NavalArmsRaceParams p)
        {
            float budget = Mathf.Clamp01(navalBudget);
            float share = Mathf.Clamp01(gdpShare);
            return Mathf.Clamp01(budget * share * p.burdenScale);
        }

        public static float EconomicBurden(float navalBudget, float gdpShare)
            => EconomicBurden(navalBudget, gdpShare, NavalArmsRaceParams.Default);

        /// <summary>
        /// 技術的優越（0..1）＝R&D投資 rdInvestment/(R&D＋敵技術 rivalTech)。
        /// 0.5基準＝拮抗。R&Dが敵を上回れば0.5超で技術優越、下回れば劣後。両0は0.5。
        /// </summary>
        public static float TechnologicalEdge(float rdInvestment, float rivalTech, NavalArmsRaceParams p)
        {
            float rd = Mathf.Clamp01(rdInvestment);
            float rival = Mathf.Clamp01(rivalTech);
            float total = rd + rival;
            if (total <= 0f) return 0.5f;
            return Mathf.Clamp01(rd / total);
        }

        public static float TechnologicalEdge(float rdInvestment, float rivalTech)
            => TechnologicalEdge(rdInvestment, rivalTech, NavalArmsRaceParams.Default);

        /// <summary>
        /// 軍拡のエスカレーション（0..1）＝こちらの対抗建艦 buildupResponse と相手の対抗建艦 rivalResponse の
        /// 積（双方が増強し合うほど跳ねる）×係数。片方が建艦をやめれば暴走しない＝相互作用が燃料。
        /// </summary>
        public static float EscalationSpiral(float buildupResponse, float rivalResponse, NavalArmsRaceParams p)
        {
            float mine = Mathf.Clamp01(buildupResponse);
            float theirs = Mathf.Clamp01(rivalResponse);
            return Mathf.Clamp01(mine * theirs * p.escalationGain);
        }

        public static float EscalationSpiral(float buildupResponse, float rivalResponse)
            => EscalationSpiral(buildupResponse, rivalResponse, NavalArmsRaceParams.Default);

        /// <summary>
        /// 軍縮の誘因（0..1）＝経済圧迫 economicBurden（0..1）×相互疲弊 mutualExhaustion（0..1）×係数。
        /// 軍拡が経済を蝕み、かつ双方が疲弊しているときにだけ、軍縮の機運が高まる。
        /// </summary>
        public static float DisarmamentIncentive(float economicBurden, float mutualExhaustion, NavalArmsRaceParams p)
        {
            float burden = Mathf.Clamp01(economicBurden);
            float exhaustion = Mathf.Clamp01(mutualExhaustion);
            return Mathf.Clamp01(burden * exhaustion * p.disarmGain);
        }

        public static float DisarmamentIncentive(float economicBurden, float mutualExhaustion)
            => DisarmamentIncentive(economicBurden, mutualExhaustion, NavalArmsRaceParams.Default);

        /// <summary>
        /// 軍拡の安定度（-1..1）＝（1−エスカレーション）に軍縮誘因（緩和側）を加味し中心化。
        /// 正＝安定（暴走せず収まる）／負＝不安定（スパイラルが止まらない）。
        /// stability = (1 − escalation) + disarm×重み を 0..1 に収め、2倍−1 で -1..1 へ写す。
        /// </summary>
        public static float RaceStability(float escalationSpiral, float disarmamentIncentive, NavalArmsRaceParams p)
        {
            float esc = Mathf.Clamp01(escalationSpiral);
            float disarm = Mathf.Clamp01(disarmamentIncentive);
            float raw = Mathf.Clamp01((1f - esc) + disarm * p.stabilityDisarmWeight);
            return Mathf.Clamp(raw * 2f - 1f, -1f, 1f);
        }

        public static float RaceStability(float escalationSpiral, float disarmamentIncentive)
            => RaceStability(escalationSpiral, disarmamentIncentive, NavalArmsRaceParams.Default);

        /// <summary>
        /// 建艦競争の帰結（-1..1）＝技術優越（0.5基準の偏差）×重み − 経済圧迫×重み − 暴走×重み。
        /// 正＝優勢（技術で抜きん出て競争を制す）／負＝疲弊（経済圧迫と暴走に飲まれる）。
        /// 技術優越は0.5中心なので (edge−0.5)×2 で -1..1 の偏差に直してから合成する。
        /// </summary>
        public static float ArmsRaceOutcome(float technologicalEdge, float economicBurden, float escalationSpiral, NavalArmsRaceParams p)
        {
            float edge = (Mathf.Clamp01(technologicalEdge) - 0.5f) * 2f;
            float burden = Mathf.Clamp01(economicBurden);
            float esc = Mathf.Clamp01(escalationSpiral);
            float raw = edge * p.outcomeTechWeight
                        - burden * p.outcomeBurdenWeight
                        - esc * p.outcomeEscalationWeight;
            return Mathf.Clamp(raw, -1f, 1f);
        }

        public static float ArmsRaceOutcome(float technologicalEdge, float economicBurden, float escalationSpiral)
            => ArmsRaceOutcome(technologicalEdge, economicBurden, escalationSpiral, NavalArmsRaceParams.Default);
    }
}
