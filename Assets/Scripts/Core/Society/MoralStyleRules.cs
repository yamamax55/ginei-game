using UnityEngine;

namespace Ginei
{
    /// <summary>スミス『道徳感情論』の三つの徳。慎慮（自己の利益への配慮＝堅実）・仁愛（他者への積極的善行＝寛大）・正義（他者を害さない消極的徳＝法の支配の土台）。</summary>
    public enum MoralVirtue { 慎慮, 仁愛, 正義 }

    /// <summary>3徳統治スタイルの調整係数（アダム・スミス『道徳感情論』の三徳・TMS-3 #1586）。</summary>
    public readonly struct MoralStyleParams
    {
        /// <summary>慎慮（堅実な財政運営）が長期安定へ寄与する重み。</summary>
        public readonly float prudenceWeight;
        /// <summary>仁愛（福祉・寛大）が民の支持・希望へ寄与する重み。</summary>
        public readonly float benevolenceWeight;
        /// <summary>正義（法の支配＝土台）が安定の土台へ寄与する重み（最大＝大黒柱）。</summary>
        public readonly float justiceWeight;
        /// <summary>正義が欠けると他の徳が活きない度合い（0=ゲートなし、1=完全に正義の従属）。</summary>
        public readonly float justiceGateStrength;
        /// <summary>これを超えた徳は過剰＝弊害となる閾値（仁愛過剰＝財政破綻、慎慮過剰＝吝嗇）。</summary>
        public readonly float excessThreshold;

        public MoralStyleParams(float prudenceWeight, float benevolenceWeight, float justiceWeight,
            float justiceGateStrength, float excessThreshold)
        {
            this.prudenceWeight = Mathf.Max(0f, prudenceWeight);
            this.benevolenceWeight = Mathf.Max(0f, benevolenceWeight);
            this.justiceWeight = Mathf.Max(0f, justiceWeight);
            this.justiceGateStrength = Mathf.Clamp01(justiceGateStrength);
            this.excessThreshold = Mathf.Clamp01(excessThreshold);
        }

        /// <summary>既定＝慎慮0.25・仁愛0.25・正義0.5（正義が土台で最大の重み）・正義ゲート0.7・過剰閾値0.85。</summary>
        public static MoralStyleParams Default => new MoralStyleParams(0.25f, 0.25f, 0.5f, 0.7f, 0.85f);
    }

