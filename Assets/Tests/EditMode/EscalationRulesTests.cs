using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// エスカレーション（戦争の梯子）を固定する：死者で事件が跳ねる、世論×タカ派の積が圧力を増幅、
    /// 高い段・高威信ほど降りコスト増、仲介者で安く降りられる（面子の出口）、圧力と譲歩の綱引き、
    /// 引き返し不能点、そして「誰も望まない戦争に梯子だけで至る」シナリオを担保。
    /// </summary>
    public class EscalationRulesTests
    {
        private static readonly EscalationParams P = EscalationParams.Default;
        // 死者跳ね0.4/固有圧0.5/昇り0.5/降り0.4/不能点0.8/仲介割引0.5

        [Test]
        public void IncidentSeverity_CasualtiesJumpTheRung()
        {
            Assert.AreEqual(0.3f, EscalationRules.IncidentSeverity(0.3f, false, P), 1e-5f); // 無血＝規模どおり
            Assert.AreEqual(0.7f, EscalationRules.IncidentSeverity(0.3f, true, P), 1e-5f);  // 死者＝+0.4
            Assert.AreEqual(1f, EscalationRules.IncidentSeverity(0.8f, true, P), 1e-5f);    // 上限1
        }

        [Test]
        public void EscalationPressure_AngerTimesHawkishness()
        {
            // 怒り×タカ派の積で増幅＝どちらか一方だけでは固有圧のまま
            Assert.AreEqual(0.3f, EscalationRules.EscalationPressure(0.6f, 0f, 0f, P), 1e-5f); // 固有圧0.5のみ
            Assert.AreEqual(0.3f, EscalationRules.EscalationPressure(0.6f, 1f, 0f, P), 1e-5f); // タカ派不在＝増幅なし
            Assert.AreEqual(0.6f, EscalationRules.EscalationPressure(0.6f, 1f, 1f, P), 1e-5f); // 全増幅＝重さどおり
        }

        [Test]
        public void DeescalationCost_HigherRungAndPrestigeHarder()
        {
            Assert.AreEqual(0.6f, EscalationRules.DeescalationCost(0.6f, 1f, P), 1e-5f);  // 高威信＝段の全額
            Assert.AreEqual(0.3f, EscalationRules.DeescalationCost(0.6f, 0f, P), 1e-5f);  // 威信ゼロでも半額は払う
            Assert.AreEqual(0f, EscalationRules.DeescalationCost(0f, 1f, P), 1e-5f);      // 地上にいれば無料
        }

        [Test]
        public void FaceSavingExit_MediatorMakesItCheaper()
        {
            float without = EscalationRules.FaceSavingExit(0.6f, 1f, false, P);
            float with = EscalationRules.FaceSavingExit(0.6f, 1f, true, P);
            Assert.AreEqual(0.6f, without, 1e-5f);            // 仲介なし＝素の威信コスト
            Assert.AreEqual(0.3f, with, 1e-5f);               // 仲介あり＝0.5倍
            Assert.Less(with, without);                        // 出口は確かに安い
        }

        [Test]
        public void RungTick_PressureClimbsConcessionDescends()
        {
            Assert.AreEqual(0.5f, EscalationRules.RungTick(0.2f, 0.6f, 0f, 1f, P), 1e-5f);  // +0.6×0.5
            Assert.AreEqual(0f, EscalationRules.RungTick(0.2f, 0f, 0.5f, 1f, P), 1e-5f);    // −0.5×0.4＝下限0
            Assert.AreEqual(1f, EscalationRules.RungTick(0.9f, 1f, 0f, 1f, P), 1e-5f);      // 上限1
        }

        [Test]
        public void PointOfNoReturn_AtThreshold()
        {
            Assert.IsFalse(EscalationRules.PointOfNoReturn(0.79f, P)); // まだ降りられる
            Assert.IsTrue(EscalationRules.PointOfNoReturn(0.8f, P));   // ここからは宣戦が合理化（DiplomacyRules.DeclareWar へ）
        }

        [Test]
        public void NobodyWantsThisWar_LadderAloneReachesNoReturn()
        {
            // 死者の出た大事件。世論もタカ派もゼロ＝誰も戦争を望んでいない。譲歩もしない。
            float severity = EscalationRules.IncidentSeverity(0.6f, true, P);              // 1.0
            float pressure = EscalationRules.EscalationPressure(severity, 0f, 0f, P);      // 固有圧0.5のみ
            Assert.AreEqual(0.5f, pressure, 1e-5f);

            float rung = 0f;
            for (int i = 0; i < 4; i++) rung = EscalationRules.RungTick(rung, pressure, 0f, 1f, P); // +0.25/tick
            Assert.AreEqual(1f, rung, 1e-5f);
            Assert.IsTrue(EscalationRules.PointOfNoReturn(rung, P)); // 梯子だけで戦争に至った
        }
    }
}
