using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 同盟の結束度（信頼性）を解く純ロジック。
    /// 結束（0..1）は「共通の脅威」と「相互の好感度」で上がり、「国力の非対称」（突出した盟主が
    /// 不公平感を生む）と「加盟国数」（調整コスト）で下がる＝外敵がいて仲が良い対等な少数同盟ほど固い。
    /// DiplomacyRules（外交状態の遷移）や AllianceChainRules（連鎖参戦）とは別系統＝
    /// こちらは「いざというとき同盟が持ちこたえるか」という結束の強度だけを解く。
    /// 乱数なし・決定論・後方互換。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AllianceCohesionRules
    {
        /// <summary>同盟結束の調整係数。</summary>
        public readonly struct Params
        {
            /// <summary>共通脅威が結束へ寄与する重み。</summary>
            public readonly float threatWeight;
            /// <summary>相互好感度が結束へ寄与する重み。</summary>
            public readonly float opinionWeight;
            /// <summary>国力非対称が結束を削る重み。</summary>
            public readonly float asymmetryPenalty;
            /// <summary>加盟国1国増えるごとの調整コスト（基準は2国＝最小同盟）。</summary>
            public readonly float perMemberPenalty;

            public Params(float threatWeight, float opinionWeight, float asymmetryPenalty, float perMemberPenalty)
            {
                this.threatWeight = Mathf.Max(0f, threatWeight);
                this.opinionWeight = Mathf.Max(0f, opinionWeight);
                this.asymmetryPenalty = Mathf.Max(0f, asymmetryPenalty);
                this.perMemberPenalty = Mathf.Max(0f, perMemberPenalty);
            }

            /// <summary>既定＝脅威重み0.5・好感度重み0.5・非対称ペナルティ0.4・加盟国コスト0.05/国。</summary>
            public static Params Default => new Params(0.5f, 0.5f, 0.4f, 0.05f);
        }

        /// <summary>
        /// 同盟結束度（0..1）。共通脅威(0..1)×脅威重み ＋ 好感度正規化(0..1)×好感度重み
        ///   − 国力非対称(0..1)×非対称ペナルティ − 超過加盟国数×加盟国コスト。
        /// 好感度(-100..100)は (opinion+100)/200 で 0..1 へ正規化。
        /// 加盟国数は2国を基準とし、それを超えるぶんだけ調整コストを引く。最終的に [0,1] へクランプ。
        /// </summary>
        public static float Cohesion(float sharedThreat01, float mutualOpinion, float powerAsymmetry01, int memberCount, Params p)
        {
            float threat = Mathf.Clamp01(sharedThreat01) * p.threatWeight;            // 共通の敵が同盟を固める
            float opinion = Mathf.Clamp01((Mathf.Clamp(mutualOpinion, -100f, 100f) + 100f) / 200f) * p.opinionWeight; // 仲の良さ
            float asymmetry = Mathf.Clamp01(powerAsymmetry01) * p.asymmetryPenalty;   // 突出した盟主は不公平感を生む
            int excessMembers = Mathf.Max(0, memberCount - 2);                        // 2国を超えるぶんが調整コスト
            float crowd = excessMembers * p.perMemberPenalty;                         // 加盟国が多いほどまとまらない

            float cohesion = threat + opinion - asymmetry - crowd;
            return Mathf.Clamp01(cohesion);
        }

        public static float Cohesion(float sharedThreat01, float mutualOpinion, float powerAsymmetry01, int memberCount)
            => Cohesion(sharedThreat01, mutualOpinion, powerAsymmetry01, memberCount, Params.Default);

        /// <summary>
        /// 与えられた負荷（stressTest 0..1＝離反の誘惑/裏切りの圧力）に同盟が耐えるか。
        /// 結束が負荷以上なら持ちこたえる（true）。
        /// </summary>
        public static bool WillHold(float cohesion, float stressTest01)
            => cohesion >= stressTest01;
    }
}
