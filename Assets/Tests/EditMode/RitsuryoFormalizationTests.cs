using NUnit.Framework;
using Ginei;
using FP = Ginei.RitsuryoFormalizationRules.FormalizationParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 律令制の形骸化＝名実の乖離（日本の律令制・官僚制基盤）を固定する。封建制のみが実効で、朝廷の権威が
    /// 下がるほど官職（名）と実際の役割（実）が乖離し、実権は封建領主に残る（史実：摂関→院政→武家→戦国）。
    /// </summary>
    public class RitsuryoFormalizationTests
    {
        [Test]
        public void OfficeAuthorityFactor_TracksCourtAuthority_Clamped()
        {
            Assert.AreEqual(0f, RitsuryoFormalizationRules.OfficeAuthorityFactor(0f), 1e-4f);
            Assert.AreEqual(1f, RitsuryoFormalizationRules.OfficeAuthorityFactor(1f), 1e-4f);
            Assert.AreEqual(1f, RitsuryoFormalizationRules.OfficeAuthorityFactor(1.5f), 1e-4f); // クランプ
        }

        [Test]
        public void EffectiveOfficePower_ShrinksAsAuthorityFalls()
        {
            Assert.AreEqual(12f, RitsuryoFormalizationRules.EffectiveOfficePower(12, 1f), 1e-4f);
            Assert.AreEqual(0f, RitsuryoFormalizationRules.EffectiveOfficePower(12, 0f), 1e-4f); // 形骸化＝実権ゼロ
            Assert.AreEqual(6f, RitsuryoFormalizationRules.EffectiveOfficePower(12, 0.5f), 1e-4f);
            // 位階オーバーロード（正三位 Tier=10）
            Assert.AreEqual(10f, RitsuryoFormalizationRules.EffectiveOfficePower(CourtRank.正三位, 1f), 1e-4f);
        }

        [Test]
        public void TitleRealityGap_WidensWithRankAndDecliningAuthority()
        {
            var p = FP.Default;
            // 権威0で最高位＝完全乖離、権威1で乖離なし
            Assert.AreEqual(1f, RitsuryoFormalizationRules.TitleRealityGap(12, 0f, p), 1e-4f);
            Assert.AreEqual(0f, RitsuryoFormalizationRules.TitleRealityGap(12, 1f, p), 1e-4f);
            // 同じ権威でも高位ほど乖離が大きい
            Assert.Greater(RitsuryoFormalizationRules.TitleRealityGap(12, 0.3f, p),
                           RitsuryoFormalizationRules.TitleRealityGap(6, 0.3f, p));
            // 権威が上がるほど乖離は縮む
            Assert.Greater(RitsuryoFormalizationRules.TitleRealityGap(12, 0.2f, p),
                           RitsuryoFormalizationRules.TitleRealityGap(12, 0.8f, p));
        }

        [Test]
        public void IsHonorary_BelowThreshold()
        {
            var p = FP.Default; // 0.4
            Assert.IsTrue(RitsuryoFormalizationRules.IsHonorary(0.3f, p));
            Assert.IsFalse(RitsuryoFormalizationRules.IsHonorary(0.5f, p));
        }

        [Test]
        public void PrestigeValue_OutlivesRealPower_RetainsFloor()
        {
            var p = FP.Default; // 威信下限0.5
            float prestigeAtZero = RitsuryoFormalizationRules.PrestigeValue(12, 0f, p);
            // 権威0でも威信は残る（戦国大名も官位を欲した）が、実権はゼロ
            Assert.Greater(prestigeAtZero, 0f);
            Assert.AreEqual(0f, RitsuryoFormalizationRules.EffectiveOfficePower(12, 0f), 1e-4f);
            Assert.Greater(prestigeAtZero, RitsuryoFormalizationRules.EffectiveOfficePower(12, 0f));
            // 権威が高いほど威信も高い
            Assert.Greater(RitsuryoFormalizationRules.PrestigeValue(12, 1f, p), prestigeAtZero);
        }

        [Test]
        public void RealPower_FeudalOnlyWhenAuthorityZero()
        {
            var p = FP.Default;
            // 朝廷の権威0＝実権は封建兵力のみ（封建制のみ有効）
            Assert.AreEqual(100f, RitsuryoFormalizationRules.RealPower(6, 100f, 0f, p), 1e-4f);
            // 権威が高いと官職が上乗せされる
            Assert.AreEqual(160f, RitsuryoFormalizationRules.RealPower(6, 100f, 1f, p), 1e-4f);
            Assert.Greater(RitsuryoFormalizationRules.RealPower(6, 100f, 1f, p),
                           RitsuryoFormalizationRules.RealPower(6, 100f, 0f, p));
        }

        [Test]
        public void RealPower_FiefOverload_UsesLevyAndRank()
        {
            var p = FP.Default;
            var fief = new Fief(1f, 200, 0.5f);
            // 従五位下 Tier=6、権威0＝levy のみ
            Assert.AreEqual(200f, RitsuryoFormalizationRules.RealPower(fief, CourtRank.従五位下, 0f, p), 1e-4f);
            Assert.AreEqual(260f, RitsuryoFormalizationRules.RealPower(fief, CourtRank.従五位下, 1f, p), 1e-4f);
            // null Fief は封建兵力0＝官職由来のみ
            Assert.AreEqual(0f, RitsuryoFormalizationRules.RealPower(null, CourtRank.従五位下, 0f, p), 1e-4f);
        }

        [Test]
        public void PhaseOf_DeclinesFromRitsuryoToSengoku()
        {
            Assert.AreEqual(RitsuryoPhase.律令制, RitsuryoFormalizationRules.PhaseOf(0.9f));
            Assert.AreEqual(RitsuryoPhase.摂関政治, RitsuryoFormalizationRules.PhaseOf(0.7f));
            Assert.AreEqual(RitsuryoPhase.院政, RitsuryoFormalizationRules.PhaseOf(0.5f));
            Assert.AreEqual(RitsuryoPhase.武家政権, RitsuryoFormalizationRules.PhaseOf(0.3f));
            Assert.AreEqual(RitsuryoPhase.戦国, RitsuryoFormalizationRules.PhaseOf(0.1f));
            // 権威が下がるほど後段（名実乖離が進む）
            Assert.Less((int)RitsuryoFormalizationRules.PhaseOf(0.9f),
                        (int)RitsuryoFormalizationRules.PhaseOf(0.1f));
        }

        [Test]
        public void CourtAuthority_Shift_Clamps()
        {
            var c = new CourtAuthority(0.5f);
            c.Shift(0.7f);
            Assert.AreEqual(1f, c.authority, 1e-4f);
            c.Shift(-2f);
            Assert.AreEqual(0f, c.authority, 1e-4f);
        }
    }
}
