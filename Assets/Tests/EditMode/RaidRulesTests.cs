using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 縦深襲撃を固定する：到達率＝縦深×哨戒すり抜け、破壊量＝戦力−防御（占領なし）、
    /// 帰還率は深さの二乗で落ちる（深く刺すほど抜けなくなる）＋警報で半減、期待戦果の損益分岐、
    /// 警戒は襲撃反復で上昇・休止で減衰（味をしめた反復は逓減）。roll決定論と境界を担保。
    /// </summary>
    public class RaidRulesTests
    {
        private static readonly RaidParams P = RaidParams.Default;
        // 縦深0.5/機動回避0.5/警報後0.5/警戒上昇0.2/減衰0.05/警報閾値0.5

        [Test]
        public void InfiltrationChance_DepthAndPicket()
        {
            Assert.AreEqual(1f, RaidRules.InfiltrationChance(0f, 1f, 0f, P), 1e-5f);     // 浅く哨戒なし＝確実
            Assert.AreEqual(0.5f, RaidRules.InfiltrationChance(1f, 0f, 0f, P), 1e-5f);   // 最深部＝半減
            Assert.AreEqual(0f, RaidRules.InfiltrationChance(0f, 0f, 1f, P), 1e-5f);     // 完全哨戒×鈍足＝通らない
            Assert.AreEqual(0.5f, RaidRules.InfiltrationChance(0f, 1f, 1f, P), 1e-5f);   // 機動で哨戒を半分すり抜け
            Assert.AreEqual(0.46875f, RaidRules.InfiltrationChance(0.5f, 0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void Infiltrates_DeterministicByRoll()
        {
            // 到達率0.46875 を挟んで決定論
            Assert.IsTrue(RaidRules.Infiltrates(0.5f, 0.5f, 0.5f, 0.46f, P));
            Assert.IsFalse(RaidRules.Infiltrates(0.5f, 0.5f, 0.5f, 0.47f, P));
        }

        [Test]
        public void TargetDamage_DestroyWithoutOccupation()
        {
            Assert.AreEqual(70f, RaidRules.TargetDamage(100f, 30f), 1e-5f); // 防御を上回るぶんだけ破壊
            Assert.AreEqual(0f, RaidRules.TargetDamage(30f, 100f), 1e-5f);  // 守り切られる
            Assert.AreEqual(0f, RaidRules.TargetDamage(-10f, -5f), 1e-5f);  // 負入力クランプ
        }

        [Test]
        public void ExfiltrationChance_DeeperIsHarderThanEntry()
        {
            Assert.AreEqual(1f, RaidRules.ExfiltrationChance(0f, 1f, false, P), 1e-5f);
            Assert.AreEqual(0.5625f, RaidRules.ExfiltrationChance(0.5f, 1f, false, P), 1e-5f); // 0.75^2
            Assert.AreEqual(0.25f, RaidRules.ExfiltrationChance(1f, 1f, false, P), 1e-5f);     // 0.5^2
            // 帰路は往路より厳しい＝同じ深さで 帰還率 < 到達率（深く刺すほど抜けなくなる）
            Assert.Less(RaidRules.ExfiltrationChance(0.5f, 1f, false, P),
                RaidRules.InfiltrationChance(0.5f, 1f, 0f, P));
            // 鈍足は帰還率が下限係数0.5倍
            Assert.AreEqual(0.5f, RaidRules.ExfiltrationChance(0f, 0f, false, P), 1e-5f);
        }

        [Test]
        public void ExfiltrationChance_AlertedHalves()
        {
            Assert.AreEqual(0.125f, RaidRules.ExfiltrationChance(1f, 1f, true, P), 1e-5f); // 0.25×0.5
            Assert.AreEqual(0.5f, RaidRules.ExfiltrationChance(0f, 1f, true, P), 1e-5f);   // 浅くても警報で半減
        }

        [Test]
        public void ExpectedValue_BreakEvenByDepthAndAlarm()
        {
            // 戦力100 vs 防御20＝破壊80、部隊価値50、哨戒なし・機動1
            // 浅い襲撃＝堅実な黒字
            Assert.AreEqual(80f, RaidRules.ExpectedValue(0f, 1f, 0f, false, 100f, 20f, 50f, P), 1e-4f);
            // 最深部・警報前＝かろうじて黒字：0.5×80 − (1−0.25)×50 = 2.5
            Assert.AreEqual(2.5f, RaidRules.ExpectedValue(1f, 1f, 0f, false, 100f, 20f, 50f, P), 1e-4f);
            // 最深部・警報後＝赤字に転落：0.5×80 − (1−0.125)×50 = −3.75（深入りの損益分岐）
            Assert.AreEqual(-3.75f, RaidRules.ExpectedValue(1f, 1f, 0f, true, 100f, 20f, 50f, P), 1e-4f);
        }

        [Test]
        public void AlarmLevelTick_RiseOnRaids_DecayWhenQuiet()
        {
            Assert.AreEqual(0.2f, RaidRules.AlarmLevelTick(0f, 1f, 1f, P), 1e-5f);    // 襲撃で警戒上昇
            Assert.AreEqual(0.4f, RaidRules.AlarmLevelTick(0.2f, 1f, 1f, P), 1e-5f);  // 反復でさらに上昇
            Assert.AreEqual(0.45f, RaidRules.AlarmLevelTick(0.5f, 0f, 1f, P), 1e-5f); // 休止で冷める
            Assert.AreEqual(1f, RaidRules.AlarmLevelTick(0.95f, 1f, 1f, P), 1e-5f);   // 上限1
            Assert.AreEqual(0f, RaidRules.AlarmLevelTick(0.02f, 0f, 1f, P), 1e-5f);   // 下限0
            // 反復→警報→以後の襲撃が通らない（帰還率半減）の連鎖
            Assert.IsTrue(RaidRules.IsAlerted(0.5f, P));
            Assert.IsFalse(RaidRules.IsAlerted(0.49f, P));
        }
    }
}
