using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>作戦ドクトリン純ロジック（#1388）の EditMode テスト。既定 Params 具体値で期待値固定。</summary>
    public class OperationalDoctrineRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>戦役効率＝運営×0.6＋情報×0.4の加重平均（既定）。</summary>
        [Test]
        public void CampaignEfficiency_運営寄りの加重平均()
        {
            // operation=1, intelligence=0 → 0.6
            Assert.AreEqual(0.6f, OperationalDoctrineRules.CampaignEfficiency(1f, 0f), Eps);
            // operation=0, intelligence=1 → 0.4
            Assert.AreEqual(0.4f, OperationalDoctrineRules.CampaignEfficiency(0f, 1f), Eps);
            // 双方0.5 → 0.5
            Assert.AreEqual(0.5f, OperationalDoctrineRules.CampaignEfficiency(0.5f, 0.5f), Eps);
        }

        /// <summary>兵站の采配＝複雑さ0は能力なりに満額、複雑さ高では能力依存。</summary>
        [Test]
        public void LogisticsOrchestration_複雑な兵站は運営能力が物を言う()
        {
            // 複雑さ0 → 能力に関わらず満額(1.0)。
            Assert.AreEqual(1f, OperationalDoctrineRules.LogisticsOrchestration(0.4f, 0f), Eps);
            // 能力1 → 複雑でも満額。
            Assert.AreEqual(1f, OperationalDoctrineRules.LogisticsOrchestration(1f, 1f), Eps);
            // 運営0.5・複雑さ1 → denom=0.5+0.5*1=1.0, ratio=0.5/1.0=0.5。
            Assert.AreEqual(0.5f, OperationalDoctrineRules.LogisticsOrchestration(0.5f, 1f), Eps);
            // 能力0 → 何も捌けない。
            Assert.AreEqual(0f, OperationalDoctrineRules.LogisticsOrchestration(0f, 0.5f), Eps);
        }

        /// <summary>情報の活用＝情報能力×生情報（既定上限1.0）。</summary>
        [Test]
        public void IntelligenceExploitation_生情報を能力で活かす()
        {
            // 情報能力0.5・生情報0.8 → 0.4。
            Assert.AreEqual(0.4f, OperationalDoctrineRules.IntelligenceExploitation(0.5f, 0.8f), Eps);
            // 情報能力0 → 生情報があっても宝の持ち腐れ。
            Assert.AreEqual(0f, OperationalDoctrineRules.IntelligenceExploitation(0f, 1f), Eps);
            // 生情報0 → 活かす元が無い。
            Assert.AreEqual(0f, OperationalDoctrineRules.IntelligenceExploitation(1f, 0f), Eps);
        }

        /// <summary>協調スコア＝1＋運営×部隊数×0.5（既定）。多部隊ほど運営が連携を高める。</summary>
        [Test]
        public void CoordinationScore_多部隊の連携を運営能力が高める()
        {
            // 単一部隊(count=0) → 協調の出番なし=1.0。
            Assert.AreEqual(1f, OperationalDoctrineRules.CoordinationScore(1f, 0f), Eps);
            // 運営1・部隊数1 → 1+1*1*0.5=1.5（満額ボーナス）。
            Assert.AreEqual(1.5f, OperationalDoctrineRules.CoordinationScore(1f, 1f), Eps);
            // 運営0.5・部隊数1 → 1+0.5*1*0.5=1.25。
            Assert.AreEqual(1.25f, OperationalDoctrineRules.CoordinationScore(0.5f, 1f), Eps);
        }

        /// <summary>ドクトリンの完成度＝戦役効率×方針の一貫性。ぶれた方針では完成しない。</summary>
        [Test]
        public void DoctrineQuality_能力と一貫性の積()
        {
            // 効率0.8・一貫性1.0 → 0.8。
            Assert.AreEqual(0.8f, OperationalDoctrineRules.DoctrineQuality(0.8f, 1f), Eps);
            // 効率0.8・一貫性0.5 → 0.4（方針がぶれると半減）。
            Assert.AreEqual(0.4f, OperationalDoctrineRules.DoctrineQuality(0.8f, 0.5f), Eps);
            // 一貫性0 → 完成度0。
            Assert.AreEqual(0f, OperationalDoctrineRules.DoctrineQuality(1f, 0f), Eps);
        }

        /// <summary>計画の射程＝運営と情報の幾何平均×10（既定）。片方欠けると伸びない。</summary>
        [Test]
        public void PlanningHorizon_運営と情報の両立で先を読む()
        {
            // 双方1 → sqrt(1)=1 × 10 = 10。
            Assert.AreEqual(10f, OperationalDoctrineRules.PlanningHorizon(1f, 1f), Eps);
            // 運営0.25・情報1 → sqrt(0.25)=0.5 × 10 = 5。
            Assert.AreEqual(5f, OperationalDoctrineRules.PlanningHorizon(0.25f, 1f), Eps);
            // 情報0 → 兵站だけでは長期計画は立たない=0。
            Assert.AreEqual(0f, OperationalDoctrineRules.PlanningHorizon(1f, 0f), Eps);
        }

        /// <summary>作戦のテンポ＝協調の上乗せ分(正規化)×情報活用。OODA的な速さ。</summary>
        [Test]
        public void OperationalTempo_協調と情報活用で循環が速く回る()
        {
            // 協調1.5(上乗せ0.5=満額)・情報活用1.0 → 1.0。
            Assert.AreEqual(1f, OperationalDoctrineRules.OperationalTempo(1.5f, 1f), Eps);
            // 協調1.5・情報活用0.5 → 1.0*0.5=0.5。
            Assert.AreEqual(0.5f, OperationalDoctrineRules.OperationalTempo(1.5f, 0.5f), Eps);
            // 協調1.0(上乗せ無し) → テンポ0。
            Assert.AreEqual(0f, OperationalDoctrineRules.OperationalTempo(1f, 1f), Eps);
            // 協調1.25(上乗せ0.25=半分)・情報活用1.0 → 0.5。
            Assert.AreEqual(0.5f, OperationalDoctrineRules.OperationalTempo(1.25f, 1f), Eps);
        }

        /// <summary>優れた幕僚仕事の判定＝戦役効率と協調(正規化)が双方とも閾値超え。</summary>
        [Test]
        public void IsCompetentStaffWork_効率と協調の双方が閾値を超える()
        {
            // 効率0.8・協調1.5(正規化1.0)・閾値0.7 → 双方超え=true。
            Assert.IsTrue(OperationalDoctrineRules.IsCompetentStaffWork(0.8f, 1.5f, 0.7f));
            // 効率は高いが協調が弱い(1.25→正規化0.5<0.7) → false。
            Assert.IsFalse(OperationalDoctrineRules.IsCompetentStaffWork(0.8f, 1.25f, 0.7f));
            // 協調は満点だが効率が低い(0.5<0.7) → false。
            Assert.IsFalse(OperationalDoctrineRules.IsCompetentStaffWork(0.5f, 1.5f, 0.7f));
        }

        /// <summary>全入力クランプ＝範囲外でも破綻しない。</summary>
        [Test]
        public void 入力クランプ_範囲外でも安全()
        {
            // 負の能力・1超の生情報。
            Assert.AreEqual(0f, OperationalDoctrineRules.CampaignEfficiency(-1f, -1f), Eps);
            Assert.AreEqual(1f, OperationalDoctrineRules.CampaignEfficiency(2f, 2f), Eps);
            Assert.AreEqual(1f, OperationalDoctrineRules.IntelligenceExploitation(2f, 2f), Eps);
            Assert.AreEqual(0f, OperationalDoctrineRules.DoctrineQuality(-1f, 2f), Eps);
        }
    }
}
