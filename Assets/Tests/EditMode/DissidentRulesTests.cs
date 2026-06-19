using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>反体制・異論（発生・地下化・殉教・良心・亡命・体制圧力・弾圧の逆説）の純ロジックを EditMode で担保する。</summary>
    public class DissidentRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        /// <summary>異論発生：不満×言論の自由×知識層で生まれ、どれか欠ければ声にならない。</summary>
        [Test]
        public void DissentEmergence_NeedsGrievanceFreedomAndIntellect()
        {
            // 0.6*0.5*0.7 = 0.21 ; *1.0 = 0.21
            float e = DissidentRules.DissentEmergence(0.6f, 0.5f, 0.7f);
            Assert.AreEqual(0.21f, e, Eps);

            // 言論の自由0＝語れない
            float noFreedom = DissidentRules.DissentEmergence(1f, 0f, 1f);
            Assert.AreEqual(0f, noFreedom, Eps);

            // 知識層0＝語る者がいない
            float noIntellect = DissidentRules.DissentEmergence(1f, 1f, 0f);
            Assert.AreEqual(0f, noIntellect, Eps);
        }

        /// <summary>地下化：異論×弾圧で潜る。弾圧が無ければ潜る必要がない。</summary>
        [Test]
        public void Underground_RisesWithRepression()
        {
            // 0.5*0.8 = 0.4
            float u = DissidentRules.Underground(0.5f, 0.8f);
            Assert.AreEqual(0.4f, u, Eps);

            // 弾圧0＝公然と語れるので潜らない
            float open = DissidentRules.Underground(1f, 0f);
            Assert.AreEqual(0f, open, Eps);
        }

        /// <summary>殉教共感：弾圧×知名度で共感を呼ぶ（弾圧の逆説）。無名なら響かない。</summary>
        [Test]
        public void MartyrSympathy_NeedsVisibility()
        {
            // 0.9*0.7 = 0.63
            float m = DissidentRules.MartyrSympathy(0.9f, 0.7f);
            Assert.AreEqual(0.63f, m, Eps);

            // 無名の異論者を弾圧しても共感は生まれない
            float unknown = DissidentRules.MartyrSympathy(1f, 0f);
            Assert.AreEqual(0f, unknown, Eps);
        }

        /// <summary>知識人の良心：知識層×体制の不正の幾何平均（どちらか0なら覚醒しない）。</summary>
        [Test]
        public void IntellectualConscience_IsGeometricMean()
        {
            // sqrt(0.64*0.81) = sqrt(0.5184) = 0.72
            float c = DissidentRules.IntellectualConscience(0.64f, 0.81f);
            Assert.AreEqual(0.72f, c, PowEps);

            // 不正が無ければ良心は覚めない
            float noInjustice = DissidentRules.IntellectualConscience(1f, 0f);
            Assert.AreEqual(0f, noInjustice, Eps);
        }

        /// <summary>亡命の声：亡命×国際的注目の幾何平均（どちらか0なら届かない）。</summary>
        [Test]
        public void ExileVoice_IsGeometricMean()
        {
            // sqrt(0.5*0.32) = sqrt(0.16) = 0.4
            float v = DissidentRules.ExileVoice(0.5f, 0.32f);
            Assert.AreEqual(0.4f, v, PowEps);

            // 世界が無関心なら亡命者の声は届かない
            float ignored = DissidentRules.ExileVoice(1f, 0f);
            Assert.AreEqual(0f, ignored, Eps);
        }

        /// <summary>体制圧力：殉教共感0.4＋良心0.35＋亡命0.25の重み付き和で漸進的に積み上がる。</summary>
        [Test]
        public void RegimePressure_IsWeightedSum()
        {
            // 0.4*0.4 + 0.6*0.35 + 0.8*0.25 = 0.16+0.21+0.2 = 0.57
            float pr = DissidentRules.RegimePressure(0.4f, 0.6f, 0.8f);
            Assert.AreEqual(0.57f, pr, Eps);

            // 全要素最大なら圧力も最大
            float full = DissidentRules.RegimePressure(1f, 1f, 1f);
            Assert.AreEqual(1f, full, Eps);
        }

        /// <summary>弾圧の正味効果：弾圧−殉教共感。共感が上回れば正味は負（強硬の逆効果）。</summary>
        [Test]
        public void SuppressionEffect_CanBackfire()
        {
            // 強硬すぎて共感が上回る＝0.6-0.8 = -0.2（負＝逆効果）
            float backfire = DissidentRules.SuppressionEffect(0.6f, 0.8f);
            Assert.AreEqual(-0.2f, backfire, Eps);

            // 無名な対象なら共感は小さく弾圧が効く＝0.9-0.2 = 0.7
            float effective = DissidentRules.SuppressionEffect(0.9f, 0.2f);
            Assert.AreEqual(0.7f, effective, Eps);
        }

        /// <summary>封じ込め判定：弾圧の正味効果−体制圧力が閾値以上で抑え込み。</summary>
        [Test]
        public void IsDissentContained_NeedsNetMargin()
        {
            // 0.7-0.3 = 0.4 >= 0.1 → 抑え込めている
            Assert.IsTrue(DissidentRules.IsDissentContained(0.7f, 0.3f));

            // 弾圧が逆効果（-0.2）で圧力0.5＝-0.7 < 0.1 → 抑え込めていない
            Assert.IsFalse(DissidentRules.IsDissentContained(-0.2f, 0.5f));
        }

        /// <summary>
        /// 物語：自由が乏しい体制で不満が募り、有名な異論者を弾圧したら殉教の共感と知識人の良心が覚め、
        /// 亡命者の声と相まって体制圧力が高まり、強硬弾圧が逆効果となって異論を封じ込められなくなる。
        /// </summary>
        [Test]
        public void Story_RepressionBackfiresIntoUncontainableDissent()
        {
            // 強い不満・わずかな言論の自由・厚い知識層で異論が芽吹く
            float emergence = DissidentRules.DissentEmergence(0.9f, 0.4f, 0.8f);
            Assert.Greater(emergence, 0.2f);

            // 強い弾圧で異論は地下へ潜る
            float underground = DissidentRules.Underground(emergence, 0.85f);
            Assert.Greater(underground, 0.2f);

            // 名の知れた異論者を厳しく弾圧＝殉教の共感が大きく広がる
            float martyr = DissidentRules.MartyrSympathy(0.95f, 0.95f);
            Assert.Greater(martyr, 0.8f);

            // 体制の不正に知識人の良心が覚め、亡命者の声も国際的注目を得る
            float conscience = DissidentRules.IntellectualConscience(0.8f, 0.8f);
            float exile = DissidentRules.ExileVoice(0.7f, 0.7f);
            Assert.Greater(conscience, 0.5f);
            Assert.Greater(exile, 0.5f);

            // 共感・良心・亡命の声が積み上がり体制圧力が高まる
            float pressure = DissidentRules.RegimePressure(martyr, conscience, exile);
            Assert.Greater(pressure, 0.5f);

            // 強硬弾圧は殉教共感に上回られ正味効果が負＝逆効果
            float net = DissidentRules.SuppressionEffect(0.85f, martyr);
            Assert.Less(net, 0f);

            // 正味が負で圧力が高い＝異論を封じ込められない
            Assert.IsFalse(DissidentRules.IsDissentContained(net, pressure));
        }
    }
}
