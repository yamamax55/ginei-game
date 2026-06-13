using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>悪の凡庸性（#1530・ThoughtlessnessRules）の純ロジックテスト。</summary>
    public class ThoughtlessnessRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>道徳的主体性＝階層が深く服従規範が強いほど下がる（既定 erosion0.7）。</summary>
        [Test]
        public void MoralAgency_深い階層と強い服従で主体性が下がる()
        {
            // 浅く弱い服従＝主体性ほぼ満額
            float shallow = ThoughtlessnessRules.MoralAgencyFactor(0f, 0f);
            Assert.AreEqual(1f, shallow, Eps);

            // 深さ1×服従規範1 ＝ 1 - 1×0.7 = 0.3
            float deep = ThoughtlessnessRules.MoralAgencyFactor(1f, 1f);
            Assert.AreEqual(0.3f, deep, Eps);

            // 深いほど主体性は低い
            Assert.Less(deep, shallow);
        }

        /// <summary>責任の拡散＝階層の深さに単調比例（誰も責任を感じなくなる）。</summary>
        [Test]
        public void ResponsibilityDiffusion_階層が深いほど責任が拡散する()
        {
            Assert.AreEqual(0f, ThoughtlessnessRules.ResponsibilityDiffusion(0f), Eps);
            Assert.AreEqual(0.5f, ThoughtlessnessRules.ResponsibilityDiffusion(0.5f), Eps);
            Assert.AreEqual(1f, ThoughtlessnessRules.ResponsibilityDiffusion(1.5f), Eps); // クランプ
        }

        /// <summary>加担リスク＝主体性が低く有害な命令が強いほど高い（既定 scale0.8）。</summary>
        [Test]
        public void AtrocityRisk_思考停止と有害命令で加担が起こる()
        {
            // 主体性倍率0.3・有害命令1 ＝ (1-0.3)×1×0.8 = 0.56
            float risk = ThoughtlessnessRules.AtrocityRisk(0.3f, 1f);
            Assert.AreEqual(0.56f, risk, Eps);

            // 主体性が満額（1）なら加担リスクは0＝思考する者は加担しない
            float none = ThoughtlessnessRules.AtrocityRisk(1f, 1f);
            Assert.AreEqual(0f, none, Eps);

            // 有害な命令がなければ加担も起きない
            float noOrder = ThoughtlessnessRules.AtrocityRisk(0.3f, 0f);
            Assert.AreEqual(0f, noOrder, Eps);
        }

        /// <summary>無思考の深まり＝官僚化・ルーティン化が無思考を上げる（既定 rate0.05）。</summary>
        [Test]
        public void ThoughtlessnessTick_日常化が無思考を深める()
        {
            // 0.5 + 0.05×1×1 = 0.55
            float next = ThoughtlessnessRules.ThoughtlessnessTick(0.5f, 1f, 1f);
            Assert.AreEqual(0.55f, next, Eps);

            // ルーティン化なし＝変化しない
            float still = ThoughtlessnessRules.ThoughtlessnessTick(0.5f, 0f, 1f);
            Assert.AreEqual(0.5f, still, Eps);

            // 上限1でクランプ
            float capped = ThoughtlessnessRules.ThoughtlessnessTick(0.99f, 1f, 10f);
            Assert.AreEqual(1f, capped, Eps);
        }

        /// <summary>官僚的距離＝階層の深さと抽象化の積（被害者が机上の数字になる）。</summary>
        [Test]
        public void BureaucraticDistance_抽象化が被害者を見えなくする()
        {
            // 深さ1×抽象化1 = 1
            Assert.AreEqual(1f, ThoughtlessnessRules.BureaucraticDistance(1f, 1f), Eps);
            // 深さ0.5×抽象化0.5 = 0.25
            Assert.AreEqual(0.25f, ThoughtlessnessRules.BureaucraticDistance(0.5f, 0.5f), Eps);
            // 抽象化0なら距離0＝顔が見えれば加担しにくい
            Assert.AreEqual(0f, ThoughtlessnessRules.BureaucraticDistance(1f, 0f), Eps);
        }

        /// <summary>良心の覚醒＝立ち止まって考える瞬間が主体性を呼び戻す（既定 gain0.4）。</summary>
        [Test]
        public void ConscienceActivation_思考が悪を止める()
        {
            // 0.2 + 1×0.4 = 0.6
            float woken = ThoughtlessnessRules.ConscienceActivation(0.2f, 1f);
            Assert.AreEqual(0.6f, woken, Eps);

            // 反省の瞬間がなければ主体性は変わらない
            float same = ThoughtlessnessRules.ConscienceActivation(0.2f, 0f);
            Assert.AreEqual(0.2f, same, Eps);

            // 覚醒後は元より高い
            Assert.Greater(woken, same);
        }

        /// <summary>服従と良心の綱引き＝conscience−obedience（どちらが勝つか）。</summary>
        [Test]
        public void ObedienceVsConscience_良心と服従の勝敗()
        {
            // 良心が勝つ＝正
            Assert.Greater(ThoughtlessnessRules.ObedienceVsConscience(0.3f, 0.8f), 0f);
            // 服従が勝つ＝負
            Assert.Less(ThoughtlessnessRules.ObedienceVsConscience(0.8f, 0.3f), 0f);
            // 拮抗＝0
            Assert.AreEqual(0f, ThoughtlessnessRules.ObedienceVsConscience(0.5f, 0.5f), Eps);
        }

        /// <summary>悪の凡庸性判定＝主体性が低く加担リスクが高いとき陥る。</summary>
        [Test]
        public void IsBanalEvil_無思考の加担に陥った状態を判定()
        {
            // 主体性倍率0.3 < (1-0.5=0.5) かつ 加担リスク0.56 >= 0.5 ＝ 凡庸な悪
            Assert.IsTrue(ThoughtlessnessRules.IsBanalEvil(0.3f, 0.56f, 0.5f));

            // 主体性が高い（0.9 >= 0.5）＝思考する者は陥らない
            Assert.IsFalse(ThoughtlessnessRules.IsBanalEvil(0.9f, 0.56f, 0.5f));

            // 加担リスクが低い（0.1 < 0.5）＝有害命令がなければ陥らない
            Assert.IsFalse(ThoughtlessnessRules.IsBanalEvil(0.3f, 0.1f, 0.5f));
        }

        /// <summary>BanalityState のコンストラクタは全フィールドを 0..1 にクランプする。</summary>
        [Test]
        public void BanalityState_全フィールドがクランプされる()
        {
            var s = new BanalityState(1.5f, -0.2f, 0.6f);
            Assert.AreEqual(1f, s.thoughtlessness, Eps);
            Assert.AreEqual(0f, s.obedience, Eps);
            Assert.AreEqual(0.6f, s.moralAgency, Eps);
        }
    }
}
