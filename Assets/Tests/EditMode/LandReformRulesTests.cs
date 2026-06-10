using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>土地改革（資産の再分配）の純ロジックのテスト。既定 Params の具体値で期待値を固定し、
    /// 細分化ペナルティと小作率による「割の良さ」を担保する。</summary>
    public class LandReformRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>意欲の向上＝範囲×小作率×0.4。小作が多いほど買える意欲が大きい（割に合う）。</summary>
        [Test]
        public void ProductivityGain_ScalesWithTenancy()
        {
            // 全範囲・小作率1.0＝最大 0.4
            Assert.AreEqual(0.4f, LandReformRules.ProductivityGain(1f, 1f), Eps);
            // 同じ範囲でも小作が半分なら利得も半分＝小作が少ない社会は割に合わない
            Assert.AreEqual(0.2f, LandReformRules.ProductivityGain(1f, 0.5f), Eps);
            // 自作農だらけ（小作率0）なら配っても意欲は買えない
            Assert.AreEqual(0f, LandReformRules.ProductivityGain(1f, 0f), Eps);
        }

        /// <summary>農民支持＝範囲×0.5。土地を配るほど農民は味方する。</summary>
        [Test]
        public void PeasantSupport_ScalesWithScope()
        {
            Assert.AreEqual(0.5f, LandReformRules.PeasantSupport(1f), Eps);
            Assert.AreEqual(0.25f, LandReformRules.PeasantSupport(0.5f), Eps);
            Assert.AreEqual(0f, LandReformRules.PeasantSupport(0f), Eps);
        }

        /// <summary>地主反発＝範囲×(1−補償)×0.7。補償で牙を抜く。</summary>
        [Test]
        public void LandlordBacklash_EasedByCompensation()
        {
            // 全範囲・無補償＝最大 0.7
            Assert.AreEqual(0.7f, LandReformRules.LandlordBacklash(1f, 0f), Eps);
            // 補償0.5で半減
            Assert.AreEqual(0.35f, LandReformRules.LandlordBacklash(1f, 0.5f), Eps);
            // 完全補償で反発消失
            Assert.AreEqual(0f, LandReformRules.LandlordBacklash(1f, 1f), Eps);
        }

        /// <summary>短期混乱＝範囲×速度×0.4。急ぐほど現場が乱れ、漸進改革は浅い。</summary>
        [Test]
        public void ShortTermDisruption_WorseWhenRushed()
        {
            // 全範囲・即時＝最大 0.4
            Assert.AreEqual(0.4f, LandReformRules.ShortTermDisruption(1f, 1f), Eps);
            // 漸進（速度0.25）なら混乱は浅い
            Assert.AreEqual(0.1f, LandReformRules.ShortTermDisruption(1f, 0.25f), Eps);
        }

        /// <summary>細分化ペナルティ＝範囲×(1−機械化)×0.3。機械化なしの全範囲が最悪、機械化で相殺。</summary>
        [Test]
        public void FragmentationPenalty_MitigatedByMechanization()
        {
            // 機械化ゼロ・全範囲＝最大 0.3
            Assert.AreEqual(0.3f, LandReformRules.FragmentationPenalty(1f, 0f), Eps);
            // 機械化0.5で半減
            Assert.AreEqual(0.15f, LandReformRules.FragmentationPenalty(1f, 0.5f), Eps);
            // 完全機械化で細分化の弊害なし
            Assert.AreEqual(0f, LandReformRules.FragmentationPenalty(1f, 1f), Eps);
        }

        /// <summary>純産出＝意欲−細分化。小作が多ければ正、機械化なしで自作農社会へ全配分すると負。</summary>
        [Test]
        public void NetOutput_PositiveWhenTenancyHighNegativeWhenFragmented()
        {
            // 小作率1.0・機械化0.5・全範囲＝0.4 − 0.15 = 0.25（割に合う）
            Assert.AreEqual(0.25f, LandReformRules.NetOutput(1f, 1f, 0.5f), Eps);
            // 小作率0.2・機械化ゼロ・全範囲＝0.08 − 0.3 = −0.22（行き過ぎた平等は赤字）
            Assert.AreEqual(-0.22f, LandReformRules.NetOutput(1f, 0.2f, 0f), Eps);
        }

        /// <summary>入力は全てクランプ＝範囲外でも例外を出さず端で飽和する（決定論）。</summary>
        [Test]
        public void Inputs_AreClamped()
        {
            // 範囲・小作率を上振れさせても最大で頭打ち
            Assert.AreEqual(0.4f, LandReformRules.ProductivityGain(5f, 5f), Eps);
            // 負の補償は0扱い＝反発は最大のまま
            Assert.AreEqual(0.7f, LandReformRules.LandlordBacklash(1f, -1f), Eps);
            // 速度を下振れさせても0で底打ち＝混乱なし
            Assert.AreEqual(0f, LandReformRules.ShortTermDisruption(1f, -2f), Eps);
        }
    }
}
