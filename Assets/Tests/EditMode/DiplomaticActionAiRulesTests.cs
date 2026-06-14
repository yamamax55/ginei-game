using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// <see cref="DiplomaticActionAiRules"/> の EditMode テスト＝外交アクション発令判断（制裁／援助／賠償）。
    /// 判断は閾値ベース・決定論、効果額は既存窓口（DiplomaticEffectRules/ReparationsRules）へ委譲。
    /// </summary>
    public class DiplomaticActionAiRulesTests
    {
        // ===== 制裁 =====

        [Test]
        public void ShouldSanction_HostileAndSuperior_True()
        {
            // 険悪（−50≤−30）かつ国力優位（120≥100×1.0）＝制裁を課す
            Assert.IsTrue(DiplomaticActionAiRules.ShouldSanction(-50f, 120f, 100f));
        }

        [Test]
        public void ShouldSanction_FriendlyOrInferior_False()
        {
            // 友好なら課さない
            Assert.IsFalse(DiplomaticActionAiRules.ShouldSanction(20f, 200f, 100f));
            // 険悪でも国力劣位なら課さない（返り血に耐えられない）
            Assert.IsFalse(DiplomaticActionAiRules.ShouldSanction(-50f, 80f, 100f));
        }

        // ===== 援助 =====

        [Test]
        public void ShouldGiveAid_FriendlyNeedySurplus_True()
        {
            // 友好（50≥40）かつ困窮（0.7≥0.3）かつ余裕（100≥0）＝援助する
            Assert.IsTrue(DiplomaticActionAiRules.ShouldGiveAid(50f, 0.7f, 100f));
        }

        [Test]
        public void ShouldGiveAid_NotFriendlyOrNotNeedy_False()
        {
            // 友好でない
            Assert.IsFalse(DiplomaticActionAiRules.ShouldGiveAid(10f, 0.7f, 100f));
            // 困窮していない（雪中の炭でない）
            Assert.IsFalse(DiplomaticActionAiRules.ShouldGiveAid(50f, 0.1f, 100f));
            // 自国に余裕がない（既定余裕閾値0・負の余裕）
            Assert.IsFalse(DiplomaticActionAiRules.ShouldGiveAid(50f, 0.7f, -5f));
        }

        // ===== 賠償 =====

        [Test]
        public void ProposedReparations_MatchesReparationsDelegate()
        {
            // warScore 0.5 は上限0.6未満なので burden=0.5、委譲先と厳密一致
            float warScore = 0.5f;
            float loserEconomy = 1000f;
            float expected = ReparationsRules.AnnualPayment(0.5f, loserEconomy);
            Assert.AreEqual(expected, DiplomaticActionAiRules.ProposedReparations(warScore, loserEconomy), 1e-4f);
        }

        [Test]
        public void ProposedReparations_CapsBurdenAndHandlesEdges()
        {
            // 圧勝（warScore 1.0）でも賠償率は上限0.6でクランプ＝鶏を殺さない
            float capped = DiplomaticActionAiRules.ProposedReparations(1.0f, 1000f);
            Assert.AreEqual(ReparationsRules.AnnualPayment(0.6f, 1000f), capped, 1e-4f);

            // 戦況が芳しくない（warScore≤0）＝賠償なし
            Assert.AreEqual(0f, DiplomaticActionAiRules.ProposedReparations(0f, 1000f), 1e-6f);
            Assert.AreEqual(0f, DiplomaticActionAiRules.ProposedReparations(-0.5f, 1000f), 1e-6f);

            // 負の敗者経済は委譲先で0扱い＝非負（境界・null安全）
            Assert.GreaterOrEqual(DiplomaticActionAiRules.ProposedReparations(0.5f, -1000f), 0f);
        }
    }
}
