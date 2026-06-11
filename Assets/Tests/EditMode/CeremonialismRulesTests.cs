using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 制度の儀礼性（VEBL-4 #1603）の純ロジックテスト。儀礼的価値・機能不全でも存続・廃止抵抗・
    /// 形骸化ドリフト・空虚な制度判定を既定 Params の具体値で固定する。
    /// </summary>
    public class CeremonialismRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>儀礼的価値＝威信×伝統の加重ブレンド（威信重み0.6）。</summary>
        [Test]
        public void CeremonialValue_BlendsPrestigeAndTradition()
        {
            // 0.6*0.8 + 0.4*0.5 = 0.68
            Assert.AreEqual(0.68f, CeremonialismRules.CeremonialValue(0.8f, 0.5f), Eps);
        }

        /// <summary>機能ゼロでも儀礼的価値が高ければ存続力が残る＝儀礼性だけで生き延びる。</summary>
        [Test]
        public void SurvivalDespiteDysfunction_SurvivesWithZeroFunction()
        {
            // 0.5*0 + 0.5*0.9 = 0.45（機能ゼロでも残る）
            float survival = CeremonialismRules.SurvivalDespiteDysfunction(0f, 0.9f);
            Assert.AreEqual(0.45f, survival, Eps);
            Assert.Greater(survival, 0f, "機能ゼロでも儀礼性で存続力が残る");
        }

        /// <summary>廃止抵抗＝儀礼的威信＋既得権が盾（既得権重み0.4）。</summary>
        [Test]
        public void AbolitionResistance_ShieldedByPrestigeAndVested()
        {
            // 0.6*0.8 + 0.4*0.6 = 0.72
            Assert.AreEqual(0.72f, CeremonialismRules.AbolitionResistance(0.8f, 0.6f), Eps);
        }

        /// <summary>形骸化ドリフト＝儀礼性が高いほど機能が形式に置き換わり目減りする。</summary>
        [Test]
        public void CeremonialDrift_ErodesFunctionTowardForm()
        {
            // 0.8*(1 - 0.1*0.9*1.0) = 0.728
            float after = CeremonialismRules.CeremonialDrift(0.8f, 0.9f, 1.0f);
            Assert.AreEqual(0.728f, after, Eps);
            Assert.Less(after, 0.8f, "機能が形式へドリフトして目減りする");
        }

        /// <summary>儀礼維持の空費＝儀礼的価値×単価（50）。</summary>
        [Test]
        public void PrestigeUpkeepCost_ScalesWithCeremonialValue()
        {
            // 0.7*50 = 35
            Assert.AreEqual(35f, CeremonialismRules.PrestigeUpkeepCost(0.7f), Eps);
        }

        /// <summary>空虚な制度＝機能が閾値未満で儀礼的価値が機能を上回る殻。</summary>
        [Test]
        public void IsHollowInstitution_DetectsEmptyShell()
        {
            Assert.IsTrue(CeremonialismRules.IsHollowInstitution(0.1f, 0.7f), "機能0.1<閾値0.2かつ儀礼0.7>機能＝空虚");
            Assert.IsFalse(CeremonialismRules.IsHollowInstitution(0.5f, 0.7f), "機能0.5は閾値以上＝形骸化していない");
        }

        /// <summary>改革難度＝抵抗が強く政治資本が乏しいほど高い（抵抗0は0＝タダ）。</summary>
        [Test]
        public void ReformDifficulty_RisesWithResistanceFallsWithCapital()
        {
            // need=0.72*1.5=1.08; 1.08/(0.3+1.08)=0.78260…
            Assert.AreEqual(1.08f / 1.38f, CeremonialismRules.ReformDifficulty(0.72f, 0.3f), Eps);
            // 抵抗なし＝改革はタダ
            Assert.AreEqual(0f, CeremonialismRules.ReformDifficulty(0f, 0.5f), Eps);
            // 政治資本が増えれば改革は易しくなる
            Assert.Less(
                CeremonialismRules.ReformDifficulty(0.72f, 0.9f),
                CeremonialismRules.ReformDifficulty(0.72f, 0.3f),
                "政治資本が厚いほど改革は易しい");
        }

        /// <summary>放置で機能が衰える（手入れ neglect=0 なら不変）。</summary>
        [Test]
        public void FunctionalDecayTick_DecaysUnderNeglect()
        {
            // 0.8*(1 - 0.2*1.0*1.0) = 0.64
            Assert.AreEqual(0.64f, CeremonialismRules.FunctionalDecayTick(0.8f, 1.0f, 1.0f), Eps);
            // 手入れ済み＝衰えない
            Assert.AreEqual(0.8f, CeremonialismRules.FunctionalDecayTick(0.8f, 0f, 1.0f), Eps);
        }
    }
}
