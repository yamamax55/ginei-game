using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政体形態と進化（#117）の純ロジックを固定する：形態↔軸の合成（分類の往復）／既存軸への橋渡し／
    /// 進化グラフ（首長制→民主/独裁→下位形態）の合法遷移と発火条件／NextForm の分岐（革命/独裁化/民主化）。
    /// </summary>
    public class GovernmentFormRulesTests
    {
        private static readonly GovernmentForm[] AllForms =
        {
            GovernmentForm.首長制, GovernmentForm.君主制, GovernmentForm.立憲君主制,
            GovernmentForm.共和制, GovernmentForm.共産主義, GovernmentForm.指導者独裁
        };

        // ===== 形態↔軸（合成ビューの往復） =====

        [Test]
        public void Classify_RoundTripsEachFormAxes()
        {
            foreach (var f in AllForms)
                Assert.AreEqual(f, GovernmentFormRules.Classify(GovernmentFormRules.Axes(f)), $"{f} の軸→分類が往復しない");
        }

        [Test]
        public void Classify_StateOwnership_IsCommunism()
        {
            // 国有経済＝共産（君主有無・立憲に関わらず）
            Assert.AreEqual(GovernmentForm.共産主義,
                GovernmentFormRules.Classify(false, true, true, Ownership.国有, CivilianControlType.党軍));
        }

        [Test]
        public void Classify_Democracy_SplitsBySovereign()
        {
            Assert.AreEqual(GovernmentForm.立憲君主制,
                GovernmentFormRules.Classify(true, true, true, Ownership.私有, CivilianControlType.文民統制));
            Assert.AreEqual(GovernmentForm.共和制,
                GovernmentFormRules.Classify(false, true, true, Ownership.私有, CivilianControlType.文民統制));
        }

        [Test]
        public void Bridges_ToExistingAxes()
        {
            Assert.AreEqual(CivilianControlType.党軍, GovernmentFormRules.ControlTypeOf(GovernmentForm.共産主義));
            Assert.AreEqual(CivilianControlType.未分化, GovernmentFormRules.ControlTypeOf(GovernmentForm.首長制));
            Assert.AreEqual(CivilianControlType.文民統制, GovernmentFormRules.ControlTypeOf(GovernmentForm.共和制));
            Assert.AreEqual(Ownership.国有, GovernmentFormRules.OwnershipOf(GovernmentForm.共産主義));
            Assert.AreEqual(Ownership.私有, GovernmentFormRules.OwnershipOf(GovernmentForm.立憲君主制));
            Assert.IsTrue(GovernmentFormRules.IsDemocratic(GovernmentForm.立憲君主制));
            Assert.IsTrue(GovernmentFormRules.IsDemocratic(GovernmentForm.共和制));
            Assert.IsTrue(GovernmentFormRules.IsAutocratic(GovernmentForm.共産主義));
            Assert.IsTrue(GovernmentFormRules.IsAutocratic(GovernmentForm.指導者独裁));
            Assert.IsFalse(GovernmentFormRules.IsDemocratic(GovernmentForm.君主制));
        }

        // ===== 進化グラフ（合法遷移） =====

        [Test]
        public void CanTransition_FollowsTheGraph()
        {
            Assert.IsTrue(GovernmentFormRules.CanTransition(GovernmentForm.首長制, GovernmentForm.君主制));
            Assert.IsTrue(GovernmentFormRules.CanTransition(GovernmentForm.君主制, GovernmentForm.立憲君主制));
            Assert.IsTrue(GovernmentFormRules.CanTransition(GovernmentForm.立憲君主制, GovernmentForm.共和制));
            Assert.IsTrue(GovernmentFormRules.CanTransition(GovernmentForm.君主制, GovernmentForm.共産主義));
            // 非隣接・自己
            Assert.IsFalse(GovernmentFormRules.CanTransition(GovernmentForm.首長制, GovernmentForm.共和制));
            Assert.IsFalse(GovernmentFormRules.CanTransition(GovernmentForm.共和制, GovernmentForm.共和制));
        }

        // ===== 発火条件 =====

        [Test]
        public void Trigger_Democratization_NeedsLegitAndInclusiveness()
        {
            var good = new RegimeSignals(0.7f, 0.2f, 0.7f, 0.7f, 0.7f);
            var bad = new RegimeSignals(0.4f, 0.2f, 0.7f, 0.7f, 0.5f);
            Assert.IsTrue(GovernmentFormRules.TransitionTrigger(GovernmentForm.君主制, GovernmentForm.立憲君主制, good));
            Assert.IsFalse(GovernmentFormRules.TransitionTrigger(GovernmentForm.君主制, GovernmentForm.立憲君主制, bad));
        }

        [Test]
        public void Trigger_Revolution_NeedsCollapseAndExtraction()
        {
            var crisis = new RegimeSignals(0.2f, 0.5f, 0.4f, 0.25f, 0.3f); // 正統性喪失×絶望×収奪
            Assert.IsTrue(GovernmentFormRules.TransitionTrigger(GovernmentForm.君主制, GovernmentForm.共産主義, crisis));
            var stable = new RegimeSignals(0.7f, 0.2f, 0.7f, 0.7f, 0.6f);
            Assert.IsFalse(GovernmentFormRules.TransitionTrigger(GovernmentForm.君主制, GovernmentForm.共産主義, stable));
        }

        [Test]
        public void Trigger_RejectsIllegalTransition()
        {
            // 首長制→共和制 は非隣接＝条件以前に false
            var any = new RegimeSignals(0.9f, 0f, 0.9f, 0.9f, 0.9f);
            Assert.IsFalse(GovernmentFormRules.TransitionTrigger(GovernmentForm.首長制, GovernmentForm.共和制, any));
        }

        // ===== NextForm（年次の分岐） =====

        [Test]
        public void NextForm_CrisisLeadsToRevolution()
        {
            var crisis = new RegimeSignals(0.2f, 0.4f, 0.5f, 0.2f, 0.3f);
            Assert.AreEqual(GovernmentForm.共産主義, GovernmentFormRules.NextForm(GovernmentForm.君主制, crisis));
        }

        [Test]
        public void NextForm_BreakdownOfConsentLeadsToStrongmanRule()
        {
            var unrest = new RegimeSignals(0.6f, 0.6f, 0.3f, 0.5f, 0.5f); // 合意崩壊×腐敗（革命条件は不成立）
            Assert.AreEqual(GovernmentForm.指導者独裁, GovernmentFormRules.NextForm(GovernmentForm.君主制, unrest));
        }

        [Test]
        public void NextForm_VirtuousInclusiveLeadsToConstitutionalMonarchy()
        {
            var good = new RegimeSignals(0.7f, 0.2f, 0.7f, 0.7f, 0.7f);
            Assert.AreEqual(GovernmentForm.立憲君主制, GovernmentFormRules.NextForm(GovernmentForm.君主制, good));
        }

        [Test]
        public void NextForm_ChiefdomMaturesToMonarchy()
        {
            var settled = new RegimeSignals(0.6f, 0.2f, 0.7f, 0.7f, 0.5f);
            Assert.AreEqual(GovernmentForm.君主制, GovernmentFormRules.NextForm(GovernmentForm.首長制, settled));
        }

        [Test]
        public void NextForm_StableMediocre_StaysPut()
        {
            var meh = new RegimeSignals(0.45f, 0.3f, 0.5f, 0.5f, 0.5f); // どの遷移条件も不成立
            Assert.AreEqual(GovernmentForm.君主制, GovernmentFormRules.NextForm(GovernmentForm.君主制, meh));
        }

        // ===== Apply / SignalsOf =====

        [Test]
        public void Apply_SetsFactionStateForm()
        {
            var s = new FactionState();
            Assert.AreEqual(GovernmentForm.首長制, s.governmentForm); // 既定＝首長制スタート
            GovernmentFormRules.Apply(s, GovernmentForm.共和制);
            Assert.AreEqual(GovernmentForm.共和制, s.governmentForm);
            GovernmentFormRules.Apply(null, GovernmentForm.君主制); // null 安全
        }

        [Test]
        public void SignalsOf_NullSafe()
        {
            var s = GovernmentFormRules.SignalsOf(null);
            Assert.AreEqual(1f, s.legitimacy, 1e-3f);
            Assert.AreEqual(0.5f, s.inclusiveness, 1e-3f);
        }
    }
}
