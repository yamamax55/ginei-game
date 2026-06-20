using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>狂信（信仰的熱狂の戦力化と暴走）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class ZealotryRulesTests
    {
        const float Eps = 0.0001f;
        const float PowEps = 0.001f; // Sqrt/Pow 箇所の緩めの許容

        /// <summary>狂信の強度＝信仰の深さを洗脳と迫害が煽る（amp=1+洗脳×0.5+迫害×0.4）。</summary>
        [Test]
        public void ZealotryIntensity_信仰を洗脳と迫害が煽る()
        {
            // amp = 1 + 0.4*0.5 + 0.5*0.4 = 1.4 ; 0.6*1.4 = 0.84
            Assert.AreEqual(0.84f, ZealotryRules.ZealotryIntensity(0.6f, 0.4f, 0.5f), Eps);
            // 煽り無し＝信仰そのまま
            Assert.AreEqual(0.5f, ZealotryRules.ZealotryIntensity(0.5f, 0f, 0f), Eps);
            // 上限クランプ（1.9→1.0）
            Assert.AreEqual(1f, ZealotryRules.ZealotryIntensity(1f, 1f, 1f), Eps);
            // 迫害が増えると狂信は強まる（弾圧が殉教熱を生む）
            Assert.Greater(ZealotryRules.ZealotryIntensity(0.5f, 0f, 0.8f),
                           ZealotryRules.ZealotryIntensity(0.5f, 0f, 0f), "迫害は狂信を煽る");
        }

        /// <summary>戦意＝狂信の平方根×1.0（既定）＝死を恐れぬ突撃・逓減。</summary>
        [Test]
        public void CombatFervor_狂信に応じた戦意で逓減する()
        {
            // sqrt(0.64) = 0.8
            Assert.AreEqual(0.8f, ZealotryRules.CombatFervor(0.64f), PowEps);
            // sqrt(0.25) = 0.5
            Assert.AreEqual(0.5f, ZealotryRules.CombatFervor(0.25f), PowEps);
            // 単調増加
            Assert.Greater(ZealotryRules.CombatFervor(0.9f), ZealotryRules.CombatFervor(0.4f), "狂信が高いほど戦意も高い");
        }

        /// <summary>殉教志願＝狂信×来世報酬×0.9（既定）＝両方高いとき跳ね上がる。</summary>
        [Test]
        public void MartyrdomWillingness_狂信と来世報酬の積で志願する()
        {
            // 0.8 * 0.5 * 0.9 = 0.36
            Assert.AreEqual(0.36f, ZealotryRules.MartyrdomWillingness(0.8f, 0.5f), Eps);
            // 1 * 1 * 0.9 = 0.9
            Assert.AreEqual(0.9f, ZealotryRules.MartyrdomWillingness(1f, 1f), Eps);
            // 来世報酬ゼロなら志願は出ない
            Assert.AreEqual(0f, ZealotryRules.MartyrdomWillingness(1f, 0f), Eps);
        }

        /// <summary>合理性喪失＝狂信×0.8（既定）＝高いほど理性的選択ができない。</summary>
        [Test]
        public void RationalityLoss_狂信が高いほど合理性を失う()
        {
            Assert.AreEqual(0.4f, ZealotryRules.RationalityLoss(0.5f), Eps);
            Assert.AreEqual(0.8f, ZealotryRules.RationalityLoss(1f), Eps);
            Assert.AreEqual(0f, ZealotryRules.RationalityLoss(0f), Eps);
        }

        /// <summary>自爆威力＝殉教志願×標的の隙×1.0（既定）＝隙が無ければ効かない。</summary>
        [Test]
        public void SuicideAttackPower_志願と標的の隙の積()
        {
            // 0.5 * 0.6 * 1.0 = 0.3
            Assert.AreEqual(0.3f, ZealotryRules.SuicideAttackPower(0.5f, 0.6f), Eps);
            // 標的に隙が無ければ威力ゼロ
            Assert.AreEqual(0f, ZealotryRules.SuicideAttackPower(0.9f, 0f), Eps);
            // 隙が大きいほど一撃が大きい
            Assert.Greater(ZealotryRules.SuicideAttackPower(0.8f, 0.9f),
                           ZealotryRules.SuicideAttackPower(0.8f, 0.2f), "隙が大きいほど威力が大きい");
        }

        /// <summary>狂信の伝染＝狂信×集団結束×0.7（既定）＝結束した集団ほど燃え広がる。</summary>
        [Test]
        public void ZealotryContagion_狂信と結束の積で伝染する()
        {
            // 0.8 * 0.5 * 0.7 = 0.28
            Assert.AreEqual(0.28f, ZealotryRules.ZealotryContagion(0.8f, 0.5f), Eps);
            // 結束ゼロなら伝染しない
            Assert.AreEqual(0f, ZealotryRules.ZealotryContagion(0.9f, 0f), Eps);
            // 結束が固いほど伝染が強い
            Assert.Greater(ZealotryRules.ZealotryContagion(0.8f, 0.9f),
                           ZealotryRules.ZealotryContagion(0.8f, 0.3f), "結束した集団ほど燃え広がる");
        }

        /// <summary>制御不能＝狂信×合理性喪失×0.9（既定）＝理性を失った狂信ほど暴走する。</summary>
        [Test]
        public void Uncontrollability_狂信と判断喪失で暴走する()
        {
            // 0.9 * 0.8 * 0.9 = 0.648
            Assert.AreEqual(0.648f, ZealotryRules.Uncontrollability(0.9f, 0.8f), Eps);
            // 合理性喪失ゼロなら従順（制御可能）
            Assert.AreEqual(0f, ZealotryRules.Uncontrollability(0.9f, 0f), Eps);
        }

        /// <summary>狂信兵の正味戦力価値＝戦意−制御不能（-1..1・両刃）。</summary>
        [Test]
        public void ZealotMilitaryValue_戦意と制御不能の両刃()
        {
            // 0.8 - 0.3 = 0.5（戦意が勝り正味プラス）
            Assert.AreEqual(0.5f, ZealotryRules.ZealotMilitaryValue(0.8f, 0.3f), Eps);
            // 0.3 - 0.7 = -0.4（制御不能が勝りマイナス＝足を引っ張る）
            Assert.AreEqual(-0.4f, ZealotryRules.ZealotMilitaryValue(0.3f, 0.7f), Eps);
        }

        /// <summary>
        /// 物語：迫害された深い信仰の集団が、洗脳で狂信に燃え、結束で伝染し、来世を信じて自爆志願し、
        /// 理性を失う。狂信兵は高い戦意（戦力）を持つが、制御不能がそれを蝕む両刃の刃である。
        /// </summary>
        [Test]
        public void 物語_迫害された狂信集団が燃え上がり両刃の戦力となる()
        {
            // 深い信仰(0.9)・強い洗脳(0.8)・激しい迫害(0.7)
            float z = ZealotryRules.ZealotryIntensity(0.9f, 0.8f, 0.7f);
            Assert.AreEqual(1f, z, Eps, "煽られた狂信は上限に張り付く"); // 0.9*(1+0.4+0.28)=1.512→1

            // 死を恐れぬ戦意は極大
            float fervor = ZealotryRules.CombatFervor(z);
            Assert.AreEqual(1f, fervor, PowEps, "狂信ピークなら戦意も極大"); // sqrt(1)*1=1

            // 結束した集団へ燃え広がる
            float contagion = ZealotryRules.ZealotryContagion(z, 0.9f);
            Assert.Greater(contagion, 0.5f, "結束した集団に伝染する"); // 1*0.9*0.7=0.63

            // 来世を強く信じて自爆志願が跳ね上がる
            float martyr = ZealotryRules.MartyrdomWillingness(z, 0.9f);
            Assert.Greater(martyr, 0.7f, "来世報酬で殉教志願が高い"); // 1*0.9*0.9=0.81

            // しかし理性を失い…
            float loss = ZealotryRules.RationalityLoss(z);
            Assert.AreEqual(0.8f, loss, Eps, "合理的判断を失う"); // 1*0.8

            // 指揮に従わぬ暴走（制御不能）が生じる
            float uncontrol = ZealotryRules.Uncontrollability(z, loss);
            Assert.AreEqual(0.72f, uncontrol, Eps, "制御不能が生じる"); // 1*0.8*0.9

            // 戦意は高いが制御不能がそれを蝕む＝正味は目減りした両刃の戦力
            float value = ZealotryRules.ZealotMilitaryValue(fervor, uncontrol);
            Assert.AreEqual(0.28f, value, Eps, "正味戦力は制御不能ぶん目減りする"); // 1-0.72
            Assert.Less(value, fervor, "制御不能が戦意を蝕む（両刃）");

            // 統制を完全に失えば（独立に高い暴走を渡せば）正味はマイナスへ＝味方の足を引っ張る
            float collapsed = ZealotryRules.ZealotMilitaryValue(0.3f, 0.9f);
            Assert.Less(collapsed, 0f, "暴走しきれば正味マイナスの戦力");
        }
    }
}
