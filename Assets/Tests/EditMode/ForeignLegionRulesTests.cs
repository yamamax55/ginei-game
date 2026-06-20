using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 外人部隊（外国人志願兵の精鋭部隊）を固定する：多国籍兵は共通目的と苦楽で結束し、過酷訓練×厳しい
    /// 選抜の幾何平均で精鋭化、祖国なき者ほど（他に居場所がないほど）部隊へ帰属し、精鋭は捨て駒として
    /// 危険任務へ投入、難民×窮状が募集源、戦歴×団結が名誉、帰属×名誉で損耗に耐える、三拍子で総合価値。
    /// 物語テスト＝祖国を失った難民が過酷な練成を経て不屈の精鋭外人部隊になる。クランプを担保。
    /// </summary>
    public class ForeignLegionRulesTests
    {
        private static readonly ForeignLegionParams P = ForeignLegionParams.Default;
        // 目的重み0.6/過酷重み0.5/危険重み0.5/窮状重み0.5/戦歴重み0.5/耐性フロア0.25

        [Test]
        public void MultinationalCohesion_PurposeAndHardshipBindMixedTroops()
        {
            // 0.8×0.6 + 0.6×0.4 = 0.48+0.24 = 0.72（共通目的をやや重く）
            Assert.AreEqual(0.72f, ForeignLegionRules.MultinationalCohesion(0.8f, 0.6f, P), 1e-4f);
            // どちらも0なら結束0
            Assert.AreEqual(0f, ForeignLegionRules.MultinationalCohesion(0f, 0f, P), 1e-4f);
            // クランプ（過大入力でも上限1）
            Assert.AreEqual(1f, ForeignLegionRules.MultinationalCohesion(2f, 2f, P), 1e-4f);
        }

        [Test]
        public void EliteForging_NeedsBothHarshnessAndRigor()
        {
            // 0.81^0.5 × 0.49^0.5 = 0.9 × 0.7 = 0.63（幾何平均）
            Assert.AreEqual(0.63f, ForeignLegionRules.EliteForging(0.81f, 0.49f, P), 1e-3f);
            // 選抜0＝鍛えても精鋭化0（両方要る）
            Assert.AreEqual(0f, ForeignLegionRules.EliteForging(1f, 0f, P), 1e-3f);
            // 訓練0＝選んでも精鋭化0
            Assert.AreEqual(0f, ForeignLegionRules.EliteForging(0f, 1f, P), 1e-3f);
        }

        [Test]
        public void UnitBelonging_NoHomeDeepensLoyalty()
        {
            // 居場所なし(home=0)＝結束がそのまま帰属：0.8×(1-0)=0.8
            Assert.AreEqual(0.8f, ForeignLegionRules.UnitBelonging(0.8f, 0f, P), 1e-4f);
            // 一部の居場所：0.8×(1-0.25)=0.6
            Assert.AreEqual(0.6f, ForeignLegionRules.UnitBelonging(0.8f, 0.25f, P), 1e-4f);
            // 帰る家がある(home=1)＝帰属0
            Assert.AreEqual(0f, ForeignLegionRules.UnitBelonging(0.8f, 1f, P), 1e-4f);
        }

        [Test]
        public void ExpendableDeployment_EliteIntoDanger()
        {
            // 0.8×0.5 + 0.6×0.5 = 0.4+0.3 = 0.7
            Assert.AreEqual(0.7f, ForeignLegionRules.ExpendableDeployment(0.8f, 0.6f, P), 1e-4f);
            // 危険度が上がるほど投入適性が上がる
            float low = ForeignLegionRules.ExpendableDeployment(0.8f, 0.2f, P);
            float high = ForeignLegionRules.ExpendableDeployment(0.8f, 0.9f, P);
            Assert.Greater(high, low);
        }

        [Test]
        public void RefugeeRecruitPool_DisplacedAndDesperate()
        {
            // 0.4×0.5 + 0.8×0.5 = 0.2+0.4 = 0.6
            Assert.AreEqual(0.6f, ForeignLegionRules.RefugeeRecruitPool(0.4f, 0.8f, P), 1e-4f);
            // どちらも0なら募集源0
            Assert.AreEqual(0f, ForeignLegionRules.RefugeeRecruitPool(0f, 0f, P), 1e-4f);
        }

        [Test]
        public void UnitHonor_RecordAndEsprit()
        {
            // 0.8×0.5 + 0.6×0.5 = 0.7
            Assert.AreEqual(0.7f, ForeignLegionRules.UnitHonor(0.8f, 0.6f, P), 1e-4f);
            // クランプ下限
            Assert.AreEqual(0f, ForeignLegionRules.UnitHonor(-1f, -1f, P), 1e-4f);
        }

        [Test]
        public void AttritionResilience_GeometricMeanOfBelongingAndHonor()
        {
            // sqrt(0.64×0.81)=sqrt(0.5184)=0.72
            Assert.AreEqual(0.72f, ForeignLegionRules.AttritionResilience(0.64f, 0.81f, P), 1e-3f);
            // 帰属だけ高く名誉0なら耐性0（両方要る＝幾何平均）
            Assert.AreEqual(0f, ForeignLegionRules.AttritionResilience(1f, 0f, P), 1e-3f);
        }

        [Test]
        public void LegionValue_ThreePillarsWithResilienceFloor()
        {
            // 耐性0.5→effResil=Lerp(0.25,1,0.5)=0.625; 0.5×0.6×0.625=0.1875
            Assert.AreEqual(0.1875f, ForeignLegionRules.LegionValue(0.5f, 0.6f, 0.5f, P), 1e-4f);
            // 耐性0でもフロア0.25で潰れきらない：0.8×0.8×0.25=0.16
            Assert.AreEqual(0.16f, ForeignLegionRules.LegionValue(0.8f, 0.8f, 0f, P), 1e-4f);
            // 精鋭度0なら総合価値0（三つの積）
            Assert.AreEqual(0f, ForeignLegionRules.LegionValue(0f, 1f, 1f, P), 1e-4f);
        }

        [Test]
        public void DefaultDelegates_MatchExplicitParams()
        {
            Assert.AreEqual(ForeignLegionRules.MultinationalCohesion(0.7f, 0.5f, P),
                ForeignLegionRules.MultinationalCohesion(0.7f, 0.5f), 1e-4f);
            Assert.AreEqual(ForeignLegionRules.EliteForging(0.6f, 0.4f, P),
                ForeignLegionRules.EliteForging(0.6f, 0.4f), 1e-3f);
            Assert.AreEqual(ForeignLegionRules.LegionValue(0.5f, 0.6f, 0.5f, P),
                ForeignLegionRules.LegionValue(0.5f, 0.6f, 0.5f), 1e-4f);
        }

        [Test]
        public void Story_RefugeesForgedIntoUnbreakableLegion()
        {
            // 祖国を失った難民（窮状の高い募集源）が外人部隊を志願する
            float pool = ForeignLegionRules.RefugeeRecruitPool(0.7f, 0.9f, P);
            Assert.Greater(pool, 0.5f);

            // 過酷な訓練と厳しい選抜で精鋭へ練成
            float elite = ForeignLegionRules.EliteForging(0.9f, 0.8f, P);
            Assert.Greater(elite, 0.7f);

            // 共通の目的と苦楽で多国籍兵が結束し、帰る家がない（home低）ほど部隊へ深く帰属
            float cohesion = ForeignLegionRules.MultinationalCohesion(0.8f, 0.8f, P);
            float belonging = ForeignLegionRules.UnitBelonging(cohesion, 0.1f, P);
            Assert.Greater(belonging, 0.6f);

            // 戦歴と団結が名誉を育て、帰属×名誉で損耗に耐える
            float honor = ForeignLegionRules.UnitHonor(0.8f, 0.9f, P);
            float resilience = ForeignLegionRules.AttritionResilience(belonging, honor, P);
            Assert.Greater(resilience, 0.6f);

            // 三拍子が揃った外人部隊は高い総合価値を持つ
            float value = ForeignLegionRules.LegionValue(elite, belonging, resilience, P);
            Assert.Greater(value, 0.45f);

            // 居場所がある志願者（home高）は帰属が浅く、同じ条件でも総合価値が落ちる
            float softBelonging = ForeignLegionRules.UnitBelonging(cohesion, 0.9f, P);
            float softValue = ForeignLegionRules.LegionValue(elite, softBelonging, resilience, P);
            Assert.Greater(value, softValue);
        }
    }
}
