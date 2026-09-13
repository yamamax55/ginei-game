using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>固定会戦QAの許容誤差つき判定（全分岐・前提崩れは未判定）。</summary>
    public class BattleQaJudgeRulesTests
    {
        [Test]
        public void RetreatDirection_AwayFromEnemy_FallbackWhenOverlapping()
        {
            Vector2 d = BattleQaJudgeRules.RetreatDirection(Vector2.zero, new Vector2(0f, 50f), Vector2.right);
            Assert.AreEqual(0f, d.x, 1e-4f);
            Assert.AreEqual(-1f, d.y, 1e-4f);

            Vector2 fb = BattleQaJudgeRules.RetreatDirection(Vector2.one, Vector2.one, new Vector2(3f, 0f));
            Assert.AreEqual(1f, fb.x, 1e-4f);

            Vector2 none = BattleQaJudgeRules.RetreatDirection(Vector2.one, Vector2.one, Vector2.zero);
            Assert.AreEqual(-1f, none.y, 1e-4f, "両方ゼロなら下向き");
        }

        [Test]
        public void DisplacementAlong_SignedProjection()
        {
            Assert.AreEqual(3f, BattleQaJudgeRules.DisplacementAlong(Vector2.zero, new Vector2(4f, -3f), Vector2.down), 1e-4f);
            Assert.AreEqual(-2f, BattleQaJudgeRules.DisplacementAlong(Vector2.zero, new Vector2(0f, 2f), new Vector2(0f, -10f)), 1e-4f);
            Assert.AreEqual(0f, BattleQaJudgeRules.DisplacementAlong(Vector2.zero, Vector2.one, Vector2.zero), 1e-4f);
        }

        [Test]
        public void JudgeRetreatingMember_AllBranches()
        {
            float min = BattleQaJudgeRules.MinRetreatDisplacement;
            Assert.AreEqual(BattleQaVerdict.未判定, BattleQaJudgeRules.JudgeRetreatingMember(false, true, 10f, min));
            Assert.AreEqual(BattleQaVerdict.不合格, BattleQaJudgeRules.JudgeRetreatingMember(true, false, 10f, min));
            Assert.AreEqual(BattleQaVerdict.不合格, BattleQaJudgeRules.JudgeRetreatingMember(true, true, min - 0.01f, min));
            Assert.AreEqual(BattleQaVerdict.合格, BattleQaJudgeRules.JudgeRetreatingMember(true, true, min, min));
        }

        [Test]
        public void JudgeBystander_AllBranches()
        {
            Assert.AreEqual(BattleQaVerdict.合格, BattleQaJudgeRules.JudgeBystander(false, false, 1f, 0.3f));
            Assert.AreEqual(BattleQaVerdict.不合格, BattleQaJudgeRules.JudgeBystander(true, false, 1f, 0.3f));
            Assert.AreEqual(BattleQaVerdict.不合格, BattleQaJudgeRules.JudgeBystander(false, true, 1f, 0.3f));
            Assert.AreEqual(BattleQaVerdict.未判定, BattleQaJudgeRules.JudgeBystander(false, true, 0.2f, 0.3f),
                "個艦撤退比を下回ると原因を分けられない");
        }

        [Test]
        public void RetreatCauseIsolated_Boundary()
        {
            Assert.IsTrue(BattleQaJudgeRules.RetreatCauseIsolated(0.3f, 0.3f));
            Assert.IsFalse(BattleQaJudgeRules.RetreatCauseIsolated(0.29f, 0.3f));
        }

        [Test]
        public void JudgeLockDuration_AllBranches()
        {
            Assert.AreEqual(BattleQaVerdict.未判定, BattleQaJudgeRules.JudgeLockDuration(false, true, 0));
            Assert.AreEqual(BattleQaVerdict.未判定, BattleQaJudgeRules.JudgeLockDuration(true, false, 0), "失われたら結論を出さない");
            Assert.AreEqual(BattleQaVerdict.不合格, BattleQaJudgeRules.JudgeLockDuration(true, true, 1));
            Assert.AreEqual(BattleQaVerdict.合格, BattleQaJudgeRules.JudgeLockDuration(true, true, 0));
        }

        [Test]
        public void JudgeDamageFlow_ContinuingVsStopped()
        {
            Assert.AreEqual(BattleQaVerdict.未判定, BattleQaJudgeRules.JudgeDamageFlow(true, 100, 90, 0f, 0));
            Assert.AreEqual(BattleQaVerdict.合格, BattleQaJudgeRules.JudgeDamageFlow(true, 100, 99, 1f, 0));
            Assert.AreEqual(BattleQaVerdict.不合格, BattleQaJudgeRules.JudgeDamageFlow(true, 100, 100, 1f, 0));
            Assert.AreEqual(BattleQaVerdict.合格, BattleQaJudgeRules.JudgeDamageFlow(false, 100, 100, 1f, 0));
            Assert.AreEqual(BattleQaVerdict.不合格, BattleQaJudgeRules.JudgeDamageFlow(false, 100, 99, 1f, 0));
            Assert.AreEqual(BattleQaVerdict.合格, BattleQaJudgeRules.JudgeDamageFlow(false, 100, 98, 1f, 2));
            Assert.AreEqual(BattleQaVerdict.不合格, BattleQaJudgeRules.JudgeDamageFlow(false, 100, 99, 1f, -5), "負の許容は0扱い");
        }

        [Test]
        public void JudgeHoldAgainstCorpsAi_AllBranches()
        {
            Assert.AreEqual(BattleQaVerdict.未判定,
                BattleQaJudgeRules.JudgeHoldAgainstCorpsAi(false, Formation.方陣, true, Formation.円陣, Formation.円陣), "対照が動いていない");
            Assert.AreEqual(BattleQaVerdict.未判定,
                BattleQaJudgeRules.JudgeHoldAgainstCorpsAi(true, Formation.円陣, true, Formation.円陣, Formation.円陣), "軍団AIも円陣＝空振り");
            Assert.AreEqual(BattleQaVerdict.不合格,
                BattleQaJudgeRules.JudgeHoldAgainstCorpsAi(true, Formation.方陣, false, Formation.円陣, Formation.円陣));
            Assert.AreEqual(BattleQaVerdict.不合格,
                BattleQaJudgeRules.JudgeHoldAgainstCorpsAi(true, Formation.方陣, true, Formation.方陣, Formation.円陣));
            Assert.AreEqual(BattleQaVerdict.合格,
                BattleQaJudgeRules.JudgeHoldAgainstCorpsAi(true, Formation.方陣, true, Formation.円陣, Formation.円陣));
        }

        [Test]
        public void JudgeReturnToCorpsAi_AllBranches()
        {
            Assert.AreEqual(BattleQaVerdict.不合格,
                BattleQaJudgeRules.JudgeReturnToCorpsAi(true, FormationOrderSource.軍団AI, Formation.方陣, Formation.方陣));
            Assert.AreEqual(BattleQaVerdict.不合格,
                BattleQaJudgeRules.JudgeReturnToCorpsAi(false, FormationOrderSource.直接命令, Formation.方陣, Formation.方陣));
            Assert.AreEqual(BattleQaVerdict.不合格,
                BattleQaJudgeRules.JudgeReturnToCorpsAi(false, FormationOrderSource.軍団AI, Formation.円陣, Formation.方陣));
            Assert.AreEqual(BattleQaVerdict.合格,
                BattleQaJudgeRules.JudgeReturnToCorpsAi(false, FormationOrderSource.軍団AI, Formation.方陣, Formation.方陣));
        }
    }
}
