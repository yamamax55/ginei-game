using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>協力者（コラボレーター）力学の純ロジックテスト。既定Paramsで期待値を固定。</summary>
    public class CollaboratorRulesTests
    {
        const float Tol = 1e-4f;
        const float PowTol = 1e-3f; // Sqrt を含む箇所

        /// <summary>協力動機＝保身／利得／信念の加重和（既定は保身が支配的）。</summary>
        [Test]
        public void CollaborationIncentive_保身利得信念の加重和()
        {
            // 0.8*0.5 + 0.5*0.3 + 0.2*0.2 = 0.4+0.15+0.04 = 0.59（重み和=1）
            float inc = CollaboratorRules.CollaborationIncentive(0.8f, 0.5f, 0.2f);
            Assert.AreEqual(0.59f, inc, Tol);

            // 三つとも0なら動機なし
            Assert.AreEqual(0f, CollaboratorRules.CollaborationIncentive(0f, 0f, 0f), Tol);
            // 保身が高いほど動機が増える（保身の重みが最大）
            Assert.Greater(CollaboratorRules.CollaborationIncentive(1f, 0f, 0f),
                CollaboratorRules.CollaborationIncentive(0f, 0f, 1f));
        }

        /// <summary>協力者の母数＝動機×占領者の寛容さ。寛大な統治ほど協力者が増える。</summary>
        [Test]
        public void RecruitmentPool_動機と寛容さで母数が決まる()
        {
            // leniencyFactor = 0.5 + 0.5*0.8 = 0.9 → 0.6*0.9 = 0.54
            float pool = CollaboratorRules.RecruitmentPool(0.6f, 0.8f);
            Assert.AreEqual(0.54f, pool, Tol);

            // 動機0なら母数0
            Assert.AreEqual(0f, CollaboratorRules.RecruitmentPool(0f, 1f), Tol);
            // 寛容なほど協力者が増える
            Assert.Greater(CollaboratorRules.RecruitmentPool(0.6f, 1f),
                CollaboratorRules.RecruitmentPool(0.6f, 0f));
        }

        /// <summary>行政貢献＝有能さ×現地知識の幾何平均（片輪欠けは崩れる）。</summary>
        [Test]
        public void AdministrativeValue_有能さと現地知識で登用価値()
        {
            // compTerm=0.8*0.6=0.48, localTerm=0.5*0.4=0.2, sqrt(0.096)=0.309839
            float v = CollaboratorRules.AdministrativeValue(0.8f, 0.5f);
            Assert.AreEqual(0.309839f, v, PowTol);

            // 現地知識ゼロなら登用価値ゼロ（積の幾何平均）
            Assert.AreEqual(0f, CollaboratorRules.AdministrativeValue(1f, 0f), Tol);
            // 有能ゼロも同様
            Assert.AreEqual(0f, CollaboratorRules.AdministrativeValue(0f, 1f), Tol);
        }

        /// <summary>社会的烙印＝協力の露骨さ×住民敵意（裏切り者の烙印）。</summary>
        [Test]
        public void SocialStigma_露骨さと住民敵意で烙印()
        {
            // visTerm=0.8*0.5=0.4, hostileTerm=0.6*0.5=0.3, denom=0.25 → 0.12/0.25=0.48
            float s = CollaboratorRules.SocialStigma(0.8f, 0.6f);
            Assert.AreEqual(0.48f, s, Tol);

            // 露骨でなければ烙印なし
            Assert.AreEqual(0f, CollaboratorRules.SocialStigma(0f, 1f), Tol);
            // 住民敵意が強いほど烙印が濃い
            Assert.Greater(CollaboratorRules.SocialStigma(0.8f, 1f),
                CollaboratorRules.SocialStigma(0.8f, 0.2f));
        }

        /// <summary>占領者への忠誠＝動機×報復の恐れ（保身ゆえもろい）。</summary>
        [Test]
        public void LoyaltyToOccupier_動機と報復の恐れで縛られる()
        {
            // fearFactor = 0.4 + 0.6*0.7 = 0.82 → 0.59*0.82 = 0.4838
            float loy = CollaboratorRules.LoyaltyToOccupier(0.59f, 0.7f);
            Assert.AreEqual(0.4838f, loy, Tol);

            // 動機0なら忠誠0
            Assert.AreEqual(0f, CollaboratorRules.LoyaltyToOccupier(0f, 1f), Tol);
            // 逃げ場がない（報復の恐れが高い）ほど忠誠が上がる
            Assert.Greater(CollaboratorRules.LoyaltyToOccupier(0.6f, 1f),
                CollaboratorRules.LoyaltyToOccupier(0.6f, 0f));
        }

        /// <summary>解放後の粛清リスク＝烙印×政権交代。</summary>
        [Test]
        public void PostLiberationRisk_烙印と政権交代で粛清()
        {
            // regimeFactor = 0.4 + 0.6*0.9 = 0.94 → 0.48*0.94 = 0.4512
            float risk = CollaboratorRules.PostLiberationRisk(0.48f, 0.9f);
            Assert.AreEqual(0.4512f, risk, Tol);

            // 烙印がなければ粛清リスクなし
            Assert.AreEqual(0f, CollaboratorRules.PostLiberationRisk(0f, 1f), Tol);
            // 政権交代が大きいほど報復が苛烈
            Assert.Greater(CollaboratorRules.PostLiberationRisk(0.48f, 1f),
                CollaboratorRules.PostLiberationRisk(0.48f, 0f));
        }

        /// <summary>二股＝忠誠が低く粛清リスクが高いと保険をかける（二重忠誠）。</summary>
        [Test]
        public void DoubleDealing_忠誠が薄く粛清が怖いと二股()
        {
            // disloyalty=1-0.4838=0.5162, 0.5162*0.4512*0.5 = 0.116455
            float dd = CollaboratorRules.DoubleDealing(0.4838f, 0.4512f);
            Assert.AreEqual(0.116455f, dd, Tol);

            // 忠誠が高ければ二股なし
            Assert.AreEqual(0f, CollaboratorRules.DoubleDealing(1f, 1f), Tol);
            // 粛清リスクが高いほど保険を打つ
            Assert.Greater(CollaboratorRules.DoubleDealing(0.3f, 0.9f),
                CollaboratorRules.DoubleDealing(0.3f, 0.1f));
        }

        /// <summary>信頼できる協力者か＝高い忠誠かつ二股が薄い（既定閾値0.5）。</summary>
        [Test]
        public void IsReliableCollaborator_忠誠と二股の両面で判定()
        {
            // 忠誠0.6>=0.5 かつ 二股0.3<=0.5 → true
            Assert.IsTrue(CollaboratorRules.IsReliableCollaborator(0.6f, 0.3f));
            // 忠誠不足 → false
            Assert.IsFalse(CollaboratorRules.IsReliableCollaborator(0.4f, 0.3f));
            // 二股が濃い → false
            Assert.IsFalse(CollaboratorRules.IsReliableCollaborator(0.6f, 0.6f));
        }

        /// <summary>
        /// 物語テスト：苛烈な占領下で利得目当てに恭順した有能な現地貴族。露骨に占領者へ付き
        /// 住民の敵意は強い＝烙印が濃い。解放と政権交代が迫ると忠誠は打算ゆえ薄く粛清が怖く、
        /// 表で協力しつつ裏で解放側へ通じる二股に走る＝信頼できる協力者ではない。
        /// </summary>
        [Test]
        public void Narrative_打算で恭順した貴族が二股に走り信頼を失う()
        {
            var p = CollaboratorParams.Default;

            // 利得が支配的・信念は薄い動機
            float incentive = CollaboratorRules.CollaborationIncentive(0.4f, 0.9f, 0.1f, p);
            // 0.4*0.5+0.9*0.3+0.1*0.2 = 0.2+0.27+0.02 = 0.49
            Assert.AreEqual(0.49f, incentive, Tol);

            // 露骨に付き住民敵意も強い＝烙印が濃い
            float stigma = CollaboratorRules.SocialStigma(0.9f, 0.9f, p);
            Assert.Greater(stigma, 0.6f);

            // 報復の恐れがそこそこでも打算ゆえ忠誠は割れる
            float loyalty = CollaboratorRules.LoyaltyToOccupier(incentive, 0.5f, p);
            // 解放・政権交代が迫ると粛清リスクが高い
            float risk = CollaboratorRules.PostLiberationRisk(stigma, 0.9f, p);
            Assert.Greater(risk, loyalty); // 粛清の恐怖が忠誠を上回る

            // 忠誠薄×粛清高 → 二股に走る
            float dd = CollaboratorRules.DoubleDealing(loyalty, risk, p);
            Assert.Greater(dd, 0.1f);

            // 信頼できる協力者ではない
            Assert.IsFalse(CollaboratorRules.IsReliableCollaborator(loyalty, dd, 0.5f));

            // 対照：占領者がそのまま残れば（政権交代なし）粛清リスクは下がり二股も鎮まる
            float riskStay = CollaboratorRules.PostLiberationRisk(stigma, 0f, p);
            float ddStay = CollaboratorRules.DoubleDealing(loyalty, riskStay, p);
            Assert.Less(ddStay, dd);
        }
    }
}
