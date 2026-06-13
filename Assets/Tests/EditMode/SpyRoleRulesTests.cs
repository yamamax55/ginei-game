using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 用間五種体系（孫子「用間篇」・#1127）のテスト。内間の高情報・死間の必発覚＆偽情報・
    /// 反間の二重スパイ価値・コスト差・神紀（五間同時運用）の発覚割引を既定Paramsで担保する。
    /// </summary>
    public class SpyRoleRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>内間（敵官吏）は郷間（住民）より深い情報を得る。</summary>
        [Test]
        public void 内間は郷間より高情報()
        {
            float insider = SpyRoleRules.IntelYield(SpyRole.内間, 1f);
            float local = SpyRoleRules.IntelYield(SpyRole.郷間, 1f);
            Assert.AreEqual(1f, insider, Eps);   // 内間情報係数1.0
            Assert.AreEqual(0.4f, local, Eps);   // 郷間情報係数0.4
            Assert.Greater(insider, local);
        }

        /// <summary>死間は情報を持ち帰らない＝情報量ゼロ。</summary>
        [Test]
        public void 死間は情報を持ち帰らない()
        {
            Assert.AreEqual(0f, SpyRoleRules.IntelYield(SpyRole.死間, 1f), Eps);
        }

        /// <summary>死間は防諜に依らず必ず露見する（捨て駒＝発覚前提）。</summary>
        [Test]
        public void 死間は必ず発覚する()
        {
            Assert.AreEqual(1f, SpyRoleRules.ExposureRisk(SpyRole.死間, 0f), Eps);
            Assert.AreEqual(1f, SpyRoleRules.ExposureRisk(SpyRole.死間, 1f), Eps);
            Assert.IsTrue(SpyRoleRules.IsExposed(SpyRole.死間, 0f, 0.999f));
        }

        /// <summary>生間（熟練）は内間より発覚リスクが低い（低リスクで往復）。</summary>
        [Test]
        public void 生間は内間より低リスク()
        {
            float rover = SpyRoleRules.ExposureRisk(SpyRole.生間, 1f);   // 1.0×0.4
            float insider = SpyRoleRules.ExposureRisk(SpyRole.内間, 1f); // 1.0×0.8
            Assert.AreEqual(0.4f, rover, Eps);
            Assert.AreEqual(0.8f, insider, Eps);
            Assert.Less(rover, insider);
        }

        /// <summary>偽情報は死間が主役＝死間は注入し、内間は注入しない。</summary>
        [Test]
        public void 死間は偽情報を注入し内間はしない()
        {
            float dead = SpyRoleRules.Disinformation(SpyRole.死間, 1f);   // 0.8×1.0
            float insider = SpyRoleRules.Disinformation(SpyRole.内間, 1f);
            Assert.AreEqual(0.8f, dead, Eps);
            Assert.AreEqual(0f, insider, Eps);
        }

        /// <summary>反間の価値は敵の信頼が残るほど高く、反間以外はゼロ。</summary>
        [Test]
        public void 反間の二重スパイ価値()
        {
            float val = SpyRoleRules.TurncoatValue(SpyRole.反間, 1f);     // 1.0×1.0
            float none = SpyRoleRules.TurncoatValue(SpyRole.内間, 1f);
            Assert.AreEqual(1f, val, Eps);
            Assert.AreEqual(0f, none, Eps);
            // 信頼が薄れるほど価値は下がる。
            Assert.Greater(SpyRoleRules.TurncoatValue(SpyRole.反間, 1f),
                           SpyRoleRules.TurncoatValue(SpyRole.反間, 0.5f));
        }

        /// <summary>内間（高位官吏の買収）は郷間（住民）より運用コストが高い。</summary>
        [Test]
        public void 内間は郷間よりコスト高()
        {
            float insider = SpyRoleRules.RoleCost(SpyRole.内間);   // 0.9
            float local = SpyRoleRules.RoleCost(SpyRole.郷間);     // 0.9×0.2
            Assert.AreEqual(0.9f, insider, Eps);
            Assert.AreEqual(0.18f, local, Eps);
            Assert.Greater(insider, local);
        }

        /// <summary>目的に応じた最適間者の選定（情報＝内間／偽情報＝死間／予算不足は妥協）。</summary>
        [Test]
        public void 目的別の最適間者()
        {
            // 深い情報＋潤沢な予算＝内間。
            Assert.AreEqual(SpyRole.内間, SpyRoleRules.BestRoleForObjective(0f, 1f));
            // 偽情報＋潤沢な予算＝死間。
            Assert.AreEqual(SpyRole.死間, SpyRoleRules.BestRoleForObjective(1f, 1f));
            // 深い情報だが予算が乏しい＝郷間へ妥協。
            Assert.AreEqual(SpyRole.郷間, SpyRoleRules.BestRoleForObjective(0f, 0.05f));
        }

        /// <summary>神紀＝五間同時運用ほど発覚リスクが割り引かれる（1種以下は割引なし）。</summary>
        [Test]
        public void 神紀は五間同時で発覚を割り引く()
        {
            Assert.AreEqual(1f, SpyRoleRules.GodlikeWebFactor(1), Eps);
            Assert.AreEqual(1f, SpyRoleRules.GodlikeWebFactor(0), Eps);
            Assert.AreEqual(0.5f, SpyRoleRules.GodlikeWebFactor(5), Eps);
            // 種類が増えるほど単調に割引が深まる。
            Assert.Greater(SpyRoleRules.GodlikeWebFactor(2), SpyRoleRules.GodlikeWebFactor(4));
        }
    }
}
