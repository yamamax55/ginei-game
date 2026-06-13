using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 多極均衡・勢力均衡圧力（合従連衡・#1103）純ロジックのテスト。既定 BalanceOfPowerParams
    /// （一強閾値0.4・連衡鋭さ3・バンドワゴン信頼重み0.7・均衡回帰率0.1）で期待値を固定。
    /// 一強での連衡圧力・多極の安定・均衡への回帰を担保する。
    /// </summary>
    public class BalanceOfPowerRulesTests
    {
        private const float Eps = 0.0005f;

        /// <summary>一強の突出度＝最大国力の全体シェア。独占で1、均等多極で低い。</summary>
        [Test]
        public void HegemonThreat_IsTopShareOfTotal()
        {
            // 70/100＝突出した一強。
            Assert.AreEqual(0.7f, BalanceOfPowerRules.HegemonThreat(new[] { 70f, 15f, 15f }), Eps);
            // 均等多極＝突出なし（1/3）。
            Assert.AreEqual(1f / 3f, BalanceOfPowerRules.HegemonThreat(new[] { 50f, 50f, 50f }), Eps);
            // 全ゼロ／空は0。
            Assert.AreEqual(0f, BalanceOfPowerRules.HegemonThreat(new[] { 0f, 0f }), Eps);
            Assert.AreEqual(0f, BalanceOfPowerRules.HegemonThreat(null), Eps);
        }

        /// <summary>最強勢力＝包囲されるべき標的（皆が警戒する者）。</summary>
        [Test]
        public void BalancingTarget_IsStrongestFaction()
        {
            Assert.AreEqual(0, BalanceOfPowerRules.BalancingTarget(new[] { 70f, 15f, 15f }));
            Assert.AreEqual(2, BalanceOfPowerRules.BalancingTarget(new[] { 20f, 30f, 80f }));
            Assert.AreEqual(-1, BalanceOfPowerRules.BalancingTarget(new float[0]));
        }

        /// <summary>一強が突出するほど弱小が結束する＝連衡圧力（バランシング）。閾値以下は0。</summary>
        [Test]
        public void CoalitionPressure_RisesWithHegemonThreat()
        {
            // 突出度0.7・閾値0.4＝超過0.3×鋭さ3＝0.9。
            float strong = BalanceOfPowerRules.CoalitionPressure(new[] { 70f, 15f, 15f }, 0);
            Assert.AreEqual(0.9f, strong, Eps);

            // 突出度0.4＝閾値ちょうど＝超過0＝連衡圧力なし（皆が動かない）。
            float atThreshold = BalanceOfPowerRules.CoalitionPressure(new[] { 40f, 30f, 30f }, 0);
            Assert.AreEqual(0f, atThreshold, Eps);

            // より一強なら圧力も強い＝最強は包囲される。
            float stronger = BalanceOfPowerRules.CoalitionPressure(new[] { 85f, 8f, 7f }, 0);
            Assert.Greater(stronger, strong);
        }

        /// <summary>連衡する相手（弱小）が2勢力未満なら結束は成立しない＝圧力0。</summary>
        [Test]
        public void CoalitionPressure_ZeroWhenNoCoalitionPartners()
        {
            // 一強＋弱小1のみ＝結束相手がいない。
            Assert.AreEqual(0f, BalanceOfPowerRules.CoalitionPressure(new[] { 90f, 10f }, 0), Eps);
            // 独占（他は全ゼロ）も0。
            Assert.AreEqual(0f, BalanceOfPowerRules.CoalitionPressure(new[] { 100f, 0f, 0f }, 0), Eps);
        }

        /// <summary>多極（力が分散）は安定・一強（突出）は不安定＝安定度＝1−突出度。</summary>
        [Test]
        public void SystemStability_HighWhenPowerDispersed()
        {
            // 均等三極＝安定（1−1/3）。
            float multipolar = BalanceOfPowerRules.SystemStability(new[] { 50f, 50f, 50f });
            Assert.AreEqual(1f - 1f / 3f, multipolar, Eps);

            // 突出した一強＝不安定（1−0.7）。
            float hegemonic = BalanceOfPowerRules.SystemStability(new[] { 70f, 15f, 15f });
            Assert.AreEqual(0.3f, hegemonic, Eps);

            // 二極（拮抗）は多極寄りに安定。
            float bipolar = BalanceOfPowerRules.SystemStability(new[] { 50f, 50f });
            Assert.AreEqual(0.5f, bipolar, Eps);
            Assert.Greater(multipolar, hegemonic);
        }

        /// <summary>連衡が頼りないほど勝ち馬に乗る＝バンドワゴン。連衡が固ければ抑える側へ。</summary>
        [Test]
        public void BandwagonTemptation_RisesWhenBalanceUnreliable()
        {
            // 弱小（10対80）で連衡の信頼性ゼロ＝強者に従う誘惑が高い。
            float unreliable = BalanceOfPowerRules.BandwagonTemptation(10f, 80f, 0f);
            // 弱小度=1−10/80=0.875、信頼欠如=(1−0.7)+0.7×1=1.0 → 0.875。
            Assert.AreEqual(0.875f, unreliable, Eps);

            // 連衡が固い（信頼1）＝バンドワゴン抑制。
            float reliable = BalanceOfPowerRules.BandwagonTemptation(10f, 80f, 1f);
            // 信頼欠如=(1−0.7)+0.7×0=0.3 → 0.875×0.3=0.2625。
            Assert.AreEqual(0.2625f, reliable, Eps);
            Assert.Greater(unreliable, reliable);

            // 自勢力が一強と同等以上＝従う理由がない＝0。
            Assert.AreEqual(0f, BalanceOfPowerRules.BandwagonTemptation(80f, 80f, 0f), Eps);
        }

        /// <summary>連衡圧力が一強の国力を弱小へ移し、国力差を縮める＝均衡への回帰（総国力保存）。</summary>
        [Test]
        public void EquilibriumShift_NarrowsPowerGap()
        {
            var before = new[] { 70f, 15f, 15f };
            // 圧力0.9・dt1＝一強から 70×clamp01(0.9×0.1×1)=70×0.09=6.3 を弱小へ国力比で再分配。
            var after = BalanceOfPowerRules.EquilibriumShift(before, 0.9f, 1f);

            Assert.AreEqual(63.7f, after[0], Eps);
            Assert.AreEqual(18.15f, after[1], Eps);
            Assert.AreEqual(18.15f, after[2], Eps);

            // 元配列は非破壊。
            Assert.AreEqual(70f, before[0], Eps);

            // 総国力は保存（再分配＝差を縮めるだけ）。
            float sumBefore = before[0] + before[1] + before[2];
            float sumAfter = after[0] + after[1] + after[2];
            Assert.AreEqual(sumBefore, sumAfter, Eps);

            // 一強は減り弱小は増えた＝差が縮んだ。
            Assert.Less(after[0], before[0]);
            Assert.Greater(after[1], before[1]);
        }

        /// <summary>連衡圧力ゼロ・移す先なし（独占）なら均衡シフトは起きない。</summary>
        [Test]
        public void EquilibriumShift_NoChangeWithoutPressureOrPartners()
        {
            // 圧力0＝動かない。
            var noPressure = BalanceOfPowerRules.EquilibriumShift(new[] { 70f, 30f }, 0f, 1f);
            Assert.AreEqual(70f, noPressure[0], Eps);
            Assert.AreEqual(30f, noPressure[1], Eps);

            // 独占（移す先がない）＝圧力があっても不変。
            var monopoly = BalanceOfPowerRules.EquilibriumShift(new[] { 100f, 0f, 0f }, 0.9f, 1f);
            Assert.AreEqual(100f, monopoly[0], Eps);
        }
    }
}
