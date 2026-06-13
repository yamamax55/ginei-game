using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 現地調達（糧を敵に因る・#1128）を固定する：徴発量は豊かさ×努力×（敵対で痩せる）・補給線軽減は需要までの
    /// 賄えた分・徴発の恨みは敵対ほど深く友好でもゼロにはならない（略奪は次の反乱を育てる）・同じ星系は絞ると涸れる
    /// （焦土化）・現地調達依存ほど軍は速い・大軍は1星系を食い潰す（充足率<1）。トレードオフとクランプを担保。
    /// </summary>
    public class ForageRulesTests
    {
        private static readonly ForageParams P = ForageParams.Default; // 規模100/敵対妨害0.8/恨み0.6/基礎恨み0.1/枯渇0.2/機動加速0.3/養兵量500

        [Test]
        public void ForageYield_RichLowHostility_GathersMore()
        {
            // 豊かさ1・全力・敵対ゼロ → 100×1×1×1＝100（最大）
            Assert.AreEqual(100f, ForageRules.ForageYield(1f, 1f, 0f, P), 1e-3f);
            // 敵対0.5で妨害＝1−0.5×0.8＝0.6 → 100×0.6＝60（敵地は痩せる）
            Assert.AreEqual(60f, ForageRules.ForageYield(1f, 1f, 0.5f, P), 1e-3f);
            // 豊かさ0.5・努力0.5・敵対ゼロ → 100×0.5×0.5＝25
            Assert.AreEqual(25f, ForageRules.ForageYield(0.5f, 0.5f, 0f, P), 1e-3f);
            // 入力過大はクランプ（豊かさ・努力・敵対）
            Assert.AreEqual(20f, ForageRules.ForageYield(2f, 2f, 1.5f, P), 1e-3f); // 100×1×1×(1−0.8)
        }

        [Test]
        public void SupplyLineRelief_CappedAtDemand()
        {
            // 徴発40・需要100 → 40だけ後方輸送が要らない
            Assert.AreEqual(40f, ForageRules.SupplyLineRelief(40f, 100f), 1e-4f);
            // 需要を超える徴発は余剰＝軽減は需要まで
            Assert.AreEqual(100f, ForageRules.SupplyLineRelief(150f, 100f), 1e-4f);
            // 負入力はクランプ
            Assert.AreEqual(0f, ForageRules.SupplyLineRelief(-10f, 100f), 1e-5f);
        }

        [Test]
        public void PopulationResentment_DeeperWhenHostile_NeverZeroWhenForaging()
        {
            // 全力徴発・友好(敵対0) → 1×(0.1+0.9×0)×0.6＝0.06（友好でもゼロにはならない＝取り立て自体が反感）
            Assert.AreEqual(0.06f, ForageRules.PopulationResentment(1f, 0f, P), 1e-4f);
            // 全力徴発・敵対1 → 1×(0.1+0.9×1)×0.6＝0.6（最大＝略奪は次の反乱を育てる）
            Assert.AreEqual(0.6f, ForageRules.PopulationResentment(1f, 1f, P), 1e-4f);
            // 努力0なら恨みは生まれない
            Assert.AreEqual(0f, ForageRules.PopulationResentment(0f, 1f, P), 1e-5f);
            // 敵対が高いほど深い（単調）
            Assert.Less(ForageRules.PopulationResentment(1f, 0.3f, P), ForageRules.PopulationResentment(1f, 0.7f, P));
        }

        [Test]
        public void DepletionTick_KeepSqueezing_RunsDry()
        {
            // 豊かさ1・全力強度・dt1 → 1−(1×1×0.2×1)＝0.8（絞れば痩せる）
            Assert.AreEqual(0.8f, ForageRules.DepletionTick(1f, 1f, 1f, P), 1e-4f);
            // 強度を半分にすれば枯渇も緩い → 1−0.1＝0.9
            Assert.AreEqual(0.9f, ForageRules.DepletionTick(1f, 0.5f, 1f, P), 1e-4f);
            // 絞らなければ涸れない
            Assert.AreEqual(0.5f, ForageRules.DepletionTick(0.5f, 0f, 1f, P), 1e-5f);
            // dt0は現状維持
            Assert.AreEqual(1f, ForageRules.DepletionTick(1f, 1f, 0f, P), 1e-5f);
        }

        [Test]
        public void MobilitySpeedBonus_RelianceMakesArmyFaster()
        {
            // 依存ゼロは加速なし（補給線頼みで鈍重）
            Assert.AreEqual(1f, ForageRules.MobilitySpeedBonus(0f, P), 1e-5f);
            // 全面現地調達 → 1＋0.3＝1.3倍（輜重を引かない軍は速い）
            Assert.AreEqual(1.3f, ForageRules.MobilitySpeedBonus(1f, P), 1e-4f);
            // 依存0.5で1.15倍・過大入力はクランプ
            Assert.AreEqual(1.15f, ForageRules.MobilitySpeedBonus(0.5f, P), 1e-4f);
            Assert.AreEqual(1.3f, ForageRules.MobilitySpeedBonus(2f, P), 1e-4f);
        }

        [Test]
        public void SustainabilityLimit_BigArmyEatsOneSystemEmpty()
        {
            // 豊かさ1の星系は500の兵力を養える
            Assert.AreEqual(500f, ForageRules.SustainabilityLimit(1f, P), 1e-3f);
            // 軍500なら充足率1.0（現地調達のみで賄える）
            Assert.AreEqual(1f, ForageRules.SustainabilityRatio(1f, 500f, P), 1e-4f);
            // 軍1000は1星系を食い潰す＝充足率0.5（不足分は後方補給に頼る）
            Assert.AreEqual(0.5f, ForageRules.SustainabilityRatio(1f, 1000f, P), 1e-4f);
            // 軍がいなければ常に充足
            Assert.AreEqual(1f, ForageRules.SustainabilityRatio(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void Tradeoff_HostileForaging_MoreYieldButMoreResentment()
        {
            // 同じ全力徴発でも、敵対度が上がると徴発量は減り恨みは増える＝取り立てのトレードオフ
            float yieldFriendly = ForageRules.ForageYield(1f, 1f, 0.1f, P);
            float yieldHostile = ForageRules.ForageYield(1f, 1f, 0.9f, P);
            float resFriendly = ForageRules.PopulationResentment(1f, 0.1f, P);
            float resHostile = ForageRules.PopulationResentment(1f, 0.9f, P);
            Assert.Greater(yieldFriendly, yieldHostile);   // 敵地ほど集まらない
            Assert.Less(resFriendly, resHostile);          // 敵地ほど恨みが深い
        }
    }
}
