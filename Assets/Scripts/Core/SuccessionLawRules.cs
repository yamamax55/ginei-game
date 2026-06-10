using UnityEngine;

namespace Ginei
{
    /// <summary>継承法の種別（PDX-1 #646）。家督/版図の継ぎ方を決める。</summary>
    public enum SuccessionLaw
    {
        長子, // 長男総取り（プリモジェニチャー＝集権・安定）
        分割, // 全相続人で均等分割（ガヴェルカインド＝分裂しやすい）
        指名, // 当主が後継を指名する
        選挙, // 有力者の中から最有力を選ぶ
    }

    /// <summary>
    /// 継承法の純ロジック（PDX-1 #646・パラドックス型継承モデル）。家督/版図の継ぎ方（長子＝総取り／分割＝均等／
    /// 指名／選挙）から<b>相続割合</b>と<b>継承戦争リスク</b>を決定論的に解く。分割相続は版図を分裂させ正統性が低いほど
    /// 揉める＝継承戦争。乱数は持たず割合と確率のみを返す（発火判定は呼び出し側 roll）。値は徹底クランプ。test-first。
    /// </summary>
    public static class SuccessionLawRules
    {
        /// <summary>継承戦争リスクの調整値（基準リスク・分割相続の加算・正統性の効き）。</summary>
        public readonly struct SuccessionParams
        {
            /// <summary>相続人が複数いるときの基準リスク。</summary>
            public readonly float baseRisk;
            /// <summary>分割相続での追加リスク（版図分裂＝最も揉める）。</summary>
            public readonly float partitionPenalty;
            /// <summary>指名/選挙での追加リスク（明文の長子より曖昧）。</summary>
            public readonly float disputedPenalty;
            /// <summary>正統性1.0でリスクを最大どれだけ削るか（高正統＝揉めない）。</summary>
            public readonly float legitimacyRelief;
            /// <summary>相続人1人増えるごとの追加リスク（兄弟が多いほど火種）。</summary>
            public readonly float perHeirRisk;

            public SuccessionParams(float baseRisk, float partitionPenalty, float disputedPenalty,
                float legitimacyRelief, float perHeirRisk)
            {
                this.baseRisk = baseRisk;
                this.partitionPenalty = partitionPenalty;
                this.disputedPenalty = disputedPenalty;
                this.legitimacyRelief = legitimacyRelief;
                this.perHeirRisk = perHeirRisk;
            }

            public static SuccessionParams Default => new SuccessionParams(0.1f, 0.4f, 0.2f, 0.5f, 0.05f);
        }

        /// <summary>
        /// 相続人 <paramref name="heirIndex"/>（0=第一相続人＝長男/指名者/最有力）が受け取る相続割合（0..1）を返す。
        /// 長子＝第一相続人が総取り（他は0）／分割＝均等（1/heirCount）／指名・選挙＝指定された者（index0）が総取り。
        /// </summary>
        public static float HeirShare(SuccessionLaw law, int heirIndex, int heirCount)
        {
            if (heirCount <= 0) return 0f;
            int idx = Mathf.Clamp(heirIndex, 0, heirCount - 1);
            switch (law)
            {
                case SuccessionLaw.分割:
                    return 1f / heirCount; // 均等分割（版図が割れる）
                case SuccessionLaw.長子:
                case SuccessionLaw.指名:
                case SuccessionLaw.選挙:
                default:
                    return idx == 0 ? 1f : 0f; // 第一相続人（長男/指名者/最有力）が総取り
            }
        }

        /// <summary>
        /// 継承戦争（家督争い）リスク（0..1）を返す。相続人が1人以下なら争いは起きず0。複数なら基準＋人数＋継承法の
        /// 加算（分割が最大・指名/選挙が中・長子は明文ゆえ最小）から正統性ぶんを差し引く。決定論＝発火は呼び出し側 roll。
        /// </summary>
        public static float SuccessionCrisisRisk(SuccessionLaw law, int heirCount, float legitimacy, SuccessionParams p)
        {
            if (heirCount <= 1) return 0f; // 相続人が1人以下＝争いの相手がいない
            int extraHeirs = heirCount - 1;
            float risk = p.baseRisk + extraHeirs * p.perHeirRisk;
            switch (law)
            {
                case SuccessionLaw.分割:
                    risk += p.partitionPenalty; // 版図分裂＝最も揉める
                    break;
                case SuccessionLaw.指名:
                case SuccessionLaw.選挙:
                    risk += p.disputedPenalty; // 曖昧ゆえ中程度
                    break;
                case SuccessionLaw.長子:
                default:
                    break; // 明文の長子優先＝追加なし
            }
            risk -= Mathf.Clamp01(legitimacy) * p.legitimacyRelief; // 高正統性は争いを鎮める
            return Mathf.Clamp01(risk);
        }

        public static float SuccessionCrisisRisk(SuccessionLaw law, int heirCount, float legitimacy)
            => SuccessionCrisisRisk(law, heirCount, legitimacy, SuccessionParams.Default);
    }
}
