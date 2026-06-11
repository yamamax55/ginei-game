using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 市民宗教（religion civile）の純ロジック（ROUS-4 #1468・ルソー）の EditMode テスト。
    /// 政治的結束・信仰の製造・形骸化のドリフト・空疎な信仰・正統性の神聖化・急速な崩壊・
    /// 寛容の要件・市民宗教の活力判定を既定 Params の具体値で固定する。
    /// </summary>
    public class CivicFaithRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>政治的結束＝信奉×共有信条の積。どちらか0なら結束も0。</summary>
        [Test]
        public void CivicCohesion_信奉と共有信条の積()
        {
            Assert.AreEqual(0.4f, CivicFaithRules.CivicCohesion(0.8f, 0.5f), Eps);
            // 信条が共有されていなければ信奉が高くても結束は生まれない
            Assert.AreEqual(0f, CivicFaithRules.CivicCohesion(1f, 0f), Eps);
        }

        /// <summary>政府の製造は信奉を製造天井(0.85×推進)へ dt 比例で押し上げる（上からの信仰）。</summary>
        [Test]
        public void ManufacturedFaithTick_推進で信奉が天井へ寄る()
        {
            // target = 0.85×1 = 0.85、移動量 = 0.04×1×1 = 0.04 → 0.5 + 0.04 = 0.54
            float d = CivicFaithRules.ManufacturedFaithTick(0.5f, 1f, 1f);
            Assert.AreEqual(0.54f, d, Eps);
            // 既に天井(推進0.5→target0.425)を超える信奉は押し上げない＝据え置き
            Assert.AreEqual(0.6f, CivicFaithRules.ManufacturedFaithTick(0.6f, 0.5f, 1f), Eps);
        }

        /// <summary>形骸化＝儀礼が盛んなほど真摯さが速く0へ薄れる（信じてないが形だけ）。</summary>
        [Test]
        public void RitualizationDrift_儀礼が盛んなほど真摯さが薄れる()
        {
            // drift = 0.02×1.0×1 = 0.02 → 0.7 - 0.02 = 0.68
            Assert.AreEqual(0.68f, CivicFaithRules.RitualizationDrift(0.7f, 1f, 1f), Eps);
            // 儀礼活力0なら形骸化は進まない
            Assert.AreEqual(0.7f, CivicFaithRules.RitualizationDrift(0.7f, 0f, 1f), Eps);
        }

        /// <summary>空疎な信仰＝儀礼活力×(1−真摯さ)。CeremonyRules.IsHollow と整合の方向。</summary>
        [Test]
        public void HollowFaith_儀礼盛ん内面空っぽで高い()
        {
            // 0.9×(1-0.2) = 0.72＝盛んな儀礼・薄い内面＝空疎
            Assert.AreEqual(0.72f, CivicFaithRules.HollowFaith(0.9f, 0.2f), Eps);
            // 真摯なら空疎でない
            Assert.AreEqual(0f, CivicFaithRules.HollowFaith(0.9f, 1f), Eps);
        }

        /// <summary>正統性の神聖化＝結束×法の神聖視×scale(0.25)。法と社会契約を聖化する。</summary>
        [Test]
        public void LegitimacySanctification_結束と法の神聖視で正統性が上がる()
        {
            // 0.8×0.5×0.25 = 0.1
            Assert.AreEqual(0.1f, CivicFaithRules.LegitimacySanctification(0.8f, 0.5f), Eps);
            // 法が神聖視されなければ神聖化は0
            Assert.AreEqual(0f, CivicFaithRules.LegitimacySanctification(0.8f, 0f), Eps);
        }

        /// <summary>形骸化した信仰は衝撃で急速崩壊するが、真摯な信仰は耐える（製造された信仰の脆さ）。</summary>
        [Test]
        public void SuddenCollapse_空疎な信仰だけ衝撃で崩れる()
        {
            // 空疎0.7≥閾値0.5 → 0.7×0.8×0.6 = 0.336 が剥がれる
            Assert.AreEqual(0.336f, CivicFaithRules.SuddenCollapse(0.7f, 0.8f, 0.5f), Eps);
            // 空疎0.3<閾値0.5＝真摯な信仰は衝撃に耐える＝崩壊0
            Assert.AreEqual(0f, CivicFaithRules.SuddenCollapse(0.3f, 0.8f, 0.5f), Eps);
        }

        /// <summary>寛容の要件＝市民宗教×(1−教条主義)。教条が強いと市民宗教が狂信へ堕す。</summary>
        [Test]
        public void ToleranceRequirement_教条主義が市民宗教を狂信へ堕す()
        {
            // 0.8×(1-0.25) = 0.6
            Assert.AreEqual(0.6f, CivicFaithRules.ToleranceRequirement(0.8f, 0.25f), Eps);
            // 完全な不寛容は寛容性を0に＝健全でない
            Assert.AreEqual(0f, CivicFaithRules.ToleranceRequirement(1f, 1f), Eps);
        }

        /// <summary>市民宗教の活力＝信奉と真摯さの両方が閾値以上のときだけ生きている。</summary>
        [Test]
        public void IsCivicReligionVital_内面が空疎なら生きていない()
        {
            Assert.IsTrue(CivicFaithRules.IsCivicReligionVital(0.6f, 0.5f, 0.4f));
            // 信奉が高くても真摯さが閾値未満＝形骸化＝結束を生まない
            Assert.IsFalse(CivicFaithRules.IsCivicReligionVital(0.9f, 0.3f, 0.4f));
        }
    }
}
