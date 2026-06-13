using NUnit.Framework;
using UnityEngine;
using Ginei;
using ChremParams = Ginei.ChrematisticsRules.ChrematisticsParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 収奪経済志向（ChrematisticsRules・#1502 アリストテレス）を固定する：取得モードの軸（家政術−／蓄財術＋）、
    /// 家政術の自然な充足の限界（足れば止まる）、蓄財術の際限なき蓄積（止まらない）、無制限の蓄財の共同体腐食、
    /// 高利貸しの不自然さ（最も腐敗的・突出）、貪欲による別経路の腐敗加速、生産的vs収奪的の比、蓄財術支配判定。
    /// 既定 Params の具体値で期待値固定。
    /// </summary>
    public class ChrematisticsRulesTests
    {
        // 取得モード：必要充足が勝てば家政術（−）、無制限の利得が勝てば蓄財術（＋）、釣り合えば0・クランプ
        [Test]
        public void AcquisitionMode_NeedVsGain_FixedAxis()
        {
            Assert.AreEqual(-0.7f, ChrematisticsRules.AcquisitionMode(0.8f, 0.1f), 1e-4f); // 必要充足優位＝家政術
            Assert.AreEqual(0.7f, ChrematisticsRules.AcquisitionMode(0.1f, 0.8f), 1e-4f);  // 無制限利得優位＝蓄財術
            Assert.AreEqual(0f, ChrematisticsRules.AcquisitionMode(0.5f, 0.5f), 1e-4f);    // 釣り合い
            Assert.AreEqual(-1f, ChrematisticsRules.AcquisitionMode(5f, 0f), 1e-4f);       // 入力クランプ
            Assert.AreEqual(1f, ChrematisticsRules.AcquisitionMode(0f, 5f), 1e-4f);
            Assert.AreEqual(EconomicMotive.家政術, ChrematisticsRules.MotiveOf(-0.7f));
            Assert.AreEqual(EconomicMotive.蓄財術, ChrematisticsRules.MotiveOf(0.7f));
        }

        // 自然な限界：充足開始域(0.5)以下は0、満たされてから非線形に飽和（足れば止まる管理型）
        [Test]
        public void NaturalLimit_SatiatesWhenNeedsMet()
        {
            Assert.AreEqual(0f, ChrematisticsRules.NaturalLimit(0.5f), 1e-4f);   // まだ足りない＝止まらない
            Assert.AreEqual(0f, ChrematisticsRules.NaturalLimit(0.2f), 1e-4f);
            Assert.AreEqual(0.25f, ChrematisticsRules.NaturalLimit(0.75f), 1e-4f); // t=0.5 → 0.5^2 = 0.25
            Assert.AreEqual(1f, ChrematisticsRules.NaturalLimit(1f), 1e-4f);     // 完全充足＝完全に止まる
            Assert.AreEqual(1f, ChrematisticsRules.NaturalLimit(5f), 1e-4f);     // 入力クランプ
        }

        // 際限なき蓄積：蓄財側（＋）だけが追い続ける、家政術側（−）は0（足れば止まる）
        [Test]
        public void UnboundedAccumulation_OnlyChrematisticSideAccumulates()
        {
            Assert.AreEqual(0f, ChrematisticsRules.UnboundedAccumulation(-0.5f), 1e-4f); // 家政術＝止まる
            Assert.AreEqual(0f, ChrematisticsRules.UnboundedAccumulation(0f), 1e-4f);
            Assert.AreEqual(0.5f, ChrematisticsRules.UnboundedAccumulation(0.5f), 1e-4f); // 蓄財術＝止まらない
            Assert.AreEqual(1f, ChrematisticsRules.UnboundedAccumulation(1f), 1e-4f);     // 貨幣のための貨幣＝最大
        }

        // 社会の腐食：無制限の利得が紐帯を削るが、共同体の絆が強いほど和らぐ
        [Test]
        public void SocialCorrosion_GreedErodesBond_StrongBondResists()
        {
            Assert.AreEqual(0.5f, ChrematisticsRules.SocialCorrosion(1f, 0.5f), 1e-4f); // 1×(1−0.5)×1
            Assert.AreEqual(1f, ChrematisticsRules.SocialCorrosion(1f, 0f), 1e-4f);     // 絆ゼロ＝守銭奴が蝕む
            Assert.AreEqual(0f, ChrematisticsRules.SocialCorrosion(1f, 1f), 1e-4f);     // 強い絆＝腐食しない
            Assert.AreEqual(0f, ChrematisticsRules.SocialCorrosion(0f, 0.5f), 1e-4f);   // 蓄財なし＝腐食なし
        }

        // 高利貸し：貨幣が貨幣を生む不自然さ＝非線形に跳ね、重み1.5で突出（最も腐敗的）
        [Test]
        public void UsuryUnnaturalness_IsMostUnnatural_NonlinearAndAmplified()
        {
            Assert.AreEqual(0f, ChrematisticsRules.UsuryUnnaturalness(0f), 1e-4f);
            Assert.AreEqual(0.375f, ChrematisticsRules.UsuryUnnaturalness(0.5f), 1e-4f); // 0.5^2×1.5 = 0.375
            Assert.AreEqual(1.5f, ChrematisticsRules.UsuryUnnaturalness(1f), 1e-4f);     // 突出＝1超（最も不自然）
            // 同じ収奪量でも高利貸しは無制限蓄積より重く効く（突出を式に出す）
            Assert.Greater(ChrematisticsRules.UsuryUnnaturalness(1f), ChrematisticsRules.UnboundedAccumulation(1f));
        }

        // 貪欲の腐敗：蓄財側（＋）だけが別経路で腐敗を加速、家政術は加速しない、dt0は無変化
        [Test]
        public void CorruptionViaGreed_OnlyChrematisticGreedAccelerates()
        {
            Assert.AreEqual(0.05f, ChrematisticsRules.CorruptionViaGreed(0.5f, 1f), 1e-4f); // 0.5×0.1×1
            Assert.AreEqual(0.1f, ChrematisticsRules.CorruptionViaGreed(1f, 1f), 1e-4f);
            Assert.AreEqual(0f, ChrematisticsRules.CorruptionViaGreed(-0.5f, 1f), 1e-4f);   // 家政術＝腐敗せず
            Assert.AreEqual(0f, ChrematisticsRules.CorruptionViaGreed(1f, 0f), 1e-4f);      // dt0＝無変化
            Assert.AreEqual(0f, ChrematisticsRules.CorruptionViaGreed(1f, -1f), 1e-4f);     // 負dt＝無変化
        }

        // 生産的 vs 収奪的：生産優位で社会が富み（＋）、収奪優位で痩せる（−）、両0は中立
        [Test]
        public void ProductiveVsExtractive_ExtractiveStarvesSociety()
        {
            Assert.AreEqual(0.5f, ChrematisticsRules.ProductiveVsExtractive(0.75f, 0.25f), 1e-4f); // (0.75−0.25)/1
            Assert.AreEqual(-0.5f, ChrematisticsRules.ProductiveVsExtractive(0.25f, 0.75f), 1e-4f); // 収奪優位＝痩せる
            Assert.AreEqual(0f, ChrematisticsRules.ProductiveVsExtractive(0.5f, 0.5f), 1e-4f);       // 拮抗＝中立
            Assert.AreEqual(0f, ChrematisticsRules.ProductiveVsExtractive(0f, 0f), 1e-4f);           // 経済活動なし
            Assert.AreEqual(1f, ChrematisticsRules.ProductiveVsExtractive(1f, 0f), 1e-4f);
        }

        // 蓄財術支配：取得モードがしきい値以上で支配的（既定0＝蓄財側に傾く）
        [Test]
        public void IsChrematisticDominant_ThresholdAtZero()
        {
            Assert.IsTrue(ChrematisticsRules.IsChrematisticDominant(0.1f));   // 蓄財側＝支配的
            Assert.IsTrue(ChrematisticsRules.IsChrematisticDominant(0f));     // しきい値ちょうど
            Assert.IsFalse(ChrematisticsRules.IsChrematisticDominant(-0.1f)); // 家政術側＝健全
            // カスタムしきい値：厳しめ(0.5)では弱い蓄財傾向は支配と見なさない
            Assert.IsFalse(ChrematisticsRules.IsChrematisticDominant(0.3f, 0.5f));
            Assert.IsTrue(ChrematisticsRules.IsChrematisticDominant(0.6f, 0.5f));
        }

        // 既定 Params の具体値（限界開始0.5・飽和指数2・高利貸し重み1.5・上限1.5・貪欲腐敗0.1）
        [Test]
        public void DefaultParams_FixedValues()
        {
            var p = ChremParams.Default;
            Assert.AreEqual(0.5f, p.limitOnset, 1e-4f);
            Assert.AreEqual(2f, p.satiationExponent, 1e-4f);
            Assert.AreEqual(1f, p.accumulationScale, 1e-4f);
            Assert.AreEqual(1.5f, p.usuryWeight, 1e-4f);
            Assert.AreEqual(1.5f, p.maxUsury, 1e-4f);
            Assert.AreEqual(0.1f, p.greedCorruptionRate, 1e-4f);
            // ctor クランプ：限界開始は0.95上限・指数は1下限
            var clamped = new ChremParams(5f, 0.1f, -1f, -1f, 0.1f, -1f, -1f, -1f, -1f);
            Assert.AreEqual(0.95f, clamped.limitOnset, 1e-4f);
            Assert.AreEqual(1f, clamped.satiationExponent, 1e-4f);
            Assert.AreEqual(0f, clamped.accumulationScale, 1e-4f);
        }
    }
}
