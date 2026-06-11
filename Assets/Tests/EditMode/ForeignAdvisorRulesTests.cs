using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>外国顧問・軍事援助（坂の上の雲型近代化・#1435）の純ロジック検証。</summary>
    public class ForeignAdvisorRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>招請可能度＝同盟の強さ×0.6＋受入国の魅力×0.4（同盟・援助条件が前提）。</summary>
        [Test]
        public void AdvisorAvailability_同盟と魅力の加重和()
        {
            // 同盟1×0.6 ＋ 魅力1×0.4 ＝ 1.0
            Assert.AreEqual(1f, ForeignAdvisorRules.AdvisorAvailability(1f, 1f), Eps);
            // 同盟0.5×0.6 ＋ 魅力0×0.4 ＝ 0.3
            Assert.AreEqual(0.3f, ForeignAdvisorRules.AdvisorAvailability(0.5f, 0f), Eps);
            // 同盟が無ければ魅力だけ＝0.4
            Assert.AreEqual(0.4f, ForeignAdvisorRules.AdvisorAvailability(0f, 1f), Eps);
        }

        /// <summary>研究加速は専門知識×吸収能力で増え、吸収0でも下限0.2ぶんは効く（学ぶ側の下地）。</summary>
        [Test]
        public void ResearchAcceleration_専門知識と吸収で加速()
        {
            // expertise1・吸収1 ＝ 1 + 1×1×1 ＝ 2.0倍
            Assert.AreEqual(2f, ForeignAdvisorRules.ResearchAcceleration(1f, 1f), Eps);
            // 吸収0でも下限0.2 ＝ 1 + 1×1×0.2 ＝ 1.2倍
            Assert.AreEqual(1.2f, ForeignAdvisorRules.ResearchAcceleration(1f, 0f), Eps);
            // 顧問なし（expertise0）＝等倍
            Assert.AreEqual(1f, ForeignAdvisorRules.ResearchAcceleration(0f, 1f), Eps);
        }

        /// <summary>人材育成は専門知識×育成対象×速度×dtで蓄積（育成対象が多いほど速い）。</summary>
        [Test]
        public void TalentDevelopment_専門知識と人数で育つ()
        {
            // expertise1・trainees1・dt1 ＝ 0.1×1×1×1 ＝ 0.1
            Assert.AreEqual(0.1f, ForeignAdvisorRules.TalentDevelopment(1f, 1f, 1f), Eps);
            // 育成対象0なら育たない
            Assert.AreEqual(0f, ForeignAdvisorRules.TalentDevelopment(1f, 0f, 1f), Eps);
            // 半分の規模で半分
            Assert.AreEqual(0.05f, ForeignAdvisorRules.TalentDevelopment(1f, 0.5f, 1f), Eps);
        }

        /// <summary>知識移転は意欲（同盟関係）と専門知識で進み、伸び代(1−移転)で逓減する。</summary>
        [Test]
        public void KnowledgeTransferTick_意欲と専門で移転が進む()
        {
            // 移転0・expertise1・意欲1・dt1 ＝ 0 + 0.05×1×1×1×1 ＝ 0.05
            Assert.AreEqual(0.05f, ForeignAdvisorRules.KnowledgeTransferTick(0f, 1f, 1f, 1f), Eps);
            // 教える意欲0なら移転は止まる（同盟が冷えれば教えない）
            Assert.AreEqual(0.5f, ForeignAdvisorRules.KnowledgeTransferTick(0.5f, 1f, 0f, 1f), Eps);
            // 移転が深いほど伸び代が小さい：0.8から ＝ 0.8 + 0.05×0.2 ＝ 0.81
            Assert.AreEqual(0.81f, ForeignAdvisorRules.KnowledgeTransferTick(0.8f, 1f, 1f, 1f), Eps);
        }

        /// <summary>依存リスクは自立度・移転が低いほど高く、根付けば消える。</summary>
        [Test]
        public void DependencyRisk_自立も移転も無いほど高い()
        {
            // 自立0・移転0 ＝ (1−0)×(1−0)×0.6 ＝ 0.6（上限）
            Assert.AreEqual(0.6f, ForeignAdvisorRules.DependencyRisk(0f, 0f), Eps);
            // 移転が完了すればリスク0（技術が根付く）
            Assert.AreEqual(0f, ForeignAdvisorRules.DependencyRisk(1f, 0f), Eps);
            // 自前の自給力があってもリスク0
            Assert.AreEqual(0f, ForeignAdvisorRules.DependencyRisk(0f, 1f), Eps);
        }

        /// <summary>自立への移行は移転×現地能力＝どちらか欠ければ立てない。</summary>
        [Test]
        public void IndependenceTransition_移転と現地能力の積()
        {
            // 移転1・現地能力1 ＝ 1.0（近代化の完成）
            Assert.AreEqual(1f, ForeignAdvisorRules.IndependenceTransition(1f, 1f), Eps);
            // 知識があっても運用する人がいなければ0
            Assert.AreEqual(0f, ForeignAdvisorRules.IndependenceTransition(1f, 0f), Eps);
            // 0.8×0.5 ＝ 0.4
            Assert.AreEqual(0.4f, ForeignAdvisorRules.IndependenceTransition(0.8f, 0.5f), Eps);
        }

        /// <summary>顧問の急引き上げは依存度に比例して痛み、円満なら無傷。</summary>
        [Test]
        public void AdvisorWithdrawalShock_急引き上げは依存ほど痛い()
        {
            // 依存1・急引き上げ ＝ 1×0.5 ＝ 0.5
            Assert.AreEqual(0.5f, ForeignAdvisorRules.AdvisorWithdrawalShock(1f, true), Eps);
            // 円満な引き継ぎ＝打撃なし
            Assert.AreEqual(0f, ForeignAdvisorRules.AdvisorWithdrawalShock(1f, false), Eps);
            // 依存0.4・急引き上げ ＝ 0.4×0.5 ＝ 0.2
            Assert.AreEqual(0.2f, ForeignAdvisorRules.AdvisorWithdrawalShock(0.4f, true), Eps);
        }

        /// <summary>自立判定＝移転がしきい値（既定0.7）以上で外国顧問を不要にする。</summary>
        [Test]
        public void IsSelfSufficient_しきい値で自立判定()
        {
            Assert.IsTrue(ForeignAdvisorRules.IsSelfSufficient(0.7f));
            Assert.IsTrue(ForeignAdvisorRules.IsSelfSufficient(0.9f));
            Assert.IsFalse(ForeignAdvisorRules.IsSelfSufficient(0.69f));
            // 明示しきい値
            Assert.IsTrue(ForeignAdvisorRules.IsSelfSufficient(0.5f, 0.5f));
        }

        /// <summary>ForeignAdvisor 構築時に各フィールドが 0..1 へクランプされる。</summary>
        [Test]
        public void ForeignAdvisor_構築でクランプ()
        {
            var a = new ForeignAdvisor(1.5f, -0.2f, 2f);
            Assert.AreEqual(1f, a.expertise, Eps);
            Assert.AreEqual(0f, a.willingnessToTeach, Eps);
            Assert.AreEqual(1f, a.knowledgeTransferred, Eps);
        }
    }
}
