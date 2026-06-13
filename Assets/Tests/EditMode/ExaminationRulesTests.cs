using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 考課制度（試験・記録）の純ロジック（PANO-3 #1509）の EditMode テスト。
    /// 記録の蓄積・可視化・昇進反映・反乱予兆検出・規範化・能力主義の透明性・考課疲れ・ドシエ国家判定を担保。
    /// 既定 Params の具体値で期待値を固定する。
    /// </summary>
    public class ExaminationRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>定期的な考課（高頻度）は記録を蓄積し、無考課は記録を陳腐化させる＝記録が個人をケース化する。</summary>
        [Test]
        public void RecordTick_考課で蓄積し無考課で陳腐化する()
        {
            // 高頻度の考課：深度0.2、頻度1、dt=1 → 0.2 + 0.5*1*(1-0.2)*1 = 0.6
            float grown = ExaminationRules.RecordTick(0.2f, 1f, 1f);
            Assert.AreEqual(0.6f, grown, Eps);

            // 無考課：深度0.6、頻度0、dt=1 → 0.6 - 0.1*(1-0)*0.6*1 = 0.54
            float decayed = ExaminationRules.RecordTick(0.6f, 0f, 1f);
            Assert.AreEqual(0.54f, decayed, Eps);

            // 蓄積は陳腐化より速い＝定期考課が記録を押し上げる方向。
            Assert.Greater(grown, 0.2f);
            Assert.Less(decayed, 0.6f);
        }

        /// <summary>記録が深いほど個人が可視化される＝逓増（薄い記録ではぼやけ、深い記録で輪郭が立つ）。</summary>
        [Test]
        public void Visibility_記録深度に逓増で対応する()
        {
            Assert.AreEqual(0f, ExaminationRules.Visibility(0f), Eps);
            Assert.AreEqual(1f, ExaminationRules.Visibility(1f), Eps);
            // 0.5 → 0.5*(0.5+0.5*0.5) = 0.375（線形未満＝逓増）。
            float mid = ExaminationRules.Visibility(0.5f);
            Assert.AreEqual(0.375f, mid, Eps);
            Assert.Less(mid, 0.5f);
        }

        /// <summary>考課点と記録の一貫性が昇進に反映される＝記録が序列を決める（考課0.6:一貫性0.4）。</summary>
        [Test]
        public void PromotionReflection_考課点と一貫性の加重で昇進に効く()
        {
            // 考課0.8、一貫性0.5 → 0.6*0.8 + 0.4*0.5 = 0.68
            Assert.AreEqual(0.68f, ExaminationRules.PromotionReflection(0.8f, 0.5f), Eps);

            // 一貫性が裏打ち：同じ高得点でも一貫性が高いほど昇進反映が高い。
            float consistent = ExaminationRules.PromotionReflection(0.8f, 1f);
            float spiky = ExaminationRules.PromotionReflection(0.8f, 0f);
            Assert.Greater(consistent, spiky);
        }

        /// <summary>蓄積された記録ほど反乱の予兆検出精度が上がる（記録0.6:統合0.4）＝MutinyRules の前段。</summary>
        [Test]
        public void RebellionPrecursorDetection_記録の蓄積で予兆が見える()
        {
            // 記録0.9、統合0.5 → 0.6*0.9 + 0.4*0.5 = 0.74
            Assert.AreEqual(0.74f, ExaminationRules.RebellionPrecursorDetection(0.9f, 0.5f), Eps);

            // 記録が深いほど（統合一定）検出精度が上がる＝普段の基準があるから逸脱が分かる。
            float deep = ExaminationRules.RebellionPrecursorDetection(0.9f, 0.5f);
            float shallow = ExaminationRules.RebellionPrecursorDetection(0.1f, 0.5f);
            Assert.Greater(deep, shallow);
        }

        /// <summary>規範からの偏差で個人を序列化＝規範超えは正常側（高位）・下回りは逸脱側（低位）、境界は0.5。</summary>
        [Test]
        public void Normalization_規範からの偏差で正常逸脱を線引きする()
        {
            // 点=規範 → 偏差0 → 0.5（境界＝平均的＝中位）。
            Assert.AreEqual(0.5f, ExaminationRules.Normalization(0.5f, 0.5f), Eps);

            // 規範超え：点0.6、規範0.5、偏差0.1、鋭さ4 → 0.5 + 0.5*4*0.1 = 0.7（正常・高位）。
            Assert.AreEqual(0.7f, ExaminationRules.Normalization(0.6f, 0.5f), Eps);

            // 規範下回り：点0.4、規範0.5 → 0.5 + 0.5*4*(-0.1) = 0.3（逸脱・低位）。
            Assert.AreEqual(0.3f, ExaminationRules.Normalization(0.4f, 0.5f), Eps);
        }

        /// <summary>記録に基づく評価は能力主義の透明性を上げる＝記録の深さ×公正さの積（情実排除の正の面）。</summary>
        [Test]
        public void MeritocraticTransparency_記録と公正さの積で透明になる()
        {
            // 記録0.8、公正0.5 → 0.4
            Assert.AreEqual(0.4f, ExaminationRules.MeritocraticTransparency(0.8f, 0.5f), Eps);

            // どちらか欠ければ透明性は出ない＝積（記録ゼロ or 公正ゼロで0）。
            Assert.AreEqual(0f, ExaminationRules.MeritocraticTransparency(0f, 1f), Eps);
            Assert.AreEqual(0f, ExaminationRules.MeritocraticTransparency(1f, 0f), Eps);
        }

        /// <summary>過度な考課（閾値0.6超）は疲労を蓄積し、適度な考課は疲労を生まない＝評価漬けの息苦しさ。</summary>
        [Test]
        public void SurveillanceFatigue_過度な考課のみ疲労を生む()
        {
            // 頻度0.6以下は閾値以下＝疲労ゼロ（適度な考課は無害）。
            Assert.AreEqual(0.1f, ExaminationRules.SurveillanceFatigue(0.1f, 0.6f, 1f), Eps);

            // 過度：頻度1、超過分0.4、疲労0.1 + 0.3*0.4*1 = 0.22
            Assert.AreEqual(0.22f, ExaminationRules.SurveillanceFatigue(0.1f, 1f, 1f), Eps);
        }

        /// <summary>記録深度が閾値以上ならドシエ国家（全員が記録で把握される考課国家）＝規律社会の到達点。</summary>
        [Test]
        public void IsDossierState_閾値以上で考課国家と判定する()
        {
            // 既定閾値0.8。
            Assert.IsTrue(ExaminationRules.IsDossierState(0.85f));
            Assert.IsTrue(ExaminationRules.IsDossierState(0.8f));   // 境界含む
            Assert.IsFalse(ExaminationRules.IsDossierState(0.7f));

            // 明示閾値版。
            Assert.IsTrue(ExaminationRules.IsDossierState(0.5f, 0.5f));
            Assert.IsFalse(ExaminationRules.IsDossierState(0.49f, 0.5f));
        }
    }
}
