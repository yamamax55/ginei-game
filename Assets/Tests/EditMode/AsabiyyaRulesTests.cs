using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// アサビーヤ（集団的連帯）を固定する：紐帯は繁栄と世代で薄れ（繁栄が紐帯を溶かす）、軍事的活力は紐帯
    /// そのもの、奢侈は繁栄に比例して積む、王朝サイクルは4世代で寿命、新興勢力は紐帯差で中枢を倒す、
    /// 再生は薄れた紐帯ほど伸びしろが大きいが意志なしはゼロ。物語テスト＝爛熟の末に辺境が優位に立つ。
    /// クランプを担保。
    /// </summary>
    public class AsabiyyaRulesTests
    {
        private static readonly AsabiyyaParams P = AsabiyyaParams.Default;
        // 基礎減衰0.02/繁栄増幅0.05/世代増幅0.01/奢侈成長0.05/寿命4世代/逆転差0.1

        [Test]
        public void AsabiyyaDecayTick_ProsperityAndGenerationsMeltBonds()
        {
            // 繁栄1・2世代：rate=0.02+0.05+0.02=0.09 → 0.9−0.09=0.81
            Assert.AreEqual(0.81f, AsabiyyaRules.AsabiyyaDecayTick(0.9f, 1f, 2f, 1f, P), 1e-5f);
            // 建国直後（繁栄0・世代0）でも基礎ぶんは風化：0.9−0.02=0.88
            Assert.AreEqual(0.88f, AsabiyyaRules.AsabiyyaDecayTick(0.9f, 0f, 0f, 1f, P), 1e-5f);
            // 下限0でクランプ（薄れきった紐帯は負にならない）
            Assert.AreEqual(0f, AsabiyyaRules.AsabiyyaDecayTick(0.05f, 1f, 10f, 1f, P), 1e-5f);
        }

        [Test]
        public void AsabiyyaDecayTick_ProsperityAcceleratesDecay()
        {
            // 同じ世代・dtでも繁栄が高いほど速く薄れる＝豊かさが連帯を要らなくする
            float poor = AsabiyyaRules.AsabiyyaDecayTick(0.9f, 0f, 1f, 1f, P);
            float rich = AsabiyyaRules.AsabiyyaDecayTick(0.9f, 1f, 1f, 1f, P);
            Assert.Greater(poor, rich);
        }

        [Test]
        public void MilitaryVigor_IsBondStrength_Clamped()
        {
            Assert.AreEqual(0.8f, AsabiyyaRules.MilitaryVigor(0.8f), 1e-5f);
            Assert.AreEqual(1f, AsabiyyaRules.MilitaryVigor(2f), 1e-5f);   // 上限
            Assert.AreEqual(0f, AsabiyyaRules.MilitaryVigor(-1f), 1e-5f);  // 下限
        }

        [Test]
        public void LuxuryCorruptionTick_GrowsWithProsperity()
        {
            // 繁栄1：0.1+0.05×1×1=0.15
            Assert.AreEqual(0.15f, AsabiyyaRules.LuxuryCorruptionTick(0.1f, 1f, 1f, P), 1e-5f);
            // 繁栄0＝贅沢の素がない＝据え置き
            Assert.AreEqual(0.1f, AsabiyyaRules.LuxuryCorruptionTick(0.1f, 0f, 1f, P), 1e-5f);
            // 上限1
            Assert.AreEqual(1f, AsabiyyaRules.LuxuryCorruptionTick(0.99f, 1f, 5f, P), 1e-5f);
        }

        [Test]
        public void DynastyLifecycle_FourGenerationLifespan()
        {
            Assert.AreEqual(0.25f, AsabiyyaRules.DynastyLifecycle(1f, P), 1e-5f); // 建設者
            Assert.AreEqual(0.5f, AsabiyyaRules.DynastyLifecycle(2f, P), 1e-5f);  // 維持者
            Assert.AreEqual(1f, AsabiyyaRules.DynastyLifecycle(4f, P), 1e-5f);    // 寿命到達
            Assert.AreEqual(1f, AsabiyyaRules.DynastyLifecycle(8f, P), 1e-5f);    // 超過は頭打ち
        }

        [Test]
        public void ChallengerAdvantage_StrongFrontierBeatsRipeCenter()
        {
            // 爛熟した中枢(0.2) vs 紐帯の強い辺境(0.8)＝後者が勝つ
            Assert.IsTrue(AsabiyyaRules.ChallengerAdvantage(0.2f, 0.8f, P));
            // 僅差（0.05<0.1）＝倒せない＝中枢健在
            Assert.IsFalse(AsabiyyaRules.ChallengerAdvantage(0.8f, 0.85f, P));
            // ちょうど差0.1＝逆転成立（境界）
            Assert.IsTrue(AsabiyyaRules.ChallengerAdvantage(0.5f, 0.6f, P));
        }

        [Test]
        public void RenewalChance_FadedBondsHaveMoreUpside_NoWillIsZero()
        {
            // 薄れた紐帯(0.2)＋強い改革意志(1)＝0.8の回復見込み（中興の祖）
            Assert.AreEqual(0.8f, AsabiyyaRules.RenewalChance(0.2f, 1f), 1e-5f);
            // 改革意志ゼロ＝自然には戻らない
            Assert.AreEqual(0f, AsabiyyaRules.RenewalChance(0.2f, 0f), 1e-5f);
            // 既に満ちた紐帯＝伸びしろなし
            Assert.AreEqual(0f, AsabiyyaRules.RenewalChance(1f, 1f), 1e-5f);
        }

        [Test]
        public void Story_ProsperityKillsBonds_FrontierTakesOver()
        {
            // 建国の強い紐帯(0.95)が繁栄(1.0)の中で世代を経るごとに薄れていき、
            // やがて辺境の紐帯の強い新興勢力(0.7)に追い越され、王朝が取って代わられる。
            float incumbent = 0.95f;
            const float challenger = 0.7f; // 辺境＝繁栄に触れず紐帯を保つ
            bool overtaken = false;

            for (int gen = 0; gen < 5; gen++)
            {
                // 1世代＝繁栄1.0のもとで紐帯が摩耗（dtは1世代ぶん）
                incumbent = AsabiyyaRules.AsabiyyaDecayTick(incumbent, 1f, gen, 1f, P);
                if (AsabiyyaRules.ChallengerAdvantage(incumbent, challenger, P))
                {
                    overtaken = true;
                    break;
                }
            }

            // 繁栄が紐帯を溶かし、最後には辺境の新興勢力が中枢を倒す＝王朝の自然寿命
            Assert.IsTrue(overtaken);
            Assert.Less(incumbent, challenger);
        }
    }
}
