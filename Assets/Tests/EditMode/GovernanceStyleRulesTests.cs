using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 仁政vs覇道の時間動態（孟子・MENC-3 #1568）を固定する：覇道は武力×強制で即効的に高いが（短期最強）、
    /// 王道は徳×心服で立ち上がりが低い（遅効）。王道は時間で心服を積み・覇道は力の維持費で陰る。統治年数で
    /// 実効統治力の優劣が逆転し（EffectivePower／CrossoverTime）、王道は持続可能で覇道は時間で脆くなる。
    /// </summary>
    public class GovernanceStyleRulesTests
    {
        private static readonly GovernanceStyleParams P = GovernanceStyleParams.Default;
        // 王道蓄積0.1/秒 / 覇道減衰0.2/秒 / 王道齢ゲイン0.5 / 覇道齢ロス0.5 / 脆さ閾値0.4

        [Test]
        public void HegemonicPower_Immediate_Kingly_SlowStart()
        {
            // 覇道：武力×強制が揃えば即座に最大（短期最強）。
            Assert.AreEqual(1f, GovernanceStyleRules.HegemonicPower(force: 1f, coercion: 1f), 1e-4f);
            // 積ゆえ片方が欠けると効かない。
            Assert.AreEqual(0f, GovernanceStyleRules.HegemonicPower(1f, 0f), 1e-4f);

            // 王道：徳はあっても心服の積み重ねが浅いと立ち上がりは低い。
            Assert.AreEqual(0.2f, GovernanceStyleRules.KinglyPower(virtue: 1f, cultivation: 0.2f), 1e-4f);
        }

        [Test]
        public void KinglyAccumulationTick_AccruesWithVirtue()
        {
            // 徳に比例して毎秒だけ心服が積む（遅効だが盤石）。
            float t1 = GovernanceStyleRules.KinglyAccumulationTick(0.2f, virtue: 1f, dt: 1f, P);
            Assert.AreEqual(0.3f, t1, 1e-4f); // 0.2 + 0.1*1*1

            // 徳0なら積まない（不仁は心服を得られない）。
            float none = GovernanceStyleRules.KinglyAccumulationTick(0.2f, virtue: 0f, dt: 1f, P);
            Assert.AreEqual(0.2f, none, 1e-4f);
        }

        [Test]
        public void HegemonicDecayTick_ErodesWithUpkeep()
        {
            // 力の維持費に比例して毎秒だけ削れる（即効だが減衰）。
            float t1 = GovernanceStyleRules.HegemonicDecayTick(1f, forceUpkeep: 1f, dt: 1f, P);
            Assert.AreEqual(0.8f, t1, 1e-4f); // 1 - 0.2*1*1

            // 維持費0なら崩れない。
            float held = GovernanceStyleRules.HegemonicDecayTick(1f, forceUpkeep: 0f, dt: 1f, P);
            Assert.AreEqual(1f, held, 1e-4f);
        }

        [Test]
        public void EffectivePower_Reverses_Over_Governing_Age()
        {
            // 同じ base=0.5 でも、新政（age0）は覇道=王道。
            Assert.AreEqual(0.5f, GovernanceStyleRules.EffectivePower(GovernanceStyle.覇道, 0.5f, 0f, P), 1e-4f);
            Assert.AreEqual(0.5f, GovernanceStyleRules.EffectivePower(GovernanceStyle.王道, 0.5f, 0f, P), 1e-4f);

            // 年数を重ねる（age1）と王道は伸び・覇道は陰る＝時間軸で優劣が逆転。
            float kinglyOld = GovernanceStyleRules.EffectivePower(GovernanceStyle.王道, 0.5f, 1f, P); // 0.5 + 0.5
            float hegemonOld = GovernanceStyleRules.EffectivePower(GovernanceStyle.覇道, 0.5f, 1f, P); // 0.5 - 0.5
            Assert.AreEqual(1f, kinglyOld, 1e-4f);
            Assert.AreEqual(0f, hegemonOld, 1e-4f);
            Assert.Greater(kinglyOld, hegemonOld); // 長期では王道が勝つ
        }

        [Test]
        public void CrossoverTime_Is_The_TimeTradeoff_Branch()
        {
            // 覇道リード1.0 を王道上り0.1＋覇道下り0.2＝相対速度0.3で割る＝追い越し時点。
            float t = GovernanceStyleRules.CrossoverTime(kinglyRate: 0.1f, hegemonicDecay: 0.2f, initialHegemonicLead: 1f);
            Assert.AreEqual(1f / 0.3f, t, 1e-3f);

            // リード0以下なら即逆転（0）。
            Assert.AreEqual(0f, GovernanceStyleRules.CrossoverTime(0.1f, 0.2f, 0f), 1e-4f);

            // 相対速度0なら逆転は来ない（無限大）。
            Assert.IsTrue(float.IsPositiveInfinity(GovernanceStyleRules.CrossoverTime(0f, 0f, 1f)));
        }

        [Test]
        public void Sustainability_Kingly_High_Hegemon_Capped()
        {
            // 王道は徳に比例して高く持続する（心服は崩れない）。
            Assert.AreEqual(0.8f, GovernanceStyleRules.Sustainability(GovernanceStyle.王道, virtue: 0.8f, force: 0f), 1e-4f);
            // 覇道は力に頼るが持続性の天井が半分まで＝本質的に長続きしない。
            Assert.AreEqual(0.5f, GovernanceStyleRules.Sustainability(GovernanceStyle.覇道, virtue: 0f, force: 1f), 1e-4f);
            Assert.Greater(
                GovernanceStyleRules.Sustainability(GovernanceStyle.王道, 1f, 1f),
                GovernanceStyleRules.Sustainability(GovernanceStyle.覇道, 1f, 1f)); // 王道のほうが必ず持続する
        }

        [Test]
        public void HeartsAndMinds_OnlyKingly_WinsHearts()
        {
            // 王道統治力に比例して民の心が積む。
            float won = GovernanceStyleRules.HeartsAndMinds(0.3f, kinglyPower: 1f, dt: 1f, P);
            Assert.AreEqual(0.4f, won, 1e-4f); // 0.3 + 0.1*1*1
            // 覇道は王道統治力0＝心を得られない。
            float none = GovernanceStyleRules.HeartsAndMinds(0.3f, kinglyPower: 0f, dt: 1f, P);
            Assert.AreEqual(0.3f, none, 1e-4f);
        }

        [Test]
        public void IsRegimeBrittle_Hegemon_Brittle_Over_Time_Kingly_Never()
        {
            // 覇道：年数を重ね（age>0）持続可能性が閾値0.4未満なら脆い。
            Assert.IsTrue(GovernanceStyleRules.IsRegimeBrittle(GovernanceStyle.覇道, age: 0.5f, sustainability: 0.3f, P));
            // 新政（age0）では脆くない。
            Assert.IsFalse(GovernanceStyleRules.IsRegimeBrittle(GovernanceStyle.覇道, age: 0f, sustainability: 0.3f, P));
            // 持続可能性が閾値以上なら脆くない。
            Assert.IsFalse(GovernanceStyleRules.IsRegimeBrittle(GovernanceStyle.覇道, age: 0.5f, sustainability: 0.5f, P));
            // 王道は時間で盤石＝決して脆くならない。
            Assert.IsFalse(GovernanceStyleRules.IsRegimeBrittle(GovernanceStyle.王道, age: 1f, sustainability: 0f, P));
        }
    }
}
