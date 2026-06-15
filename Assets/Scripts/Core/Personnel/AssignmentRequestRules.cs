using UnityEngine;

namespace Ginei
{
    /// <summary>配属の希望区分（TKO-5・#2482）。前線＝指揮で武勲を狙う／参謀＝中枢で栄達／後方＝安全。</summary>
    public enum PostingPreference { 前線, 参謀, 後方 }

    /// <summary>配属希望への内示（TKO-5）。内示＝希望どおり／代替＝格下の配属を提示／棄却＝却下。</summary>
    public enum AssignmentVerdict { 内示, 代替, 棄却 }

    /// <summary>配属希望の内示結果（TKO-5）。</summary>
    public readonly struct AssignmentDecision
    {
        public readonly AssignmentVerdict verdict;
        /// <summary>内示/代替で割り当てられた配属（棄却時は希望のまま＝意味を持たない）。</summary>
        public readonly PostingPreference granted;
        /// <summary>判定の素点（0..1・観測用）。</summary>
        public readonly float score;

        public AssignmentDecision(AssignmentVerdict verdict, PostingPreference granted, float score)
        {
            this.verdict = verdict;
            this.granted = granted;
            this.score = score;
        }
    }

    /// <summary>
    /// 配属・転任の希望と内示の純ロジック（TKO-5・太閤立志伝V「キャリアの主体性」・#2482・唯一の窓口）。主人公が配属先を希望し、
    /// 人事が<b>武勲（<see cref="MeritRecordRules"/> の実力スコア）・引き立て（<see cref="PersonRelationRules.PromotionFavor"/>）・年功（<see cref="SeniorityRules"/> の序列）</b>で
    /// 内示する＝希望が通るかは功績と人脈次第。配属可否ゲート（在学中は不可）は <see cref="ProtagonistCareerRules.CanDeploy"/>/<see cref="SchoolPostingRules"/>、
    /// 実際の配属先確保は <see cref="FleetRoster"/>/<see cref="SchoolPostingRules"/> へ委譲する（本ルールは可否判定のみ・並行系を作らない＝additive）。決定論・test-first。
    /// </summary>
    public static class AssignmentRequestRules
    {
        /// <summary>内示判定の調整値。</summary>
        public readonly struct AssignmentParams
        {
            public readonly float weightMerit;     // 武勲（実力スコア0..1）の重み
            public readonly float weightFavor;     // 引き立て（人脈・-1..1→0..1）の重み
            public readonly float weightStanding;  // 年功（序列0..1）の重み
            public readonly float grantThreshold;  // この素点以上で内示
            public readonly float altThreshold;    // この素点以上で代替（未満は棄却）

            public AssignmentParams(float weightMerit, float weightFavor, float weightStanding,
                float grantThreshold, float altThreshold)
            {
                this.weightMerit = Mathf.Max(0f, weightMerit);
                this.weightFavor = Mathf.Max(0f, weightFavor);
                this.weightStanding = Mathf.Max(0f, weightStanding);
                this.grantThreshold = Mathf.Clamp01(grantThreshold);
                this.altThreshold = Mathf.Clamp01(altThreshold);
            }

            /// <summary>既定＝武勲0.5・引き立て0.3・年功0.2、内示閾値0.6・代替閾値0.35。</summary>
            public static AssignmentParams Default => new AssignmentParams(0.5f, 0.3f, 0.2f, 0.6f, 0.35f);
        }

        /// <summary>希望が通らなかったときの代替配属（前線→後方／参謀→後方／後方→後方）。前線・中枢は希望が要るが後方は受け皿。</summary>
        public static PostingPreference AlternativeFor(PostingPreference want)
            => PostingPreference.後方;

        /// <summary>
        /// 内示の素点（0..1）＝武勲×引き立て×年功の重み付き和。<paramref name="merit01"/> は <see cref="MeritRecordRules.MeritScore01"/>、
        /// <paramref name="favor"/> は <see cref="PersonRelationRules.PromotionFavor"/>（-1..1。内部で 0..1 へ）、
        /// <paramref name="standing01"/> は年功序列（0..1）。重みは内部で正規化（合計1でなくてよい）。
        /// </summary>
        public static float Score(float merit01, float favor, float standing01, AssignmentParams p)
        {
            float m = Mathf.Clamp01(merit01);
            float f = Mathf.Clamp01((Mathf.Clamp(favor, -1f, 1f) + 1f) * 0.5f);
            float s = Mathf.Clamp01(standing01);
            float wSum = p.weightMerit + p.weightFavor + p.weightStanding;
            if (wSum <= 0f) return 0f;
            return (m * p.weightMerit + f * p.weightFavor + s * p.weightStanding) / wSum;
        }

        /// <summary>
        /// 配属希望を内示する。<paramref name="deployable"/>=false（在学中など配属不可）は無条件で棄却。
        /// それ以外は素点（<see cref="Score"/>）で 内示≥grantThreshold ＞ 代替≥altThreshold ＞ 棄却 を決める。
        /// 後方希望は受け皿＝代替帯でも希望どおり後方を内示する（格下げ先が同じため）。
        /// </summary>
        public static AssignmentDecision Adjudicate(PostingPreference want, bool deployable,
            float merit01, float favor, float standing01, AssignmentParams p)
        {
            if (!deployable)
                return new AssignmentDecision(AssignmentVerdict.棄却, want, 0f);

            float score = Score(merit01, favor, standing01, p);
            if (score >= p.grantThreshold)
                return new AssignmentDecision(AssignmentVerdict.内示, want, score);
            if (score >= p.altThreshold)
            {
                PostingPreference alt = AlternativeFor(want);
                // 後方希望は代替先と同じ＝希望どおり内示扱い。
                AssignmentVerdict v = alt == want ? AssignmentVerdict.内示 : AssignmentVerdict.代替;
                return new AssignmentDecision(v, alt, score);
            }
            return new AssignmentDecision(AssignmentVerdict.棄却, want, score);
        }
    }
}
