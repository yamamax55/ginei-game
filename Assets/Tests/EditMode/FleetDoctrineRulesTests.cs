using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊ドクトリン選択（海軍戦略思想・SKUN-2 #1432）を固定する：各ドクトリンの行動重み（決戦/消耗/襲撃/回避）、
    /// ジャンケン的なドクトリン相性、戦力比・補給依存に応じた状況適合度、状況に応じた最適ドクトリン推奨、AI行動の傾き。
    /// </summary>
    public class FleetDoctrineRulesTests
    {
        private static FleetDoctrineParams P => FleetDoctrineParams.Default;

        // --- 行動重み：各ドクトリンの性格 ---

        [Test]
        public void Weights_EachDoctrineHasItsDominantWeight()
        {
            // 艦隊決戦＝決戦が最高（=現存艦隊の決戦より高い）
            Assert.AreEqual(1.0f, FleetDoctrineRules.EngagementWeight(FleetDoctrine.艦隊決戦, P), 1e-4f);
            Assert.Greater(FleetDoctrineRules.EngagementWeight(FleetDoctrine.艦隊決戦, P),
                           FleetDoctrineRules.EngagementWeight(FleetDoctrine.現存艦隊, P));

            // 漸減邀撃＝消耗が最高
            Assert.AreEqual(1.0f, FleetDoctrineRules.AttritionWeight(FleetDoctrine.漸減邀撃, P), 1e-4f);
            Assert.Greater(FleetDoctrineRules.AttritionWeight(FleetDoctrine.漸減邀撃, P),
                           FleetDoctrineRules.AttritionWeight(FleetDoctrine.艦隊決戦, P));

            // 通商破壊＝襲撃が最高
            Assert.AreEqual(1.0f, FleetDoctrineRules.CommerceRaidWeight(FleetDoctrine.通商破壊, P), 1e-4f);
            Assert.Greater(FleetDoctrineRules.CommerceRaidWeight(FleetDoctrine.通商破壊, P),
                           FleetDoctrineRules.CommerceRaidWeight(FleetDoctrine.艦隊決戦, P));

            // 現存艦隊＝回避が最高
            Assert.AreEqual(1.0f, FleetDoctrineRules.AvoidanceWeight(FleetDoctrine.現存艦隊, P), 1e-4f);
            Assert.Greater(FleetDoctrineRules.AvoidanceWeight(FleetDoctrine.現存艦隊, P),
                           FleetDoctrineRules.AvoidanceWeight(FleetDoctrine.艦隊決戦, P));
        }

        [Test]
        public void EngagementWeight_DecisiveHighFleetInBeingLow()
        {
            // 決戦を求める度合い：艦隊決戦＞現存艦隊（決戦を避ける）
            Assert.AreEqual(0.0f, FleetDoctrineRules.EngagementWeight(FleetDoctrine.現存艦隊, P), 1e-4f);
        }

        // --- AI行動の傾き ---

        [Test]
        public void AIBehaviorBias_MapsEachDoctrine()
        {
            Assert.AreEqual(DoctrineBias.攻勢的, FleetDoctrineRules.AIBehaviorBias(FleetDoctrine.艦隊決戦));
            Assert.AreEqual(DoctrineBias.守勢的, FleetDoctrineRules.AIBehaviorBias(FleetDoctrine.漸減邀撃));
            Assert.AreEqual(DoctrineBias.襲撃的, FleetDoctrineRules.AIBehaviorBias(FleetDoctrine.通商破壊));
            Assert.AreEqual(DoctrineBias.温存的, FleetDoctrineRules.AIBehaviorBias(FleetDoctrine.現存艦隊));
        }

        // --- ドクトリン相性（ジャンケン的） ---

        [Test]
        public void DoctrineMatchup_FleetInBeingWeakToCommerceRaiding()
        {
            // 現存艦隊は通商破壊に弱い（決戦を避けても交通線を狩られる）
            Assert.AreEqual(0.75f, FleetDoctrineRules.DoctrineMatchup(FleetDoctrine.現存艦隊, FleetDoctrine.通商破壊, P), 1e-4f);
            // 逆＝通商破壊は現存艦隊に有利
            Assert.AreEqual(1.3f, FleetDoctrineRules.DoctrineMatchup(FleetDoctrine.通商破壊, FleetDoctrine.現存艦隊, P), 1e-4f);
            // 決戦は決戦に応じる（互角）
            Assert.AreEqual(1.0f, FleetDoctrineRules.DoctrineMatchup(FleetDoctrine.艦隊決戦, FleetDoctrine.艦隊決戦, P), 1e-4f);
            // 漸減邀撃は決戦志向の敵に有利
            Assert.AreEqual(1.3f, FleetDoctrineRules.DoctrineMatchup(FleetDoctrine.漸減邀撃, FleetDoctrine.艦隊決戦, P), 1e-4f);
        }

        // --- 状況適合度 ---

        [Test]
        public void SituationalFitness_FleetInBeingFitsWhenOutnumbered()
        {
            // 劣勢（forceRatio低）ほど現存艦隊が適合
            float weak = FleetDoctrineRules.SituationalFitness(FleetDoctrine.現存艦隊, 0.1f, 0f, P);
            float strong = FleetDoctrineRules.SituationalFitness(FleetDoctrine.現存艦隊, 0.9f, 0f, P);
            Assert.AreEqual(0.9f, weak, 1e-4f);   // 1-0.1
            Assert.AreEqual(0.1f, strong, 1e-4f); // 1-0.9
            Assert.Greater(weak, strong);

            // 艦隊決戦は優勢ほど適合
            Assert.AreEqual(0.9f, FleetDoctrineRules.SituationalFitness(FleetDoctrine.艦隊決戦, 0.9f, 0f, P), 1e-4f);
        }

        [Test]
        public void SituationalFitness_CommerceRaidFitsWhenEnemySupplyDependent()
        {
            // 敵の補給依存が高いほど通商破壊が刺さる：supply=1,ratio=0.5 → 1*0.7 + 0.5*0.3 = 0.85
            float hi = FleetDoctrineRules.SituationalFitness(FleetDoctrine.通商破壊, 0.5f, 1f, P);
            float lo = FleetDoctrineRules.SituationalFitness(FleetDoctrine.通商破壊, 0.5f, 0f, P);
            Assert.AreEqual(0.85f, hi, 1e-4f);
            Assert.AreEqual(0.15f, lo, 1e-4f); // 0*0.7 + 0.5*0.3
            Assert.Greater(hi, lo);

            // 漸減邀撃は互角(0.5)で最大＝1
            Assert.AreEqual(1.0f, FleetDoctrineRules.SituationalFitness(FleetDoctrine.漸減邀撃, 0.5f, 0f, P), 1e-4f);
        }

        // --- 最適ドクトリン推奨 ---

        [Test]
        public void OptimalDoctrine_OutnumberedRecommendsFleetInBeing()
        {
            // 劣勢かつ敵が補給依存せず＝現存艦隊（決戦を避け牽制）
            Assert.AreEqual(FleetDoctrine.現存艦隊,
                FleetDoctrineRules.OptimalDoctrine(0.15f, 0.0f, 0.0f, P));

            // 圧倒的優勢＝艦隊決戦（一挙撃滅）
            Assert.AreEqual(FleetDoctrine.艦隊決戦,
                FleetDoctrineRules.OptimalDoctrine(0.95f, 0.0f, 0.0f, P));

            // 優勢に偏り(0.8)かつ敵が極度に補給依存＝通商破壊が最大に：
            // 通商破壊=1*0.7+(1-0.8)*0.3=0.76、漸減邀撃=1-1.5*0.3=0.55、艦隊決戦=0.8 …艦隊決戦が勝つ。
            // 逆に劣勢寄り(0.35)＋敵補給依存1.0：通商破壊=0.7+0.65*0.3=0.895、漸減邀撃=1-1.5*0.15=0.775、
            //   艦隊決戦=0.35、現存艦隊=0.65 → 通商破壊が最大（交通線を断つ）。
            Assert.AreEqual(FleetDoctrine.通商破壊,
                FleetDoctrineRules.OptimalDoctrine(0.35f, 1.0f, 0.0f, P));
        }

        [Test]
        public void OptimalDoctrine_OwnSupplyDependenceDiscountsRaidAndFleetInBeing()
        {
            // 自軍が補給依存だと通商破壊の適合が割り引かれる（自分も交通線を晒す）。
            // 劣勢寄り(0.35)・敵補給依存1.0：自軍補給0なら通商破壊=0.895で最大。
            // 自軍補給1.0だと 0.895*(1-0.4)=0.537 へ落ち、漸減邀撃(0.775)が上回り選ばれる。
            Assert.AreEqual(FleetDoctrine.通商破壊,
                FleetDoctrineRules.OptimalDoctrine(0.35f, 1.0f, 0.0f, P));
            Assert.AreEqual(FleetDoctrine.漸減邀撃,
                FleetDoctrineRules.OptimalDoctrine(0.35f, 1.0f, 1.0f, P));
        }
    }
}
