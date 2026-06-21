using System;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 均衡回帰テスト（DEV効率化 D）。論理の正しさでなく「ゲームの均衡（面白さの土台）」が
    /// 崩れていないことを不変量で固定する＝バランス破壊を TestHarness/PR で自動検出する。
    /// 純ロジック（AutoBattleSim・FormationMatchupRules）のみ＝Unity 不要で dotnet 実行可。
    /// ここが落ちたら「数式は通るが均衡が壊れた」＝意図したバランス変更なら期待値を更新する。
    /// </summary>
    public class BalanceRegressionTests
    {
        // ───────── 自動会戦：ランチェスター二乗則の不変量 ─────────
        // 勝者は power·strength² の大小で決まる（数の二乗が質に効く）。これを破る式変更を検出する。

        [Test]
        public void AutoBattle_EqualForces_NotBiasedToAttacker()
        {
            // 完全対等＝攻撃側に下駄が無い（膠着の勝者は兵力多い側＝対等なら攻撃側勝利でない）。
            var r = AutoBattleSim.Resolve(100, 100, 1f, 1f, AutoBattleParams.Default);
            Assert.IsFalse(r.attackerWon, "対等な会戦が攻撃側有利に偏ってはならない");
            Assert.Greater(r.durationSeconds, 0.0);
        }

        [Test]
        public void AutoBattle_Mirrored_WinnerFlips()
        {
            // 攻守を入れ替えると勝者も反転する（非対称な隠れバイアスが無い）。
            var ab = AutoBattleSim.Resolve(160, 100, 1.2f, 1f, AutoBattleParams.Default);
            var ba = AutoBattleSim.Resolve(100, 160, 1f, 1.2f, AutoBattleParams.Default);
            Assert.IsTrue(ab.attackerWon, "強い側（攻撃）が勝つ");
            Assert.IsFalse(ba.attackerWon, "入れ替えると同じ側（今度は防御）が勝つ＝攻撃側ではない");
        }

        [Test]
        public void AutoBattle_MoreNumbers_Wins_AtEqualQuality()
        {
            var r = AutoBattleSim.Resolve(200, 100, 1f, 1f, AutoBattleParams.Default);
            Assert.IsTrue(r.attackerWon, "質が同じなら数の多い側が勝つ");
        }

        [Test]
        public void AutoBattle_HigherQuality_Wins_AtEqualNumbers()
        {
            var r = AutoBattleSim.Resolve(100, 100, 2f, 1f, AutoBattleParams.Default);
            Assert.IsTrue(r.attackerWon, "数が同じなら質（提督攻撃力等）の高い側が勝つ");
        }

        [Test]
        public void AutoBattle_SquareLaw_NumbersBeatLinearQuality()
        {
            // ★二乗則の署名：2倍の数は「3倍の質」を覆す（ap·a²=40000 > 3·10000=30000）。
            // もし式が線形（firepower=power×strength）に退化すると、ここで防御が勝ち本テストが落ちる。
            var win = AutoBattleSim.Resolve(200, 100, 1f, 3f, AutoBattleParams.Default);
            Assert.IsTrue(win.attackerWon, "2倍の数は3倍の質に勝つ（二乗則）");
        }

        [Test]
        public void AutoBattle_SquareLaw_QualityMustExceedSquaredNumbers()
        {
            // ★二乗則の閾値：2倍の数を覆すには「4倍超の質」が要る（5·10000=50000 > 40000）。
            var lose = AutoBattleSim.Resolve(200, 100, 1f, 5f, AutoBattleParams.Default);
            Assert.IsFalse(lose.attackerWon, "2倍の数を覆すには4倍超の質が必要（二乗則）");
        }

        [Test]
        public void AutoBattle_Deterministic_And_Bounded()
        {
            var a = AutoBattleSim.Resolve(150, 100, 1f, 1f, AutoBattleParams.Default);
            var b = AutoBattleSim.Resolve(150, 100, 1f, 1f, AutoBattleParams.Default);
            Assert.AreEqual(a.attackerWon, b.attackerWon, "決定論＝同入力は同結果");
            Assert.AreEqual(a.survivorStrength, b.survivorStrength);
            Assert.AreEqual(a.durationSeconds, b.durationSeconds);
            Assert.GreaterOrEqual(a.survivorStrength, 0, "残存は非負");
            Assert.LessOrEqual(a.survivorStrength, 150, "残存が初期兵力を超えない");
            Assert.LessOrEqual(a.durationSeconds, AutoBattleParams.Default.maxDuration, "上限を超えない");
        }

        // ───────── 陣形：三すくみが成立し「支配陣形」が存在しない ─────────

        private static readonly Formation[] RpsTriad =
            { Formation.紡錘陣, Formation.横陣, Formation.鶴翼陣 };

        [Test]
        public void Formation_SelfMatchup_IsNeutral()
        {
            foreach (Formation f in Enum.GetValues(typeof(Formation)))
                Assert.AreEqual(FormationMatchupRules.Neutral, FormationMatchupRules.AttackFactor(f, f), 1e-5f,
                    $"同陣形同士は相性中立であるべき: {f}");
        }

        [Test]
        public void Formation_Matchup_IsReciprocal()
        {
            // a が b に有利なら、b は a に不利（対称な三すくみ）。
            foreach (Formation a in Enum.GetValues(typeof(Formation)))
            foreach (Formation b in Enum.GetValues(typeof(Formation)))
            {
                float ab = FormationMatchupRules.AttackFactor(a, b);
                float ba = FormationMatchupRules.AttackFactor(b, a);
                if (ab > FormationMatchupRules.Neutral)
                    Assert.Less(ba, FormationMatchupRules.Neutral, $"{a}>{b} 有利なら {b}>{a} は不利");
                else if (ab < FormationMatchupRules.Neutral)
                    Assert.Greater(ba, FormationMatchupRules.Neutral, $"{a}>{b} 不利なら {b}>{a} は有利");
                else
                    Assert.AreEqual(FormationMatchupRules.Neutral, ba, 1e-5f, $"{a}-{b} 中立なら逆も中立");
            }
        }

        [Test]
        public void Formation_Triad_FormsCycle()
        {
            // 紡錘→横陣→鶴翼→紡錘 の循環（じゃんけん）が成立する。
            Assert.Greater(FormationMatchupRules.AttackFactor(Formation.紡錘陣, Formation.横陣), 1f, "紡錘は横陣に有利");
            Assert.Greater(FormationMatchupRules.AttackFactor(Formation.横陣, Formation.鶴翼陣), 1f, "横陣は鶴翼に有利");
            Assert.Greater(FormationMatchupRules.AttackFactor(Formation.鶴翼陣, Formation.紡錘陣), 1f, "鶴翼は紡錘に有利");
        }

        [Test]
        public void Formation_NoDominantFormation()
        {
            // ★どの陣形も「他の全陣形に相性有利」ではない＝支配陣形が存在しない。
            var all = (Formation[])Enum.GetValues(typeof(Formation));
            foreach (Formation f in all)
            {
                int advantages = 0;
                int weakAgainst = 0;
                foreach (Formation other in all)
                {
                    if (other.Equals(f)) continue;
                    float k = FormationMatchupRules.AttackFactor(f, other);
                    if (k > FormationMatchupRules.Neutral) advantages++;
                    if (k < FormationMatchupRules.Neutral) weakAgainst++;
                }
                Assert.Less(advantages, all.Length - 1, $"{f} が全陣形に有利＝支配陣形になっている");
                // 三すくみ参加陣形は必ず弱点を1つ持つ（円陣/方陣は相性中立なので weakAgainst=0 を許容）。
                if (Array.IndexOf(RpsTriad, f) >= 0)
                    Assert.GreaterOrEqual(weakAgainst, 1, $"三すくみ陣形 {f} は弱点を持つべき");
            }
        }
    }
}
