using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// コントリビューション制（軍税徴発＝戦争は戦争を養う・TYW-1 #1420）の純ロジック検証。
    /// 既定 KontributionParams（組織化寄与0.7・素の抽出比0.3・自活係数1.2・枯渇速度0.25・前進圧力1.0・荒廃速度0.3・財政独立0.8）で
    /// 期待値を固定する。
    /// </summary>
    public class KontributionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>抽出量＝占領地の富×（素の抽出比＋組織化×組織化寄与）。組織的なほど効率的に搾り取る。</summary>
        [Test]
        public void Extraction_組織化が高いほど多く抽出する()
        {
            // 0.8 × (0.3 + 0.5×0.7) = 0.8 × 0.65 = 0.52
            Assert.AreEqual(0.52f, KontributionRules.Extraction(0.8f, 0.5f), Eps);
            // 組織化0でも素の抽出比ぶんは得る：0.8 × 0.3 = 0.24
            Assert.AreEqual(0.24f, KontributionRules.Extraction(0.8f, 0f), Eps);
            // 富1・組織化1で最大：1 × (0.3+0.7) = 1.0
            Assert.AreEqual(1f, KontributionRules.Extraction(1f, 1f), Eps);
            // 組織化が高いほど抽出量は増える（単調）
            Assert.Greater(KontributionRules.Extraction(0.8f, 1f), KontributionRules.Extraction(0.8f, 0.5f));
        }

        /// <summary>軍の自活度＝抽出量×自活係数÷軍規模。戦争が戦争を養う。</summary>
        [Test]
        public void ArmySelfSufficiency_抽出で軍が本国財政から自活する()
        {
            // 0.3 × 1.2 / 0.8 = 0.36/0.8 = 0.45
            Assert.AreEqual(0.45f, KontributionRules.ArmySelfSufficiency(0.3f, 0.8f), Eps);
            // 抽出が軍規模に見合えば1（本国に頼らず自活）：0.5×1.2/0.5=1.2→クランプ1
            Assert.AreEqual(1f, KontributionRules.ArmySelfSufficiency(0.5f, 0.5f), Eps);
            // 養うべき軍がなければ常に自活
            Assert.AreEqual(1f, KontributionRules.ArmySelfSufficiency(0f, 0f), Eps);
        }

        /// <summary>占領地の枯渇＝徴発し続けると涸れる。搾り尽くすと次の占領地が要る。</summary>
        [Test]
        public void DepletionTick_徴発し続けると占領地が枯渇する()
        {
            // 1.0 − 1×1×0.25×1 = 0.75
            Assert.AreEqual(0.75f, KontributionRules.DepletionTick(1f, 1f, 1f), Eps);
            // dt0は不変
            Assert.AreEqual(1f, KontributionRules.DepletionTick(1f, 1f, 0f), Eps);
            // 徴発しなければ枯渇しない
            Assert.AreEqual(1f, KontributionRules.DepletionTick(1f, 0f, 1f), Eps);
        }

        /// <summary>前進圧力＝軍の自活度×枯渇度。占領地が枯渇すると新たな占領地を求めて前進する＝止まれない。</summary>
        [Test]
        public void AdvancePressure_占領地の枯渇が前進圧力を生む()
        {
            // 0.6 × 0.8 × 1.0 = 0.48
            Assert.AreEqual(0.48f, KontributionRules.AdvancePressure(0.6f, 0.8f), Eps);
            // 枯渇していなければ前進圧力なし
            Assert.AreEqual(0f, KontributionRules.AdvancePressure(0.6f, 0f), Eps);
            // 自活していなければ（本国頼みなら）前進せずとも済む
            Assert.AreEqual(0f, KontributionRules.AdvancePressure(0f, 0.8f), Eps);
        }

        /// <summary>戦争の自己永続度＝前進圧力×抽出。抽出と前進圧力が戦争を自己永続させる＝終われない戦争。</summary>
        [Test]
        public void WarSelfPerpetuation_抽出と前進圧力が戦争を自己永続させる()
        {
            // 0.48 × 0.52 = 0.2496
            Assert.AreEqual(0.2496f, KontributionRules.WarSelfPerpetuation(0.48f, 0.52f), Eps);
            // 抽出できなければ自己永続しない
            Assert.AreEqual(0f, KontributionRules.WarSelfPerpetuation(0.48f, 0f), Eps);
            // 前進圧力がなければ止まれる
            Assert.AreEqual(0f, KontributionRules.WarSelfPerpetuation(0f, 0.52f), Eps);
        }

        /// <summary>占領地の荒廃＝過酷な徴発が占領地を荒廃させる（住民の窮乏・人口流出）。</summary>
        [Test]
        public void OccupiedDevastation_過酷な徴発が占領地を荒廃させる()
        {
            // 0.2 + 1×0.3×1 = 0.5
            Assert.AreEqual(0.5f, KontributionRules.OccupiedDevastation(0.2f, 1f, 1f), Eps);
            // dt0は不変
            Assert.AreEqual(0.2f, KontributionRules.OccupiedDevastation(0.2f, 1f, 0f), Eps);
            // 上限1にクランプ（荒廃しきった土地はそれ以上荒れない）
            Assert.AreEqual(1f, KontributionRules.OccupiedDevastation(0.9f, 1f, 1f), Eps);
        }

        /// <summary>本国財政からの独立度＝軍の自活度×財政独立係数。軍が国家を超える。</summary>
        [Test]
        public void FiscalIndependenceFromState_軍の自活が将軍の政治的自律を増す()
        {
            // 1.0 × 0.8 = 0.8
            Assert.AreEqual(0.8f, KontributionRules.FiscalIndependenceFromState(1f), Eps);
            // 0.5 × 0.8 = 0.4
            Assert.AreEqual(0.4f, KontributionRules.FiscalIndependenceFromState(0.5f), Eps);
            // 自活度が高いほど政治的自律も高い（単調）
            Assert.Greater(KontributionRules.FiscalIndependenceFromState(1f),
                KontributionRules.FiscalIndependenceFromState(0.5f));
        }

        /// <summary>自己永続戦争の判定＝自己永続度が閾値以上。戦争が自力で燃料を調達し続ける。</summary>
        [Test]
        public void IsSelfFeedingWar_自己永続度が閾値以上で自己永続する()
        {
            Assert.IsTrue(KontributionRules.IsSelfFeedingWar(0.5f, 0.4f));
            Assert.IsFalse(KontributionRules.IsSelfFeedingWar(0.3f, 0.4f));
            // 境界（閾値ちょうどは成立）
            Assert.IsTrue(KontributionRules.IsSelfFeedingWar(0.4f, 0.4f));
        }
    }
}
