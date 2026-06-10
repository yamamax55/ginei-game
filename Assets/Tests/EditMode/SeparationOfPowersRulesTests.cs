using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>三権分立の純ロジック（#171）の EditMode テスト。決定論。</summary>
    public class SeparationOfPowersRulesTests
    {
        const float Eps = 1e-4f;

        // --- CheckBalance ---

        [Test]
        public void CheckBalance_完全拮抗で1()
        {
            // 三府が均等＝完全均衡
            Assert.AreEqual(1f, SeparationOfPowersRules.CheckBalance(1f, 1f, 1f), Eps);
        }

        [Test]
        public void CheckBalance_一府独占で0()
        {
            // 一府に完全集中＝均衡なし
            Assert.AreEqual(0f, SeparationOfPowersRules.CheckBalance(1f, 0f, 0f), Eps);
        }

        [Test]
        public void CheckBalance_総和0で0_クランプ()
        {
            // 全府0＝測れず0（負値クランプも兼ねる）
            Assert.AreEqual(0f, SeparationOfPowersRules.CheckBalance(0f, 0f, 0f), Eps);
            // 負入力は0扱い（独占と同じ）
            Assert.AreEqual(0f, SeparationOfPowersRules.CheckBalance(-5f, 0f, 0f), Eps);
        }

        [Test]
        public void CheckBalance_スケール不変()
        {
            // 比率が同じなら均衡度は同じ（絶対量に依存しない）
            float a = SeparationOfPowersRules.CheckBalance(2f, 1f, 1f);
            float b = SeparationOfPowersRules.CheckBalance(20f, 10f, 10f);
            Assert.AreEqual(a, b, Eps);
        }

        [Test]
        public void CheckBalance_偏りは均等より低い()
        {
            float even = SeparationOfPowersRules.CheckBalance(1f, 1f, 1f);
            float skewed = SeparationOfPowersRules.CheckBalance(3f, 1f, 1f);
            Assert.Greater(even, skewed);
            // 0..1 にクランプされている
            Assert.GreaterOrEqual(skewed, 0f);
            Assert.LessOrEqual(skewed, 1f);
        }

        // --- TyrannyRisk ---

        [Test]
        public void TyrannyRisk_均等で0()
        {
            Assert.AreEqual(0f, SeparationOfPowersRules.TyrannyRisk(1f, 1f, 1f), Eps);
        }

        [Test]
        public void TyrannyRisk_一府独占で1_独裁不成立()
        {
            // 行政に全集中＝専制リスク最大（均衡は不成立）
            Assert.AreEqual(1f, SeparationOfPowersRules.TyrannyRisk(0f, 1f, 0f), Eps);
        }

        [Test]
        public void TyrannyRisk_総和0で0()
        {
            Assert.AreEqual(0f, SeparationOfPowersRules.TyrannyRisk(0f, 0f, 0f), Eps);
        }

        [Test]
        public void TyrannyRisk_集中が進むほど上昇()
        {
            float mild = SeparationOfPowersRules.TyrannyRisk(2f, 1f, 1f);
            float severe = SeparationOfPowersRules.TyrannyRisk(8f, 1f, 1f);
            Assert.Greater(severe, mild);
            Assert.GreaterOrEqual(mild, 0f);
            Assert.LessOrEqual(severe, 1f);
        }

        [Test]
        public void TyrannyRisk_負入力はクランプ()
        {
            // 負は0扱い＝行政独占と同義でリスク1
            Assert.AreEqual(1f, SeparationOfPowersRules.TyrannyRisk(-3f, 1f, -2f), Eps);
        }

        // --- IsGridlocked ---

        [Test]
        public void IsGridlocked_完全拮抗で停滞()
        {
            // 均衡度1.0 >= 既定しきい0.7 ＝三すくみ停滞
            Assert.IsTrue(SeparationOfPowersRules.IsGridlocked(1f, 1f, 1f));
        }

        [Test]
        public void IsGridlocked_独裁では非停滞()
        {
            // 一極集中＝均衡度低く停滞しない（決められる専制）
            Assert.IsFalse(SeparationOfPowersRules.IsGridlocked(10f, 0f, 0f));
        }

        [Test]
        public void IsGridlocked_しきい値境界()
        {
            // しきい値0に下げれば均衡度0でも >= 成立＝停滞
            var loose = new SeparationParams(0f);
            Assert.IsTrue(SeparationOfPowersRules.IsGridlocked(1f, 0f, 0f, loose));
            // しきい値を均衡度ちょうどに合わせると >= で成立
            float balance = SeparationOfPowersRules.CheckBalance(1f, 1f, 1f);
            var exact = new SeparationParams(balance);
            Assert.IsTrue(SeparationOfPowersRules.IsGridlocked(1f, 1f, 1f, exact));
        }

        [Test]
        public void SeparationParams_クランプとDefault()
        {
            // しきい値は 0..1 にクランプ
            Assert.AreEqual(1f, new SeparationParams(5f).gridlockThreshold, Eps);
            Assert.AreEqual(0f, new SeparationParams(-2f).gridlockThreshold, Eps);
            Assert.AreEqual(0.7f, SeparationParams.Default.gridlockThreshold, Eps);
        }
    }
}
