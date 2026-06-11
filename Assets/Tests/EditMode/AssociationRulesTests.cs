using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 中間団体・市民結社（TOCQ-2 #1482・トクヴィル）の純ロジック検証。
    /// 結社の密度・専制への防壁・孤立の防止・民主主義の学校・集合行動の力・国家による萎縮・多元的均衡・市民的活力判定を担保。
    /// 既定 Params（防壁0.9/学校速度0.05/萎縮速度0.06/活力閾値0.5）で期待値を固定。
    /// </summary>
    public class AssociationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>結社の密度＝団体の数×市民参加（積＝両方が要る）。</summary>
        [Test]
        public void AssociationalDensity_団体数と参加の積()
        {
            // 0.8 * 0.5 = 0.4
            Assert.AreEqual(0.4f, AssociationRules.AssociationalDensity(0.8f, 0.5f), Eps);
            // どちらか欠ければ0
            Assert.AreEqual(0f, AssociationRules.AssociationalDensity(0.9f, 0f), Eps);
        }

        /// <summary>専制への防壁＝結社の密度×0.9（厚い結社ほど厚い防壁）。</summary>
        [Test]
        public void BufferAgainstDespotism_密度に比例した防壁()
        {
            // 0.6 * 0.9 = 0.54
            Assert.AreEqual(0.54f, AssociationRules.BufferAgainstDespotism(0.6f), Eps);
            // 密度0なら防壁0（孤立した個人は屈する）
            Assert.AreEqual(0f, AssociationRules.BufferAgainstDespotism(0f), Eps);
        }

        /// <summary>孤立の防止＝結社の密度×個人主義（病が重いほど薬が効く）。</summary>
        [Test]
        public void IsolationProtection_個人主義が強いほど結社が効く()
        {
            // 0.5 * 0.8 = 0.4
            Assert.AreEqual(0.4f, AssociationRules.IsolationProtection(0.5f, 0.8f), Eps);
            // 個人主義が無ければ防ぐ孤立も無い
            Assert.AreEqual(0f, AssociationRules.IsolationProtection(0.7f, 0f), Eps);
        }

        /// <summary>民主主義の学校＝参加×学校速度0.05×dt（参加が公共心を育てる増分）。</summary>
        [Test]
        public void SchoolOfDemocracy_参加が自治能力を育てる()
        {
            // 0.05 * 0.8 * 1 = 0.04
            Assert.AreEqual(0.04f, AssociationRules.SchoolOfDemocracy(0.8f, 1f), Eps);
            // 参加0なら育たない
            Assert.AreEqual(0f, AssociationRules.SchoolOfDemocracy(0f, 2f), Eps);
        }

        /// <summary>集合行動の力＝結社の密度×共通の関心（積＝両方が要る）。</summary>
        [Test]
        public void CollectiveActionCapacity_密度と関心の積()
        {
            // 0.6 * 0.5 = 0.3
            Assert.AreEqual(0.3f, AssociationRules.CollectiveActionCapacity(0.6f, 0.5f), Eps);
            // 共通の関心が無ければ共同行動は立ち上がらない
            Assert.AreEqual(0f, AssociationRules.CollectiveActionCapacity(0.9f, 0f), Eps);
        }

        /// <summary>国家による萎縮＝介入×萎縮速度0.06×dt ずつ密度・自律が低下（後見国家が市民社会を痩せさせる）。</summary>
        [Test]
        public void StateAtrophyTick_国家介入で結社が痩せる()
        {
            var assoc = new CivicAssociation(0.8f, 0.7f, 0.6f);
            // delta = 0.06 * 1.0 * 1 = 0.06
            var next = AssociationRules.StateAtrophyTick(assoc, 1f, 1f);
            Assert.AreEqual(0.74f, next.density, Eps);        // 0.8 - 0.06
            Assert.AreEqual(0.64f, next.autonomy, Eps);       // 0.7 - 0.06
            Assert.AreEqual(0.54f, next.participation, Eps);  // 0.6 - 0.06（受け皿の縮小ぶん）
            // 介入0なら不変
            var stable = AssociationRules.StateAtrophyTick(assoc, 0f, 1f);
            Assert.AreEqual(0.8f, stable.density, Eps);
            Assert.AreEqual(0.7f, stable.autonomy, Eps);
            Assert.AreEqual(0.6f, stable.participation, Eps);
        }

        /// <summary>多元的均衡＝防壁が国家権力に拮抗するほど高く、密度が薄ければ崩れる。</summary>
        [Test]
        public void PluralisticBalance_防壁と国家権力の拮抗()
        {
            // density 0.5 → buffer = 0.5*0.9 = 0.45。state=0.45 で完全拮抗
            // balance = (1 - |0.45-0.45|) * 0.5 = 0.5
            Assert.AreEqual(0.5f, AssociationRules.PluralisticBalance(0.5f, 0.45f), Eps);
            // 中間団体が薄い（density 0）なら均衡そのものが成立しない
            Assert.AreEqual(0f, AssociationRules.PluralisticBalance(0f, 0.5f), Eps);
        }

        /// <summary>市民的活力＝密度が閾値0.5以上で専制に強い。</summary>
        [Test]
        public void IsCivicVitality_閾値で活力判定()
        {
            Assert.IsTrue(AssociationRules.IsCivicVitality(0.6f, 0.5f));
            Assert.IsTrue(AssociationRules.IsCivicVitality(0.5f, 0.5f));   // 境界は含む
            Assert.IsFalse(AssociationRules.IsCivicVitality(0.49f, 0.5f));
        }

        /// <summary>全入力クランプ＝範囲外を渡しても0..1に収まる（決定論・無例外）。</summary>
        [Test]
        public void 全API_入力クランプで安全()
        {
            Assert.AreEqual(1f, AssociationRules.AssociationalDensity(5f, 3f), Eps);
            Assert.AreEqual(0f, AssociationRules.BufferAgainstDespotism(-1f), Eps);
            Assert.AreEqual(1f, AssociationRules.IsolationProtection(2f, 2f), Eps);
            var next = AssociationRules.StateAtrophyTick(new CivicAssociation(0f, 0f, 0f), 5f, -1f);
            Assert.AreEqual(0f, next.density, Eps);   // dt<0 はクランプされ不変・下限0
        }
    }
}
