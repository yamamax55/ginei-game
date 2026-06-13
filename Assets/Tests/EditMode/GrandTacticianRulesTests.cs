using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>大戦術家（ハンニバル）：包囲殲滅/戦場の霧/心理戦/地形無効/宿敵特効/多国籍結束/戦略的弱点/ザマ。</summary>
    public class GrandTacticianRulesTests
    {
        [Test]
        public void Envelopment_CannaeButCounteredAtZama()
        {
            Assert.AreEqual(1.4f, GrandTacticianRules.EnvelopmentMasteryFactor(true, false), 1e-4f);  // カンナエ
            Assert.AreEqual(1.0f, GrandTacticianRules.EnvelopmentMasteryFactor(true, true), 1e-4f);   // 研究され破られる（ザマ）
            Assert.AreEqual(1.0f, GrandTacticianRules.EnvelopmentMasteryFactor(false, false), 1e-4f); // 並
        }

        [Test]
        public void Ambush_Terrain_Nemesis()
        {
            Assert.AreEqual(1.3f, GrandTacticianRules.AmbushMasteryFactor(true), 1e-4f);
            Assert.AreEqual(1.0f, GrandTacticianRules.AmbushMasteryFactor(false), 1e-4f);

            Assert.IsTrue(GrandTacticianRules.IgnoresTerrainPenalty(true));   // アルプス越え・戦象
            Assert.IsFalse(GrandTacticianRules.IgnoresTerrainPenalty(false));

            Assert.AreEqual(1.25f, GrandTacticianRules.NemesisFactionBonus(true, true), 1e-4f); // ローマ特効
            Assert.AreEqual(1.0f, GrandTacticianRules.NemesisFactionBonus(true, false), 1e-4f);
            Assert.AreEqual(1.0f, GrandTacticianRules.NemesisFactionBonus(false, true), 1e-4f);
        }

        [Test]
        public void Provocation_ByEnemyPersonality()
        {
            Assert.AreEqual(0.4f, GrandTacticianRules.ProvocationFactor(true, CommanderPersonality.激情), 1e-4f); // 短気
            Assert.AreEqual(0.3f, GrandTacticianRules.ProvocationFactor(true, CommanderPersonality.果敢), 1e-4f);
            Assert.AreEqual(0.1f, GrandTacticianRules.ProvocationFactor(true, CommanderPersonality.冷静), 1e-4f); // 乗らない
            Assert.AreEqual(0f, GrandTacticianRules.ProvocationFactor(false, CommanderPersonality.激情), 1e-4f);
        }

        [Test]
        public void DiverseCohesion_And_StrategicWeakness()
        {
            // 大戦術家は多様なほど結束UP（16年無反乱）。
            Assert.AreEqual(1.3f, GrandTacticianRules.DiverseForceCohesionFactor(true, 1f), 1e-4f);
            Assert.AreEqual(1.15f, GrandTacticianRules.DiverseForceCohesionFactor(true, 0.5f), 1e-4f);
            Assert.AreEqual(1.0f, GrandTacticianRules.DiverseForceCohesionFactor(true, 0f), 1e-4f);
            // 並は多様なほど結束DOWN（混成ペナルティ）。
            Assert.AreEqual(0.85f, GrandTacticianRules.DiverseForceCohesionFactor(false, 1f), 1e-4f);
            Assert.AreEqual(1.0f, GrandTacticianRules.DiverseForceCohesionFactor(false, 0f), 1e-4f);

            // 戦争に勝つが勝利を活かせない＝内政/兵站が苦手。
            Assert.AreEqual(0.7f, GrandTacticianRules.StrategicWeaknessFactor(true), 1e-4f);
            Assert.AreEqual(1.0f, GrandTacticianRules.StrategicWeaknessFactor(false), 1e-4f);
        }
    }
}