    /// <summary>
    /// 3徳統治スタイル＝アダム・スミス『道徳感情論』の三つの徳を統治スタイルの軸に据える（TMS-3 #1586）。
    /// <b>慎慮(prudence)</b>＝自己の利益への配慮＝堅実な財政運営／<b>仁愛(benevolence)</b>＝他者への積極的
    /// 善行＝福祉と寛大／<b>正義(justice)</b>＝他者を害さない消極的徳＝法の支配の土台。各徳の強さが安定度修正子
    /// を生み、三徳のバランスが統治の質を決める。スミスはとりわけ<b>正義を「社会を支える大黒柱」</b>とした
    /// ＝慎慮や仁愛は飾りだが正義は土台であり、<b>正義が欠けると建物（統治）全体が崩れる</b>
    /// （<see cref="JusticeAsFoundation"/> の乗算ゲート）。徳も過剰は弊害（仁愛過剰＝財政破綻、慎慮過剰＝吝嗇＝
    /// <see cref="ExcessOfVirtue"/>）。<b>GovernanceRules</b>（安定度の収束計算）/<b>WangDaoRules</b>（王道覇道の
    /// 主義ドリフト）とは別系統＝こちらは<b>スミス三徳による統治スタイルと安定度修正子</b>を解く。
    /// <b>MoralSproutsRules</b>（孟子の四端＝別 EPIC）とも別＝出典が西洋（スミス）の三徳。
    /// 全入力クランプ・乱数なし・決定論・基準値非破壊（修正子を返す）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MoralStyleRules
    {
        /// <summary>
        /// 慎慮の効果（修正子 0..1）。堅実な財政運営が財政・長期安定へ寄与する＝慎慮(0..1)×慎慮重み。
        /// </summary>
        public static float PrudenceEffect(float prudence, MoralStyleParams p)
            => Mathf.Clamp01(Mathf.Clamp01(prudence) * p.prudenceWeight);

        public static float PrudenceEffect(float prudence) => PrudenceEffect(prudence, MoralStyleParams.Default);

        /// <summary>
        /// 仁愛の効果（修正子 0..1）。福祉・寛大が民の支持・希望へ寄与する＝仁愛(0..1)×仁愛重み。
        /// </summary>
        public static float BenevolenceEffect(float benevolence, MoralStyleParams p)
            => Mathf.Clamp01(Mathf.Clamp01(benevolence) * p.benevolenceWeight);

        public static float BenevolenceEffect(float benevolence) => BenevolenceEffect(benevolence, MoralStyleParams.Default);

        /// <summary>
        /// 正義の効果（修正子 0..1）。他者を害さない＝法の支配が安定の土台へ寄与する＝正義(0..1)×正義重み。
        /// スミスにとって正義は社会を支える大黒柱＝重みが最大（既定で他の倍）。
        /// </summary>
        public static float JusticeEffect(float justice, MoralStyleParams p)
            => Mathf.Clamp01(Mathf.Clamp01(justice) * p.justiceWeight);

        public static float JusticeEffect(float justice) => JusticeEffect(justice, MoralStyleParams.Default);

        /// <summary>
        /// 三徳を統合した安定度修正子（0..1）。各徳の効果の和を、<b>正義の土台ゲート</b>で乗算する
        /// ＝<see cref="JusticeAsFoundation"/> を通すため正義が欠ければ全体が崩れる（土台の重みが大きい）。
        /// 慎慮・仁愛がいかに高くても、正義（法の支配）なき統治は安定を生まない。
        /// </summary>
        public static float StabilityModifier(float prudence, float benevolence, float justice, MoralStyleParams p)
        {
            float pr = PrudenceEffect(prudence, p);
            float be = BenevolenceEffect(benevolence, p);
            float ju = JusticeEffect(justice, p);
            float sum = Mathf.Clamp01(pr + be + ju); // 三徳の効果の総和（土台込み）
            // 正義の大黒柱が抜けると全体が崩れる＝慎慮+仁愛の寄与を正義ゲートで乗算する。
            float otherVirtues = Mathf.Clamp01(pr + be);
            float gated = JusticeAsFoundation(justice, otherVirtues, p);
            // 正義由来分はゲートに従属せず（土台そのもの）、他徳分はゲートを通した値を採る。
            return Mathf.Clamp01(ju + gated);
        }

        public static float StabilityModifier(float prudence, float benevolence, float justice)
            => StabilityModifier(prudence, benevolence, justice, MoralStyleParams.Default);

        /// <summary>
        /// 三徳のバランス（0..1）。偏りが少ないほど高い＝徳の調和。三徳の平均からの平均絶対偏差を
        /// 取り、1から引く（全て等しい＝1.0、極端に偏る＝低い）。スミスの説く三徳の均衡。
        /// </summary>
        public static float VirtueBalance(float prudence, float benevolence, float justice)
        {
            float pr = Mathf.Clamp01(prudence);
            float be = Mathf.Clamp01(benevolence);
            float ju = Mathf.Clamp01(justice);
            float mean = (pr + be + ju) / 3f;
            float dev = (Mathf.Abs(pr - mean) + Mathf.Abs(be - mean) + Mathf.Abs(ju - mean)) / 3f;
            // 平均絶対偏差の理論上限は 4/9（一つだけ1で他0：平均1/3、偏差和 2/3+1/3+1/3=4/3 を3で割る）。これで正規化して 0..1 に。
            float spread = Mathf.Clamp01(dev / (4f / 9f));
            return Mathf.Clamp01(1f - spread);
        }

        /// <summary>
        /// 正義の土台ゲート（0..1）。正義が欠けると他の徳も活きない＝<b>柱が抜けると建物が崩れる</b>。
        /// 他の徳(otherVirtues 0..1)を、正義(0..1)で決まるゲート係数で乗算する。ゲート係数＝
        /// (1−gateStrength)＋gateStrength×justice＝正義が1なら全開・0ならgateStrength分だけ閉じる。
        /// gateStrength が大きいほど「正義なしには他徳が無力」になる（スミスの大黒柱）。
        /// </summary>
        public static float JusticeAsFoundation(float justice, float otherVirtues, MoralStyleParams p)
        {
            float ju = Mathf.Clamp01(justice);
            float gate = (1f - p.justiceGateStrength) + p.justiceGateStrength * ju;
            return Mathf.Clamp01(Mathf.Clamp01(otherVirtues) * gate);
        }

        public static float JusticeAsFoundation(float justice, float otherVirtues)
            => JusticeAsFoundation(justice, otherVirtues, MoralStyleParams.Default);

        /// <summary>
        /// 最も強い徳（統治スタイルの色）。三徳の値を比べ最大の徳を返す。同値は慎慮→仁愛→正義の順に優先
        /// （安定した既定スタイル）。すべて0なら正義（土台）を返す。
        /// </summary>
        public static MoralVirtue DominantVirtue(float prudence, float benevolence, float justice)
        {
            float pr = Mathf.Clamp01(prudence);
            float be = Mathf.Clamp01(benevolence);
            float ju = Mathf.Clamp01(justice);
            // すべて0＝徳が無い＝土台の正義に倒す（スミスの大黒柱）。
            if (pr <= 0f && be <= 0f && ju <= 0f) return MoralVirtue.正義;
            if (pr >= be && pr >= ju) return MoralVirtue.慎慮;
            if (be >= ju) return MoralVirtue.仁愛;
            return MoralVirtue.正義;
        }

        /// <summary>
        /// 徳の過剰による弊害（0..1）。徳も過剰は弊害＝仁愛過剰＝財政破綻、慎慮過剰＝吝嗇。閾値(threshold)を
        /// 超えた分を 0..1 に正規化して返す（閾値以下＝0＝健全、閾値を超えるほど弊害が増す）。中庸の徳を是とする。
        /// </summary>
        public static float ExcessOfVirtue(float virtueLevel, float threshold)
        {
            float v = Mathf.Clamp01(virtueLevel);
            float th = Mathf.Clamp01(threshold);
            if (v <= th) return 0f;
            float room = 1f - th;
            if (room <= 0f) return 0f;
            return Mathf.Clamp01((v - th) / room);
        }

        public static float ExcessOfVirtue(float virtueLevel)
            => ExcessOfVirtue(virtueLevel, MoralStyleParams.Default.excessThreshold);
    }
}
