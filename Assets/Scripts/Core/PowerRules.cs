using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>実権の合成・傀儡/黒幕判定の調整係数（#164 寡頭支配）。</summary>
    public readonly struct PowerParams
    {
        /// <summary>形式権力(tier)を 0..1 へ正規化する基準（この tier で形式寄与が 1.0）。</summary>
        public readonly float rankScale;
        /// <summary>非公式影響力を策謀で増幅する係数（intrigue×これ ぶん影響力を底上げ）。</summary>
        public readonly float intrigueBoost;
        /// <summary>傀儡判定：形式権力(正規化)がこれ以上なら「肩書は高い」。</summary>
        public readonly float puppetFormalHigh;
        /// <summary>傀儡判定：実効非公式影響力がこれ以下なら「実権は低い」。</summary>
        public readonly float puppetInformalLow;

        public PowerParams(float rankScale, float intrigueBoost, float puppetFormalHigh, float puppetInformalLow)
        {
            this.rankScale = rankScale;
            this.intrigueBoost = intrigueBoost;
            this.puppetFormalHigh = puppetFormalHigh;
            this.puppetInformalLow = puppetInformalLow;
        }

        // 既定：tier10 で形式寄与1.0／策謀は影響力を最大0.3底上げ／肩書0.6以上かつ実権0.3以下を傀儡とみなす。
        public static PowerParams Default => new PowerParams(10f, 0.3f, 0.6f, 0.3f);
    }

    /// <summary>
    /// 寡頭支配と実権（#164）の純ロジック。形式上の地位（肩書＝階級 #14）と非公式の影響力・策謀を
    /// 合成して「実効的な権力」を出す＝肩書≠実権。これにより「位は高いが操られる傀儡」「位は低いが
    /// 場を支配する黒幕（影の支配者）」を判定できる。すべて基準値非破壊（実効値パターン）・test-first。
    /// </summary>
    public static class PowerRules
    {
        /// <summary>形式権力 0..1＝tier を <see cref="PowerParams.rankScale"/> で正規化（上限1.0）。</summary>
        public static float FormalPower(PowerActor a, PowerParams p)
        {
            if (a == null || p.rankScale <= 0f) return 0f;
            return Mathf.Clamp01(a.formalRank / p.rankScale);
        }

        /// <summary>実効的な非公式影響力 0..1＝影響力を策謀で底上げ（基準フィールドは非破壊）。</summary>
        public static float EffectiveInfluence(PowerActor a, PowerParams p)
        {
            if (a == null) return 0f;
            return Mathf.Clamp01(a.informalInfluence + a.intrigue * p.intrigueBoost);
        }

        /// <summary>
        /// 実効的な権力 0..1＝形式権力と実効非公式影響力の合成（大きい方＝肩書か実権の高い方が実権を決める）。
        /// 黒幕は形式が低くても影響力で実権を握り、傀儡は形式が高くても実権が伴わない。
        /// </summary>
        public static float EffectivePower(PowerActor a, PowerParams p)
        {
            if (a == null) return 0f;
            return Mathf.Max(FormalPower(a, p), EffectiveInfluence(a, p));
        }

        public static float EffectivePower(PowerActor a) => EffectivePower(a, PowerParams.Default);

        /// <summary>
        /// 傀儡か＝肩書は高い（形式権力≥puppetFormalHigh）が実権は低い（実効影響力≤puppetInformalLow）。
        /// 位人臣を極めても操り人形＝寡頭支配の被支配側。
        /// </summary>
        public static bool IsPuppet(PowerActor a, PowerParams p)
        {
            if (a == null) return false;
            return FormalPower(a, p) >= p.puppetFormalHigh
                && EffectiveInfluence(a, p) <= p.puppetInformalLow;
        }

        public static bool IsPuppet(PowerActor a) => IsPuppet(a, PowerParams.Default);

        /// <summary>
        /// 影の支配者（黒幕）か＝より高い肩書の者が居る（形式では最高位でない）のに、実効的な
        /// <b>非公式影響力</b>（裏の実権）は一同の中で最も高い。肩書（飾りの形式権力）でなく実権で測るのが核
        /// ＝位人臣を極めた飾りの上に立つ寡頭支配（#164）。
        /// </summary>
        public static bool IsShadowRuler(PowerActor a, IList<PowerActor> others, PowerParams p)
        {
            if (a == null || others == null) return false;

            float myInfluence = EffectiveInfluence(a, p);
            bool higherRankExists = false; // 自分より形式上位の者が居るか
            for (int i = 0; i < others.Count; i++)
            {
                PowerActor o = others[i];
                if (o == null || ReferenceEquals(o, a)) continue;
                if (o.formalRank > a.formalRank) higherRankExists = true;
                if (EffectiveInfluence(o, p) >= myInfluence) return false; // 実権で並ぶ/勝る者が居れば黒幕でない
            }
            return higherRankExists; // 形式では下位なのに実権は最高＝黒幕
        }

        public static bool IsShadowRuler(PowerActor a, IList<PowerActor> others)
            => IsShadowRuler(a, others, PowerParams.Default);
    }
}
