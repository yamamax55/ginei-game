using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>偵察衛星・偵察機（前方偵察と監視・#偵察衛星）の純ロジックのテスト。既定Params期待値固定。</summary>
    public class ReconSatelliteRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>カバー率＝機数×走査幅/面積（既定スケール1.0）。</summary>
        [Test]
        public void ReconCoverage_機数と走査幅で覆う()
        {
            // 2機×走査幅3 / 面積10 = 0.6。
            Assert.AreEqual(0.6f, ReconSatelliteRules.ReconCoverage(2f, 3f, 10f), Eps);
            // 大量に掃けば 1.0 で頭打ち（10機×幅3/面積10 = 3 → clamp 1）。
            Assert.AreEqual(1f, ReconSatelliteRules.ReconCoverage(10f, 3f, 10f), Eps);
            // 面積0以下は完全カバー扱い。
            Assert.AreEqual(1f, ReconSatelliteRules.ReconCoverage(1f, 1f, 0f), Eps);
            // 機ゼロは覆えない。
            Assert.AreEqual(0f, ReconSatelliteRules.ReconCoverage(0f, 5f, 10f), Eps);
        }

        /// <summary>解像度＝センサー精度/(1+高度×ペナルティ)。低高度ほど精細。</summary>
        [Test]
        public void Resolution_低高度ほど精細()
        {
            // 高度0＝ペナルティなし＝センサー精度そのまま 0.8。
            Assert.AreEqual(0.8f, ReconSatelliteRules.Resolution(0f, 0.8f), Eps);
            // 高度1＝0.8/(1+1) = 0.4（高いと粗い）。
            Assert.AreEqual(0.4f, ReconSatelliteRules.Resolution(1f, 0.8f), Eps);
            // 高度0.5/精度1 = 1/1.5 = 0.6667。
            Assert.AreEqual(0.66667f, ReconSatelliteRules.Resolution(0.5f, 1f), Eps);
        }

        /// <summary>再訪頻度＝機数/(周期+1)。多機・短周期ほど監視が途切れにくい。</summary>
        [Test]
        public void RevisitRate_機数と周期で監視継続性()
        {
            // 3機/周期5 = 3/6 = 0.5。
            Assert.AreEqual(0.5f, ReconSatelliteRules.RevisitRate(3f, 5f), Eps);
            // 4機/周期1 = 4/2 = 2 → clamp 1（常時監視）。
            Assert.AreEqual(1f, ReconSatelliteRules.RevisitRate(4f, 1f), Eps);
            // 機ゼロは再訪なし。
            Assert.AreEqual(0f, ReconSatelliteRules.RevisitRate(0f, 5f), Eps);
        }

        /// <summary>被発見/撃墜リスク＝(1-高度)×敵防空。低空ほど危険。</summary>
        [Test]
        public void DetectionRisk_低空かつ敵防空で危険()
        {
            // 高度0.2/防空0.8 = 0.8×0.8 = 0.64。
            Assert.AreEqual(0.64f, ReconSatelliteRules.DetectionRisk(0.2f, 0.8f), Eps);
            // 高高度なら敵防空が高くても安全（高度1 → 0）。
            Assert.AreEqual(0f, ReconSatelliteRules.DetectionRisk(1f, 1f), Eps);
            // 敵防空ゼロなら低空でも安全。
            Assert.AreEqual(0f, ReconSatelliteRules.DetectionRisk(0f, 0f), Eps);
        }

        /// <summary>情報鮮度＝再訪頻度/(1+経過×減衰0.5)。再訪多く経過浅いほど新しい。</summary>
        [Test]
        public void IntelFreshness_再訪と経過で鮮度()
        {
            // 再訪0.8/経過2 = 0.8/(1+2×0.5) = 0.8/2 = 0.4。
            Assert.AreEqual(0.4f, ReconSatelliteRules.IntelFreshness(0.8f, 2f), Eps);
            // 経過0なら鮮度＝再訪そのまま。
            Assert.AreEqual(0.8f, ReconSatelliteRules.IntelFreshness(0.8f, 0f), Eps);
        }

        /// <summary>偵察拒否＝敵隠蔽×敵妨害。両方で偵察を潰す。</summary>
        [Test]
        public void ReconDenial_隠蔽と妨害で偵察を潰す()
        {
            // 隠蔽0.5×妨害0.6 = 0.3。
            Assert.AreEqual(0.3f, ReconSatelliteRules.ReconDenial(0.5f, 0.6f), Eps);
            // 妨害ゼロなら拒否ゼロ（積）。
            Assert.AreEqual(0f, ReconSatelliteRules.ReconDenial(1f, 0f), Eps);
        }

        /// <summary>実効偵察価値＝カバー×解像度×鮮度×(1-拒否)。一つ欠けても崩れる。</summary>
        [Test]
        public void EffectiveRecon_四要素の積で価値()
        {
            // 0.6×0.5×0.4×(1-0.25) = 0.12×0.75 = 0.09。
            Assert.AreEqual(0.09f, ReconSatelliteRules.EffectiveRecon(0.6f, 0.5f, 0.4f, 0.25f), Eps);
            // 拒否1.0なら(1-1)=0で価値ゼロ。
            Assert.AreEqual(0f, ReconSatelliteRules.EffectiveRecon(1f, 1f, 1f, 1f), Eps);
        }

        /// <summary>偵察成立＝実効価値－リスクが既定閾値0.2以上か。</summary>
        [Test]
        public void IsReconViable_価値がリスク見合いか()
        {
            // 価値0.5－リスク0.2 = 0.3 ≥ 0.2 → 成立。
            Assert.IsTrue(ReconSatelliteRules.IsReconViable(0.5f, 0.2f));
            // 価値0.3－リスク0.2 = 0.1 < 0.2 → 不成立。
            Assert.IsFalse(ReconSatelliteRules.IsReconViable(0.3f, 0.2f));
        }

        /// <summary>
        /// 物語：低空偵察機を敵防空圏へ投入する判断。低空（高度0.2）は解像度を稼ぐが被発見リスクが高い。
        /// 多機（4機・走査幅4/面積8）で広く覆い再訪を重ね（4機/周期1）、敵の妨害が弱ければ偵察価値は高い。
        /// しかし低空ゆえ被発見リスク0.64が価値を上回り、結局この偵察は成立しない（割に合わない）。
        /// 一方、高高度（高度0.9）に引き上げると解像度は落ちるがリスクが消え、偵察は成立に転じる。
        /// </summary>
        [Test]
        public void Story_低空は危険で割に合わず高高度なら成立する()
        {
            var p = ReconSatelliteParams.Default;

            // --- 低空ドクトリン（高度0.2）---
            float coverage = ReconSatelliteRules.ReconCoverage(4f, 4f, 8f, p); // 4×4/8 = 2 → 1.0
            float resLow = ReconSatelliteRules.Resolution(0.2f, 0.9f, p);      // 0.9/(1+0.2) = 0.75
            float revisit = ReconSatelliteRules.RevisitRate(4f, 1f, p);        // 4/2 = 2 → 1.0
            float fresh = ReconSatelliteRules.IntelFreshness(revisit, 0f, p);  // 1.0/(1) = 1.0
            float denial = ReconSatelliteRules.ReconDenial(0.4f, 0.3f, p);     // 0.12
            float valueLow = ReconSatelliteRules.EffectiveRecon(coverage, resLow, fresh, denial, p);
            // 1.0×0.75×1.0×(1-0.12) = 0.75×0.88 = 0.66。
            Assert.AreEqual(0.66f, valueLow, 1e-3f);
            float riskLow = ReconSatelliteRules.DetectionRisk(0.2f, 0.8f, p);  // 0.8×0.8 = 0.64
            Assert.AreEqual(0.64f, riskLow, Eps);
            // 価値0.66－リスク0.64 = 0.02 < 0.2 → 成立しない（撃墜の危険が高すぎる）。
            Assert.IsFalse(ReconSatelliteRules.IsReconViable(valueLow, riskLow, p.viabilityThreshold));

            // --- 高高度ドクトリン（高度0.9）---
            float resHigh = ReconSatelliteRules.Resolution(0.9f, 0.9f, p);     // 0.9/(1.9) ≈ 0.4737
            float valueHigh = ReconSatelliteRules.EffectiveRecon(coverage, resHigh, fresh, denial, p);
            // 1.0×0.47368×1.0×0.88 ≈ 0.41684。
            Assert.AreEqual(0.41684f, valueHigh, 1e-3f);
            float riskHigh = ReconSatelliteRules.DetectionRisk(0.9f, 0.8f, p); // 0.1×0.8 = 0.08
            Assert.AreEqual(0.08f, riskHigh, Eps);
            // 価値0.41684－リスク0.08 = 0.33684 ≥ 0.2 → 成立（粗いが安全で割に合う）。
            Assert.IsTrue(ReconSatelliteRules.IsReconViable(valueHigh, riskHigh, p.viabilityThreshold));
        }
    }
}
