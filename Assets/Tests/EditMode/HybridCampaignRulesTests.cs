using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// HybridCampaignRules（複合打撃ドクトリン＝ハイブリッド戦・#1374）のテスト。
    /// ドメイン相乗・複合圧力・単独の不足・領域間補強・敵の混乱・帰属の曖昧化・守備側のジレンマ・
    /// ハイブリッド攻勢判定・空配列安全を既定Paramsの具体値で固定する。
    /// </summary>
    public class HybridCampaignRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>ドメイン相乗＝2領域同時で部分の和(1.4)を相乗ぶん(0.24)超える＝1.64。</summary>
        [Test]
        public void DomainSynergy_二領域同時で和を超える()
        {
            float[] domains = { 0.8f, 0.6f };
            // sum=1.4, product=0.48, active=2, bonus=0.5*0.48*1=0.24
            Assert.AreEqual(1.64f, HybridCampaignRules.DomainSynergy(domains), Eps);
        }

        /// <summary>単独ドメインは相乗ゼロ＝和そのまま（0.8）。空/null は 0＝配列安全。</summary>
        [Test]
        public void DomainSynergy_単独は相乗なし_空配列は0()
        {
            Assert.AreEqual(0.8f, HybridCampaignRules.DomainSynergy(new[] { 0.8f }), Eps);
            Assert.AreEqual(0f, HybridCampaignRules.DomainSynergy(new float[0]), Eps);
            Assert.AreEqual(0f, HybridCampaignRules.DomainSynergy(null), Eps);
        }

        /// <summary>5ドメイン複合圧力＝全0.8で相乗込み合計4.65536を飽和し約0.6995。</summary>
        [Test]
        public void MultiVectorPressure_5領域全開で高圧力()
        {
            float pressure = HybridCampaignRules.MultiVectorPressure(0.8f, 0.8f, 0.8f, 0.8f, 0.8f);
            // raw = 4.0 + 0.5*0.8^5*4 = 4.65536, /(raw+2)=0.69949
            Assert.AreEqual(0.69949f, pressure, Eps);
        }

        /// <summary>単独ドメインは満点入力でも決定打上限0.6で頭打ち＝単体では限定的。</summary>
        [Test]
        public void SingleDomainInsufficiency_満点でも頭打ち()
        {
            Assert.AreEqual(0.6f, HybridCampaignRules.SingleDomainInsufficiency(1.0f), Eps);
            Assert.AreEqual(0.3f, HybridCampaignRules.SingleDomainInsufficiency(0.5f), Eps);
        }

        /// <summary>領域間補強＝両立(0.6,0.4)で max0.6 に相乗0.12を上乗せ＝0.72。片方0なら補強なし。</summary>
        [Test]
        public void CrossDomainReinforcement_両立で相乗_片方ゼロで補強なし()
        {
            Assert.AreEqual(0.72f, HybridCampaignRules.CrossDomainReinforcement(0.6f, 0.4f), Eps);
            Assert.AreEqual(0.6f, HybridCampaignRules.CrossDomainReinforcement(0.6f, 0f), Eps);
        }

        /// <summary>敵の混乱＝多方面(0.8)×係数0.8×(1−適応0.25)＝0.48。適応力が高いほど混乱は減る。</summary>
        [Test]
        public void EnemyConfusion_多方面で混乱_適応で減衰()
        {
            Assert.AreEqual(0.48f, HybridCampaignRules.EnemyConfusion(0.8f, 0.25f), Eps);
            Assert.AreEqual(0f, HybridCampaignRules.EnemyConfusion(0.8f, 1.0f), Eps);
        }

        /// <summary>帰属の曖昧化＝代理0.7×否認0.8×係数1.0＝0.56。誰がやったか分からない。</summary>
        [Test]
        public void AttributionAmbiguity_代理と否認で曖昧化()
        {
            Assert.AreEqual(0.56f, HybridCampaignRules.AttributionAmbiguity(0.7f, 0.8f), Eps);
            Assert.AreEqual(0f, HybridCampaignRules.AttributionAmbiguity(0f, 0.8f), Eps);
        }

        /// <summary>守備側ジレンマ＝圧力0.7−資源0.4×カバー1.0＝0.3の隙。資源十分なら隙0。</summary>
        [Test]
        public void DefenderResourceDilemma_全部は守れない()
        {
            Assert.AreEqual(0.3f, HybridCampaignRules.DefenderResourceDilemma(0.7f, 0.4f), Eps);
            Assert.AreEqual(0f, HybridCampaignRules.DefenderResourceDilemma(0.5f, 0.9f), Eps);
        }

        /// <summary>ハイブリッド攻勢判定＝相乗が閾値超え＋多ドメイン同時のみ真。単独強打は偽。</summary>
        [Test]
        public void IsHybridOffensive_多ドメイン連動のみ攻勢()
        {
            Assert.IsTrue(HybridCampaignRules.IsHybridOffensive(1.64f, 0.8f, 1.0f));
            // 相乗が閾値未満＝偽
            Assert.IsFalse(HybridCampaignRules.IsHybridOffensive(0.8f, 0.8f, 1.0f));
            // ドメインが単独規模＝偽
            Assert.IsFalse(HybridCampaignRules.IsHybridOffensive(1.64f, 0.3f, 1.0f));
        }
    }
}
