using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>敗走兵の再結集：確率/所要時間/回復量/士気/追撃妨害/集結点/復帰遅れ/可否（既定Params）。</summary>
    public class RallyRulesTests
    {
        [Test]
        public void RallyChance_LeadershipAndDistance()
        {
            // 統率重み0.6・距離重み0.4・基準距離50
            Assert.AreEqual(1f, RallyRules.RallyChance(100f, 50f), 1e-4f);   // 統率満点＋十分遠い＝確実
            Assert.AreEqual(0.5f, RallyRules.RallyChance(50f, 25f), 1e-4f);  // 0.6*0.5 + 0.4*0.5
            Assert.AreEqual(0f, RallyRules.RallyChance(0f, 0f), 1e-4f);      // 統率皆無＋敵の直近＝ゼロ
            Assert.AreEqual(0.6f, RallyRules.RallyChance(100f, 0f), 1e-4f);  // 距離0でも統率ぶんは残る
        }

        [Test]
        public void RallyTime_DisorderAndLeadership()
        {
            Assert.AreEqual(30f, RallyRules.RallyTime(0f, 50f), 1e-4f);    // 混乱なし＝最短
            Assert.AreEqual(180f, RallyRules.RallyTime(1f, 0f), 1e-4f);    // 混乱極大＋統率皆無＝最長
            Assert.AreEqual(30f, RallyRules.RallyTime(1f, 100f), 1e-4f);   // 名将なら混乱極大でも最短で立て直す
            Assert.AreEqual(67.5f, RallyRules.RallyTime(0.5f, 50f), 1e-4f); // 30 + 150*0.5*0.5
        }

        [Test]
        public void RecoveredStrength_FractionOfScattered()
        {
            Assert.AreEqual(600f, RallyRules.RecoveredStrength(1000f, 0.6f), 1e-4f);
            Assert.AreEqual(0f, RallyRules.RecoveredStrength(1000f, 0f), 1e-4f);    // 一兵も戻らない
            Assert.AreEqual(0f, RallyRules.RecoveredStrength(-50f, 1f), 1e-4f);     // 負兵力はガード
        }

        [Test]
        public void MoraleRestore_NeedsProgressAndLeader()
        {
            Assert.AreEqual(0.72f, RallyRules.MoraleRestore(0.8f, 0.9f), 1e-4f);
            Assert.AreEqual(0f, RallyRules.MoraleRestore(1f, 0f), 1e-4f);   // 指揮官不在＝士気戻らず
            Assert.AreEqual(0f, RallyRules.MoraleRestore(0f, 1f), 1e-4f);   // 未集結＝士気戻らず
        }

        [Test]
        public void PursuitDisruption_ScalesWithProximity()
        {
            Assert.AreEqual(0.7f, RallyRules.PursuitDisruption(0.7f), 1e-4f);
            Assert.AreEqual(0f, RallyRules.PursuitDisruption(0f), 1e-4f);   // 追撃なし＝妨害なし
            Assert.AreEqual(1f, RallyRules.PursuitDisruption(1f), 1e-4f);   // 背後に張りつき＝妨害最大
        }

        [Test]
        public void SafeRallyPoint_ControlAndDistance()
        {
            // 支配重み0.5・基準距離50
            Assert.AreEqual(1f, RallyRules.SafeRallyPoint(50f, 1f), 1e-4f);    // 自勢力下＋遠い＝完全に安全
            Assert.AreEqual(0f, RallyRules.SafeRallyPoint(0f, 0f), 1e-4f);     // 敵直近＋無支配＝危険
            Assert.AreEqual(0.5f, RallyRules.SafeRallyPoint(25f, 0.5f), 1e-4f); // 0.5*0.5 + 0.5*0.5
        }

        [Test]
        public void ReintegrationDelay_GrowsWithStrength()
        {
            Assert.AreEqual(5f, RallyRules.ReintegrationDelay(0f), 1e-4f);    // 固定の段取り
            Assert.AreEqual(17f, RallyRules.ReintegrationDelay(600f), 1e-4f); // 5 + 0.02*600
        }

        // 物語テスト：統率ある指揮官が安全な後方で敗走兵を再結集して戦線へ復帰させる。だが追撃下では立て直せない。
        [Test]
        public void Narrative_LeaderRalliesInSafeRearButNotUnderPursuit()
        {
            // 有能な指揮官（統率80）が、味方支配下で敵から十分離れた後方へ下がった
            float chance = RallyRules.RallyChance(80f, 50f);          // 0.6*0.8 + 0.4*1.0 = 0.88
            Assert.AreEqual(0.88f, chance, 1e-4f);

            float safe = RallyRules.SafeRallyPoint(50f, 1f);          // 完全に安全な集結点
            Assert.AreEqual(1f, safe, 1e-4f);

            // 追撃が薄い（近さ0.1）＝立て直せる
            float lightPursuit = RallyRules.PursuitDisruption(0.1f);
            Assert.IsTrue(RallyRules.CanRally(chance, lightPursuit)); // 0.88*0.9=0.792 ≥ 0.5

            // 散兵の大半を再結集し、士気も戻り、ほどなく戦線へ復帰する
            float recovered = RallyRules.RecoveredStrength(1000f, chance); // 880
            Assert.AreEqual(880f, recovered, 1e-4f);
            Assert.Greater(RallyRules.MoraleRestore(0.9f, 1f), 0.5f);      // 指揮官臨在で士気回復
            Assert.AreEqual(22.6f, RallyRules.ReintegrationDelay(recovered), 1e-4f); // 5 + 0.02*880

            // しかし敵が背後に張りついて追撃してくると、同じ指揮官でも立て直せない
            float heavyPursuit = RallyRules.PursuitDisruption(0.9f);
            Assert.IsFalse(RallyRules.CanRally(chance, heavyPursuit));     // 0.88*0.1=0.088 < 0.5
        }
    }
}
