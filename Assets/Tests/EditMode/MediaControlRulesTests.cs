using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class MediaControlRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void MediaDominance_WeightsOwnershipAndRegulation()
        {
            // 既定は半々の重み。国営0.6×規制0.8 -> 0.7、国営のみ1.0 -> 0.5。
            Assert.AreEqual(0.7f, MediaControlRules.MediaDominance(0.6f, 0.8f), Eps);
            Assert.AreEqual(0.5f, MediaControlRules.MediaDominance(1f, 0f), Eps);
        }

        [Test]
        public void AgendaSetting_NeedsMessageDiscipline()
        {
            // 論調が割れる(0.5)とアジェンダ設定が半減、統一(1)なら支配率がそのまま通る。
            Assert.AreEqual(0.4f, MediaControlRules.AgendaSetting(0.8f, 0.5f), Eps);
            Assert.AreEqual(0.8f, MediaControlRules.AgendaSetting(0.8f, 1f), Eps);
        }

        [Test]
        public void NarrativeControl_AmplifiesAgenda()
        {
            // narrativeGain=0.5 で 1.5 倍、上限1にクランプ。
            Assert.AreEqual(0.6f, MediaControlRules.NarrativeControl(0.4f), Eps);
            Assert.AreEqual(1f, MediaControlRules.NarrativeControl(0.9f), Eps);
        }

        [Test]
        public void CredibilityErosion_ControlErodesTrust()
        {
            // 支配0.8×懐疑0.5×erosionMax0.8 -> 0.32。懐疑0なら信用は減らない。
            Assert.AreEqual(0.32f, MediaControlRules.CredibilityErosion(0.8f, 0.5f), Eps);
            Assert.AreEqual(0f, MediaControlRules.CredibilityErosion(0.8f, 0f), Eps);
        }

        [Test]
        public void UndergroundInformation_FlowsThroughGaps()
        {
            // 信頼喪失0.32×隙0.5 -> 0.16。隙が無ければ流通しない。
            Assert.AreEqual(0.16f, MediaControlRules.UndergroundInformation(0.32f, 0.5f), Eps);
            Assert.AreEqual(0f, MediaControlRules.UndergroundInformation(0.32f, 0f), Eps);
        }

        [Test]
        public void ForeignMediaPenetration_DampenedByOwnNarrative()
        {
            // 国境の穴0.6を自前の物語支配0.5が薄める -> 0.6/1.5 = 0.4。物語支配0なら素通り0.6。
            Assert.AreEqual(0.4f, MediaControlRules.ForeignMediaPenetration(0.6f, 0.5f), Eps);
            Assert.AreEqual(0.6f, MediaControlRules.ForeignMediaPenetration(0.6f, 0f), Eps);
        }

        [Test]
        public void BacklashFromOvercontrol_TriggersAboveThreshold()
        {
            // 閾値0.7超で反動。0.85 -> (0.15/0.3)×0.6 = 0.3。閾値以下は0。
            Assert.AreEqual(0.3f, MediaControlRules.BacklashFromOvercontrol(0.85f), Eps);
            Assert.AreEqual(0f, MediaControlRules.BacklashFromOvercontrol(0.6f), Eps);
        }

        [Test]
        public void IsNarrativeDominant_AdvocacyOverUnderground()
        {
            // 擁護が地下情報を既定閾値0.1超で上回れば支配。拮抗(差0)は false。
            Assert.IsTrue(MediaControlRules.IsNarrativeDominant(0.7f, 0.2f));
            Assert.IsFalse(MediaControlRules.IsNarrativeDominant(0.4f, 0.4f));
        }

        [Test]
        public void Story_HeavyHandedControlBreedsUndergroundAndForeignMedia()
        {
            // 物語：露骨な統制（高支配×懐疑的な国民）が信頼を削り、地下情報と海外メディアを呼び込み、
            // 過剰統制の反動も生じて、物語支配が揺らぐ筋を通しで確認する。
            float dom = MediaControlRules.MediaDominance(0.95f, 0.95f);
            Assert.Greater(dom, 0.9f);

            float agenda = MediaControlRules.AgendaSetting(dom, 0.9f);
            float narrative = MediaControlRules.NarrativeControl(agenda);

            // 懐疑的な国民下では信頼が大きく削れる。
            float erosion = MediaControlRules.CredibilityErosion(dom, 0.9f);
            Assert.Greater(erosion, 0.5f);

            // 統制の隙が大きいと地下情報が流通する。
            float underground = MediaControlRules.UndergroundInformation(erosion, 0.8f);
            Assert.Greater(underground, 0.4f);

            // 自前の物語支配が薄くなれば海外メディアが浸透する余地が出る。
            float weakNarrative = MediaControlRules.NarrativeControl(0.2f);
            float foreignHigh = MediaControlRules.ForeignMediaPenetration(0.7f, weakNarrative);
            float foreignLow = MediaControlRules.ForeignMediaPenetration(0.7f, 0.95f);
            Assert.Greater(foreignHigh, foreignLow);

            // 過剰な支配は反動を招く。
            Assert.Greater(MediaControlRules.BacklashFromOvercontrol(dom), 0f);

            // 結果：地下情報が膨らむと擁護の物語支配は揺らぐ（接戦化）。
            // 物語支配が満点(1.0)でも地下情報がそれに迫れば差は閾値を割り、優位は揺らぐ。
            Assert.IsFalse(MediaControlRules.IsNarrativeDominant(narrative, 0.95f));
        }
    }
}
