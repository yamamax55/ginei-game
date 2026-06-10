using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>AutoBattleSim（TIME-4 #950）の純ロジックテスト。代表＋境界＋クランプ＋不変条件。</summary>
    public class AutoBattleSimTests
    {
        // 決定論のため固定刻み・短上限のパラメータ。
        private static AutoBattleParams Std => new AutoBattleParams(0.01f, 600f, 1f);

        // ===== 代表 =====

        [Test]
        public void Resolve_StrongerAttacker_Wins()
        {
            var r = AutoBattleSim.Resolve(200, 100, 1f, 1f, Std);
            Assert.IsTrue(r.attackerWon, "兵力2倍の攻撃側が勝つべき");
            Assert.Greater(r.survivorStrength, 0, "勝者は残存>0");
        }

        [Test]
        public void Resolve_StrongerDefender_Wins()
        {
            var r = AutoBattleSim.Resolve(100, 200, 1f, 1f, Std);
            Assert.IsFalse(r.attackerWon, "兵力2倍の防衛側が勝つべき");
            Assert.Greater(r.survivorStrength, 0);
        }

        // ===== 不変条件：勝者残存>0／所要時間>0 =====

        [Test]
        public void Resolve_Duration_AlwaysPositive()
        {
            var r = AutoBattleSim.Resolve(150, 100, 1f, 1f, Std);
            Assert.Greater(r.durationSeconds, 0.0, "所要時間は常に>0");
        }

        // ===== 不変条件：相打ち（同戦力同power）は接戦で長時間 =====

        [Test]
        public void Resolve_SymmetricBattle_IsCloseAndLong()
        {
            var quick = AutoBattleSim.Resolve(200, 100, 1f, 1f, Std);
            var even = AutoBattleSim.Resolve(150, 150, 1f, 1f, Std);
            // 同戦力は決着がつきにくく、非対称戦より長くかかる。
            Assert.GreaterOrEqual(even.durationSeconds, quick.durationSeconds,
                "対称戦は非対称戦より長時間の接戦になる");
        }

        [Test]
        public void Resolve_EqualStrength_DefenderHoldsOrCloseSurvivors()
        {
            var r = AutoBattleSim.Resolve(150, 150, 1f, 1f, Std);
            // 同数同powerは防衛側勝利（守り切る）＝attackerWon=false、残存は僅少。
            Assert.IsFalse(r.attackerWon, "同戦力同powerは防衛側が守り切る");
        }

        // ===== 不変条件：power↑で単調（所要時間↓ or 残存↑） =====

        [Test]
        public void Resolve_HigherAttackerPower_FasterOrMoreSurvivors()
        {
            var low = AutoBattleSim.Resolve(150, 150, 1f, 1f, Std);
            var high = AutoBattleSim.Resolve(150, 150, 2f, 1f, Std);
            Assert.IsTrue(high.attackerWon, "攻撃power2倍なら攻撃側が勝つ");
            // power↑で所要時間↓ または 残存↑ の単調性（少なくとも結果が攻撃側有利へ動く）。
            bool faster = high.durationSeconds <= low.durationSeconds;
            bool moreSurvivors = high.survivorStrength >= 1;
            Assert.IsTrue(faster || moreSurvivors, "power↑で所要時間↓ or 残存↑");
        }

        // ===== クランプ：兵力0・負 =====

        [Test]
        public void Resolve_ZeroAttacker_DefenderWinsImmediately()
        {
            var r = AutoBattleSim.Resolve(0, 100, 1f, 1f, Std);
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(100, r.survivorStrength, "全滅相手なら防衛側は無傷で残る");
            Assert.Greater(r.durationSeconds, 0.0);
        }

        [Test]
        public void Resolve_NegativeStrength_ClampedToZero()
        {
            var r = AutoBattleSim.Resolve(-50, 100, 1f, 1f, Std);
            // 負の攻撃兵力は0扱い＝防衛側無傷勝利（敵を強化しない）。
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(100, r.survivorStrength);
        }

        [Test]
        public void Resolve_BothZero_NoSurvivors()
        {
            var r = AutoBattleSim.Resolve(0, 0, 1f, 1f, Std);
            Assert.AreEqual(0, r.survivorStrength, "両者全滅は残存0");
            Assert.Greater(r.durationSeconds, 0.0);
        }

        [Test]
        public void Resolve_NegativePower_ClampedNoHealing()
        {
            // 負powerが回復＝兵力増にならないこと（クランプで0扱い）。
            var r = AutoBattleSim.Resolve(150, 150, -5f, -5f, Std);
            // 両power0＝損耗ゼロ＝maxDuration まで膠着→兵力多い側（同数なら防衛）勝ち。残存は初期値を超えない。
            Assert.LessOrEqual(r.survivorStrength, 150, "負powerでも兵力は増えない");
        }

        // ===== 境界：dtStep<=0 のガード =====

        [Test]
        public void Resolve_NonPositiveDtStep_DoesNotHang()
        {
            var bad = new AutoBattleParams(0.01f, 100f, 0f);
            var r = AutoBattleSim.Resolve(200, 100, 1f, 1f, bad);
            // dtStep<=0 は既定刻みへフォールバックし無限ループしない＝結果が返る。
            Assert.IsTrue(r.attackerWon);
            Assert.Greater(r.durationSeconds, 0.0);
        }

        // ===== 境界：maxDuration 打ち切り（膠着＝兵力多い側勝ち） =====

        [Test]
        public void Resolve_MaxDuration_CutsOffStalemate()
        {
            // 損耗ほぼ0＋短い上限＝決着前に打ち切り。攻撃側兵力多い＝攻撃側勝ち。
            var prm = new AutoBattleParams(0.00001f, 5f, 1f);
            var r = AutoBattleSim.Resolve(200, 100, 1f, 1f, prm);
            Assert.LessOrEqual(r.durationSeconds, 5.0, "maxDuration で打ち切られる");
            Assert.IsTrue(r.attackerWon, "膠着打ち切りは兵力多い側勝ち");
        }

        // ===== 不変条件：Lanchester 二乗則の近似一致 =====
        // 二乗則の保存量：attackerPower×A² − defenderPower×D² ≈ 一定。
        // 同power・攻撃側優勢なら 勝者残存² ≈ A0² − D0²。

        [Test]
        public void Resolve_LanchesterSquareLaw_ApproxHolds()
        {
            int a0 = 500, d0 = 300;
            // 二乗則精度を上げるため細かい刻み。
            var prm = new AutoBattleParams(0.005f, 100000f, 0.05f);
            var r = AutoBattleSim.Resolve(a0, d0, 1f, 1f, prm);
            Assert.IsTrue(r.attackerWon);

            double predictedSq = (double)a0 * a0 - (double)d0 * d0; // 同power
            double predicted = System.Math.Sqrt(predictedSq);        // ≈400
            // 積分誤差・切り上げを許容（±5%程度）。
            double tolerance = predicted * 0.05 + 2.0;
            Assert.AreEqual(predicted, r.survivorStrength, tolerance,
                "勝者残存 ≈ √(A²−D²)（Lanchester 二乗則）");
        }

        // ===== 既定オーバーロードの整合 =====

        [Test]
        public void Resolve_DefaultOverload_MatchesExplicit()
        {
            var a = AutoBattleSim.Resolve(180, 120);
            var b = AutoBattleSim.Resolve(180, 120, 1f, 1f, AutoBattleParams.Default);
            Assert.AreEqual(b.attackerWon, a.attackerWon);
            Assert.AreEqual(b.survivorStrength, a.survivorStrength);
            Assert.AreEqual(b.durationSeconds, a.durationSeconds, 1e-9);
        }

        [Test]
        public void Default_Params_HaveExpectedValues()
        {
            var d = AutoBattleParams.Default;
            Assert.AreEqual(AutoBattleParams.DefaultAttritionRate, d.attritionRate);
            Assert.AreEqual(600f, d.maxDuration, "maxDuration 既定は600秒");
            Assert.AreEqual(1f, d.dtStep, "dtStep 既定は1秒");
        }
    }
}
