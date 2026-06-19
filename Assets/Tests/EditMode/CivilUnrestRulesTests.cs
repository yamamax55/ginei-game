using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>市民不安（社会の不穏度と治安悪化）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class CivilUnrestRulesTests
    {
        const float Eps = 0.0001f;
        const float DivEps = 0.001f; // 除算箇所の緩めの許容

        /// <summary>不満＝生活苦0.4/抑圧0.3/不公正0.3 の重み付き平均。</summary>
        [Test]
        public void Discontent_重み付き平均で不満を出す()
        {
            // (0.5*0.4 + 0.5*0.3 + 0.5*0.3)/1.0 = 0.5
            Assert.AreEqual(0.5f, CivilUnrestRules.Discontent(0.5f, 0.5f, 0.5f), Eps);
            // 生活苦のみ＝0.4
            Assert.AreEqual(0.4f, CivilUnrestRules.Discontent(1f, 0f, 0f), Eps);
            // (0.8*0.4 + 0.4*0.3 + 0.6*0.3) = 0.32+0.12+0.18 = 0.62
            Assert.AreEqual(0.62f, CivilUnrestRules.Discontent(0.8f, 0.4f, 0.6f), Eps);
            // 全て0なら不満0
            Assert.AreEqual(0f, CivilUnrestRules.Discontent(0f, 0f, 0f), Eps);
        }

        /// <summary>不安の伝播＝不満×(0.2 + 0.8×つながり)。つながり0でも下限ぶん広がる。</summary>
        [Test]
        public void UnrestSpread_不満とつながりで広がる()
        {
            // 0.6*(0.2+0.8*1) = 0.6
            Assert.AreEqual(0.6f, CivilUnrestRules.UnrestSpread(0.6f, 1f), Eps);
            // 0.6*0.2 = 0.12（つながり0でも下限）
            Assert.AreEqual(0.12f, CivilUnrestRules.UnrestSpread(0.6f, 0f), Eps);
            // 1*(0.2+0.4) = 0.6
            Assert.AreEqual(0.6f, CivilUnrestRules.UnrestSpread(1f, 0.5f), Eps);
            // つながりが高いほど広がる（単調）
            Assert.Greater(CivilUnrestRules.UnrestSpread(0.8f, 1f),
                           CivilUnrestRules.UnrestSpread(0.8f, 0f), "つながりが厚いほど不安は広がる");
        }

        /// <summary>当局の対応＝治安力0.6/譲歩0.4 の重み付き平均。</summary>
        [Test]
        public void AuthorityResponse_治安力と譲歩の合算()
        {
            // 0.8*0.6 + 0.4*0.4 = 0.48+0.16 = 0.64
            Assert.AreEqual(0.64f, CivilUnrestRules.AuthorityResponse(0.8f, 0.4f), Eps);
            // 両方1なら1
            Assert.AreEqual(1f, CivilUnrestRules.AuthorityResponse(1f, 1f), Eps);
            // 両方0なら0
            Assert.AreEqual(0f, CivilUnrestRules.AuthorityResponse(0f, 0f), Eps);
        }

        /// <summary>沈静化＝対応/(対応+伝播) の競合比。</summary>
        [Test]
        public void Suppression_対応が伝播を上回るほど鎮まる()
        {
            // 0.64/(0.64+0.6) = 0.64/1.24 = 0.516129
            Assert.AreEqual(0.516129f, CivilUnrestRules.Suppression(0.64f, 0.6f), DivEps);
            // 対応のみなら完全沈静
            Assert.AreEqual(1f, CivilUnrestRules.Suppression(1f, 0f), Eps);
            // 両方0なら沈静0
            Assert.AreEqual(0f, CivilUnrestRules.Suppression(0f, 0f), Eps);
            // 対応が大きいほど沈静が進む（単調）
            Assert.Greater(CivilUnrestRules.Suppression(0.8f, 0.4f),
                           CivilUnrestRules.Suppression(0.2f, 0.4f), "対応が厚いほど鎮まる");
        }

        /// <summary>激化＝伝播×過剰鎮圧×0.5＝弾圧が火に油。</summary>
        [Test]
        public void Escalation_過剰鎮圧が不安を煽る()
        {
            // 0.6*1*0.5 = 0.3
            Assert.AreEqual(0.3f, CivilUnrestRules.Escalation(0.6f, 1f), Eps);
            // 過剰鎮圧0なら激化なし
            Assert.AreEqual(0f, CivilUnrestRules.Escalation(0.6f, 0f), Eps);
            // 1*0.5*0.5 = 0.25
            Assert.AreEqual(0.25f, CivilUnrestRules.Escalation(1f, 0.5f), Eps);
            // 過剰鎮圧が強いほど激化（単調）
            Assert.Greater(CivilUnrestRules.Escalation(0.6f, 1f),
                           CivilUnrestRules.Escalation(0.6f, 0.3f), "弾圧が過剰なほど燃え広がる");
        }

        /// <summary>許容閾値＝-0.1 + 安定×0.4。安定した社会は不安に強い。</summary>
        [Test]
        public void ToleranceThreshold_安定で閾値が上がる()
        {
            // -0.1 + 1*0.4 = 0.3
            Assert.AreEqual(0.3f, CivilUnrestRules.ToleranceThreshold(1f), Eps);
            // -0.1 + 0 = -0.1
            Assert.AreEqual(-0.1f, CivilUnrestRules.ToleranceThreshold(0f), Eps);
            // -0.1 + 0.5*0.4 = 0.1
            Assert.AreEqual(0.1f, CivilUnrestRules.ToleranceThreshold(0.5f), Eps);
        }

        /// <summary>不安水準＝伝播−沈静+激化（-1..1）。</summary>
        [Test]
        public void UnrestLevel_伝播から沈静を引き激化を足す()
        {
            // 0.6-0.5+0.3 = 0.4
            Assert.AreEqual(0.4f, CivilUnrestRules.UnrestLevel(0.6f, 0.5f, 0.3f), Eps);
            // 0.2-0.8+0 = -0.6（沈静が勝つ＝落ち着き）
            Assert.AreEqual(-0.6f, CivilUnrestRules.UnrestLevel(0.2f, 0.8f, 0f), Eps);
            // -1..1にクランプ（伝播1・沈静0・激化1 → 2 だが 1 へ）
            Assert.AreEqual(1f, CivilUnrestRules.UnrestLevel(1f, 0f, 1f), Eps);
        }

        /// <summary>秩序判定＝不安水準が許容閾値（+余裕）を超えなければ保たれる。</summary>
        [Test]
        public void IsOrderMaintained_閾値超えで秩序崩壊()
        {
            // 閾値0.1・不安0.05 → 保たれる
            Assert.IsTrue(CivilUnrestRules.IsOrderMaintained(0.05f, 0.1f));
            // 閾値0.1・不安0.4 → 崩壊
            Assert.IsFalse(CivilUnrestRules.IsOrderMaintained(0.4f, 0.1f));
            // 境界（等しい）は保たれる
            Assert.IsTrue(CivilUnrestRules.IsOrderMaintained(0.1f, 0.1f));
            // 余裕0.2を足せば 0.25 は閾値0.1+0.2=0.3 以内＝保たれる
            Assert.IsTrue(CivilUnrestRules.IsOrderMaintained(0.25f, 0.1f, 0.2f));
        }

        /// <summary>
        /// 物語：生活苦と抑圧に喘ぐ密集都市で不公正がはびこり不満が募る。当局が弾圧一辺倒（譲歩薄・過剰鎮圧）に
        /// 走ると激化が沈静を上回り、不安水準が脆い社会の許容閾値を超えて暴動へ転化する。だが同じ不安でも
        /// 譲歩を厚くして弾圧を控えれば沈静が勝り、安定した社会の高い閾値の内で秩序が保たれる。
        /// </summary>
        [Test]
        public void 物語_弾圧一辺倒は暴動へ譲歩と安定は秩序を保つ()
        {
            // 生活苦・抑圧・不公正が重なり不満が高い
            float discontent = CivilUnrestRules.Discontent(0.8f, 0.7f, 0.75f);
            Assert.Greater(discontent, 0.7f, "苦境で不満が募る");

            // 密集した社会で不安が広く伝播
            float spread = CivilUnrestRules.UnrestSpread(discontent, 0.8f);
            Assert.Greater(spread, 0.6f, "つながりの厚い都市で広がる");

            // 弾圧一辺倒：治安力は高いが譲歩が薄く過剰鎮圧
            float harshResponse = CivilUnrestRules.AuthorityResponse(0.7f, 0.05f);
            float harshSup = CivilUnrestRules.Suppression(harshResponse, spread);
            float harshEsc = CivilUnrestRules.Escalation(spread, 0.95f);
            float harshLevel = CivilUnrestRules.UnrestLevel(spread, harshSup, harshEsc);
            // 脆い社会（安定低い）
            float fragileTol = CivilUnrestRules.ToleranceThreshold(0.2f);
            Assert.IsFalse(CivilUnrestRules.IsOrderMaintained(harshLevel, fragileTol),
                           "弾圧一辺倒の激化で暴動へ転化");

            // 融和路線：譲歩を厚くし弾圧を控える
            float softResponse = CivilUnrestRules.AuthorityResponse(0.7f, 0.85f);
            float softSup = CivilUnrestRules.Suppression(softResponse, spread);
            float softEsc = CivilUnrestRules.Escalation(spread, 0.1f);
            float softLevel = CivilUnrestRules.UnrestLevel(spread, softSup, softEsc);
            // 安定した社会（高い閾値）
            float stableTol = CivilUnrestRules.ToleranceThreshold(0.9f);
            Assert.IsTrue(CivilUnrestRules.IsOrderMaintained(softLevel, stableTol),
                          "譲歩と安定で秩序は保たれる");

            // 同じ不安でも融和路線の方が不安水準は低い
            Assert.Less(softLevel, harshLevel, "弾圧より譲歩の方が不安は鎮まる");
        }
    }
}
