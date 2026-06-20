using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 宮廷陰謀（讒言・権謀術数）の力学を固定する：君主の暗愚さと標的の声望で割れる讒言、
    /// 共謀者数と連携で増す精巧さ（だが脆い）、精巧さは隠し人数と警戒が暴く露見リスク、
    /// 讒言×政敵の隙の失脚工作、政治力×人脈の立ち回り、露見×不興の墓穴、そして
    /// 失脚＋立ち回り−反動の正味成果（-1..1）と成功判定。既定Paramsの期待値と境界を担保。
    /// </summary>
    public class CourtIntrigueRulesTests
    {
        private static readonly CourtIntrigueParams P = CourtIntrigueParams.Default;
        // 暗愚1.0・精巧0.2/人＋連携下限0.4・隠蔽0.3・口0.1/人・警戒0.6・隙1.0・人脈0.5・不興1.0・立ち回り0.4・閾値0.3

        [Test]
        public void SlanderEffect_GullibilityRaises_ReputationDeflects()
        {
            // 信憑性1×暗愚1÷(信憑性1+声望1)=0.5
            Assert.AreEqual(0.5f, CourtIntrigueRules.SlanderEffect(1f, 1f, 1f, P), 1e-4f);
            // やや信憑0.8×暗愚0.5÷(0.8+0.2)=0.4／声望が低いと刺さる
            Assert.AreEqual(0.4f, CourtIntrigueRules.SlanderEffect(0.8f, 0.5f, 0.2f, P), 1e-4f);
            // 信憑性も声望も0なら分母0＝効果なし
            Assert.AreEqual(0f, CourtIntrigueRules.SlanderEffect(0f, 0.5f, 0f, P), 1e-4f);
            // 入力は0..1にクランプ（5,5,5→1,1,1）=0.5
            Assert.AreEqual(0.5f, CourtIntrigueRules.SlanderEffect(5f, 5f, 5f, P), 1e-4f);
        }

        [Test]
        public void SchemeComplexity_HeadcountAndCoordination()
        {
            // 3人×0.2=0.6・連携満点で仕上げ1.0＝0.6
            Assert.AreEqual(0.6f, CourtIntrigueRules.SchemeComplexity(3f, 1f, P), 1e-4f);
            // 同じ3人でも連携皆無＝下限0.4 → 0.6×0.4=0.24
            Assert.AreEqual(0.24f, CourtIntrigueRules.SchemeComplexity(3f, 0f, P), 1e-4f);
            // 大人数（10人）は規模が上限1に飽和 → 連携満点で1.0
            Assert.AreEqual(1f, CourtIntrigueRules.SchemeComplexity(10f, 1f, P), 1e-4f);
            // 単独（0人）は精巧さ0
            Assert.AreEqual(0f, CourtIntrigueRules.SchemeComplexity(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void ExposureRisk_HeadcountVigilanceUp_ComplexityConceals()
        {
            // 3人（0.3）警戒なし・精巧さ0＝隠蔽効かず0.3
            Assert.AreEqual(0.3f, CourtIntrigueRules.ExposureRisk(0f, 3f, 0f, P), 1e-4f);
            // 同条件で精巧さ満点＝(1−1×0.3)=0.7倍 → 0.21
            Assert.AreEqual(0.21f, CourtIntrigueRules.ExposureRisk(1f, 3f, 0f, P), 1e-4f);
            // 2人（0.2）＋敵の警戒満点（0.6）=0.8・精巧0.5なら隠蔽0.85倍 → 0.68
            Assert.AreEqual(0.68f, CourtIntrigueRules.ExposureRisk(0.5f, 2f, 1f, P), 1e-4f);
            // 大人数＋警戒は上限1で頭打ち
            Assert.AreEqual(1f, CourtIntrigueRules.ExposureRisk(1f, 10f, 1f, P), 1e-4f);
        }

        [Test]
        public void RivalDownfall_NeedsBothSlanderAndOpening()
        {
            // 讒言0.5×政敵の隙0.8＝0.4
            Assert.AreEqual(0.4f, CourtIntrigueRules.RivalDownfall(0.5f, 0.8f, P), 1e-4f);
            // 隙が満点でも讒言が薄ければ薄い＝0.4×1=0.4
            Assert.AreEqual(0.4f, CourtIntrigueRules.RivalDownfall(0.4f, 1f, P), 1e-4f);
            // 隙が無ければ讒言が刺さっても失脚しない
            Assert.AreEqual(0f, CourtIntrigueRules.RivalDownfall(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void CourtManeuvering_SkillTimesNetwork()
        {
            // 政治力0.8×人脈（1×0.5）=0.4
            Assert.AreEqual(0.4f, CourtIntrigueRules.CourtManeuvering(0.8f, 1f, P), 1e-4f);
            // 政治力満点×人脈満点でも人脈重み0.5で頭打ち＝0.5
            Assert.AreEqual(0.5f, CourtIntrigueRules.CourtManeuvering(1f, 1f, P), 1e-4f);
            // 人脈ゼロの切れ者は空回り
            Assert.AreEqual(0f, CourtIntrigueRules.CourtManeuvering(0.5f, 0f, P), 1e-4f);
        }

        [Test]
        public void Backfire_NeedsExposureAndDispleasure()
        {
            // 露見0.5×不興0.6＝0.3 の墓穴
            Assert.AreEqual(0.3f, CourtIntrigueRules.Backfire(0.5f, 0.6f, P), 1e-4f);
            // 不興満点でも露見が薄ければ薄い＝0.4
            Assert.AreEqual(0.4f, CourtIntrigueRules.Backfire(0.4f, 1f, P), 1e-4f);
            // 君主が不興でなければ咎められない（露見しても反動なし）
            Assert.AreEqual(0f, CourtIntrigueRules.Backfire(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void NetIntrigueGain_DownfallPlusManeuverMinusBackfire()
        {
            // 失脚0.4＋立ち回り0.5×0.4(=0.2)−反動0.1＝0.5
            Assert.AreEqual(0.5f, CourtIntrigueRules.NetIntrigueGain(0.4f, 0.5f, 0.1f, P), 1e-4f);
            // 成果なしで反動だけ＝負値（自滅）
            Assert.AreEqual(-0.8f, CourtIntrigueRules.NetIntrigueGain(0f, 0f, 0.8f, P), 1e-4f);
            // 上限1にクランプ（1+0.4−0=1.4）
            Assert.AreEqual(1f, CourtIntrigueRules.NetIntrigueGain(1f, 1f, 0f, P), 1e-4f);
            // 下限-1にクランプ
            Assert.AreEqual(-1f, CourtIntrigueRules.NetIntrigueGain(0f, 0f, 1f, P), 1e-4f);
        }

        [Test]
        public void IsIntrigueSuccessful_ThresholdGate()
        {
            // 既定閾値0.3を上回れば成功
            Assert.IsTrue(CourtIntrigueRules.IsIntrigueSuccessful(0.5f, 0.3f));
            Assert.IsFalse(CourtIntrigueRules.IsIntrigueSuccessful(0.2f, 0.3f));
            // 自滅（負値）は当然不成功
            Assert.IsFalse(CourtIntrigueRules.IsIntrigueSuccessful(-0.8f));
            // 既定委譲版も同じ閾値0.3
            Assert.IsTrue(CourtIntrigueRules.IsIntrigueSuccessful(0.5f));
        }

        [Test]
        public void DefaultParams_CtorClamps()
        {
            // 負値・範囲外はctorでクランプ
            var p = new CourtIntrigueParams(-1f, -1f, 2f, 2f, -1f, -1f, -1f, -1f, -1f, -1f, 2f);
            Assert.AreEqual(0f, p.gullibilityWeight, 1e-6f);
            Assert.AreEqual(1f, p.coordinationFloor, 1e-6f);   // 0..1にクランプ
            Assert.AreEqual(1f, p.complexityConceals, 1e-6f);  // 0..1にクランプ
            Assert.AreEqual(0f, p.headcountRisk, 1e-6f);
            Assert.AreEqual(1f, p.successThreshold, 1e-6f);    // 0..1にクランプ
            // 既定値の固定
            Assert.AreEqual(1.0f, P.gullibilityWeight, 1e-6f);
            Assert.AreEqual(0.2f, P.complexityPerConspirator, 1e-6f);
            Assert.AreEqual(0.4f, P.coordinationFloor, 1e-6f);
            Assert.AreEqual(0.3f, P.successThreshold, 1e-6f);
        }

        [Test]
        public void Narrative_AmbitiousNobleToplesARival()
        {
            // 物語：野心的な門閥貴族が、暗愚な皇帝に取り入って政敵を讒言で失脚させる。
            // やや作り話めいた讒言（信憑性0.6）だが皇帝は暗愚（0.8）、政敵の声望は中(0.4)。
            float slander = CourtIntrigueRules.SlanderEffect(0.6f, 0.8f, 0.4f, P);
            // 0.6×0.8÷(0.6+0.4)=0.48
            Assert.AreEqual(0.48f, slander, 1e-4f);

            // 少人数（2人）の精巧な共謀（連携満点）。
            float complexity = CourtIntrigueRules.SchemeComplexity(2f, 1f, P); // 0.4
            // 警戒の薄い宮廷（0.2）＝露見は低い。
            float exposure = CourtIntrigueRules.ExposureRisk(complexity, 2f, 0.2f, P);
            // raw=2×0.1+0.2×0.6=0.32・conceal=1−0.4×0.3=0.88 → 0.2816
            Assert.AreEqual(0.2816f, exposure, 1e-4f);

            // 政敵には付け入る隙（0.7）があり失脚工作は実る。
            float downfall = CourtIntrigueRules.RivalDownfall(slander, 0.7f, P);
            // 0.48×0.7=0.336
            Assert.AreEqual(0.336f, downfall, 1e-4f);

            // 自らは政治力（0.8）と人脈（0.6）で立ち回る。
            float maneuver = CourtIntrigueRules.CourtManeuvering(0.8f, 0.6f, P);
            // 0.8×(0.6×0.5)=0.24
            Assert.AreEqual(0.24f, maneuver, 1e-4f);

            // 皇帝に露見しても不興は薄い（0.3）。
            float backfire = CourtIntrigueRules.Backfire(exposure, 0.3f, P);
            // 0.2816×0.3=0.08448
            Assert.AreEqual(0.08448f, backfire, 1e-4f);

            // 正味＝0.336＋0.24×0.4(=0.096)−0.08448=0.34752 ＞閾値0.3 ＝陰謀成功。
            float net = CourtIntrigueRules.NetIntrigueGain(downfall, maneuver, backfire, P);
            Assert.AreEqual(0.34752f, net, 1e-4f);
            Assert.IsTrue(CourtIntrigueRules.IsIntrigueSuccessful(net, P.successThreshold));
        }
    }
}
