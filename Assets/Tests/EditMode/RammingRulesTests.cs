using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 衝角・特攻（体当たり攻撃＝捨て身戦法）の純ロジック <see cref="RammingRules"/> の EditMode テスト。
    /// 既定 <see cref="RammingParams.Default"/>（打撃1.0／意志 絶望0.6+士気0.4／最低回避0.0／盾重み0.5）で期待値を固定。
    /// </summary>
    public class RammingRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 衝突打撃は相対速度と質量の積に比例()
        {
            // scale=1.0, closingSpeed=10, ownMass=3 → 30
            Assert.AreEqual(30f, RammingRules.ImpactDamage(10f, 3f), Eps);
            // 負の速度/質量は0クランプ
            Assert.AreEqual(0f, RammingRules.ImpactDamage(-5f, 3f), Eps);
        }

        [Test]
        public void 道連れ度合いは打撃を相手耐久で割って01クランプ()
        {
            // 半壊
            Assert.AreEqual(0.5f, RammingRules.MutualDestruction(50f, 100f), Eps);
            // 打撃過剰でも1.0で頭打ち
            Assert.AreEqual(1f, RammingRules.MutualDestruction(300f, 100f), Eps);
            // 耐久0以下は最大
            Assert.AreEqual(1f, RammingRules.MutualDestruction(10f, 0f), Eps);
        }

        [Test]
        public void 自艦損失は生存性が高いほど減る()
        {
            // 生存性0＝全損
            Assert.AreEqual(1f, RammingRules.SelfLoss(0f), Eps);
            // 生存性0.2＝損失0.8
            Assert.AreEqual(0.8f, RammingRules.SelfLoss(0.2f), Eps);
        }

        [Test]
        public void 特攻意志は絶望と士気の加重和()
        {
            // (絶望0.6*0.6 + 士気0.6*0.4)/(0.6+0.4) = (0.36+0.24)/1 = 0.60
            Assert.AreEqual(0.60f, RammingRules.RammingWillingness(0.6f, 0.6f), Eps);
            // 絶望満点・士気満点＝1.0
            Assert.AreEqual(1f, RammingRules.RammingWillingness(1f, 1f), Eps);
        }

        [Test]
        public void 回避は正面で最小横で機動どおり()
        {
            // 真正面（0度）＝minEvasion(0)
            Assert.AreEqual(0f, RammingRules.InterceptEvasion(0.8f, 0f), Eps);
            // 180度＝機動どおり0.8
            Assert.AreEqual(0.8f, RammingRules.InterceptEvasion(0.8f, 180f), Eps);
            // 90度＝Lerp(0,0.8,0.5)=0.4
            Assert.AreEqual(0.4f, RammingRules.InterceptEvasion(0.8f, 90f), Eps);
        }

        [Test]
        public void 収支は相手破壊から自艦損失を引く()
        {
            // 道連れ1.0・自艦損失1.0（全損）＝0（差し違え）
            Assert.AreEqual(0f, RammingRules.NetExchange(1f, 1f), Eps);
            // 相手0.4・自損1.0＝-0.6（割に合わない）
            Assert.AreEqual(-0.6f, RammingRules.NetExchange(0.4f, 1f), Eps);
        }

        [Test]
        public void 盾の価値は脅威と守対象の貴さで決まる()
        {
            // 脅威1.0・守対象1.0：1.0*(1*0.5+0.5)=1.0
            Assert.AreEqual(1f, RammingRules.BlockingSacrifice(1f, 1f), Eps);
            // 脅威1.0・守対象0.0：1.0*(0+0.5)=0.5（守る価値が低くても脅威阻止分は残る）
            Assert.AreEqual(0.5f, RammingRules.BlockingSacrifice(1f, 0f), Eps);
        }

        [Test]
        public void 成立判定は意志閾値以上かつ収支非負()
        {
            // 意志0.6≥0.5・収支0.1≥0 → 成立
            Assert.IsTrue(RammingRules.IsRammingViable(0.6f, 0.1f));
            // 意志不足
            Assert.IsFalse(RammingRules.IsRammingViable(0.3f, 0.5f));
            // 収支マイナス（割に合わない）
            Assert.IsFalse(RammingRules.IsRammingViable(0.9f, -0.2f));
        }

        [Test]
        public void 物語_絶望した艦が高速で大型艦を道連れにするが自艦は失われる()
        {
            // 追い詰められ崩れていない小型艦（絶望0.9・士気0.8＝死兵）が高速で大型艦へ突入。
            float willingness = RammingRules.RammingWillingness(0.9f, 0.8f);
            // (0.9*0.6 + 0.8*0.4)/1.0 = (0.54+0.32) = 0.86
            Assert.AreEqual(0.86f, willingness, Eps);

            // 高速接近（closingSpeed=12）の小質量艦（mass=2）でも、大型艦の耐久(=20)を打ち砕く打撃。
            float impact = RammingRules.ImpactDamage(12f, 2f); // 24
            Assert.AreEqual(24f, impact, Eps);
            float mutual = RammingRules.MutualDestruction(impact, 20f); // 24/20=1.2→1.0 道連れ成立
            Assert.AreEqual(1f, mutual, Eps);

            // 装甲の薄い小型艦は全損（生存性0）。
            float selfLoss = RammingRules.SelfLoss(0f); // 1.0
            Assert.AreEqual(1f, selfLoss, Eps);

            // 収支：大型艦撃沈(1.0)−自艦全損(1.0)=0（差し違え＝損ではない）。
            float net = RammingRules.NetExchange(mutual, selfLoss); // 0
            Assert.AreEqual(0f, net, Eps);

            // 意志十分・収支非負＝特攻成立。
            Assert.IsTrue(RammingRules.IsRammingViable(willingness, net));

            // しかし大型艦が機動(0.9)で横(180度)へかわせば回避は機動どおり高く、特攻は空振りになりうる。
            float evasion = RammingRules.InterceptEvasion(0.9f, 180f);
            Assert.AreEqual(0.9f, evasion, Eps);
        }
    }
}
