using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>要塞出撃（ソーティ）の純ロジックを既定Paramsの具体値で固定する。</summary>
    public class FortressSortieRulesTests
    {
        /// <summary>出撃の好機＝敵の混乱×要塞即応（どちらか低いと萎む）。</summary>
        [Test]
        public void SortieOpportunity_IsProductOfDisorganizationAndReadiness()
        {
            // 0.8 * 0.5 * 1 = 0.4
            Assert.AreEqual(0.4f, FortressSortieRules.SortieOpportunity(0.8f, 0.5f), 1e-4f);
            // 敵が崩れていなければ好機なし
            Assert.AreEqual(0f, FortressSortieRules.SortieOpportunity(0f, 1f), 1e-4f);
            // 入力clamp（上振れ）
            Assert.AreEqual(1f, FortressSortieRules.SortieOpportunity(2f, 2f), 1e-4f);
        }

        /// <summary>掩護戦闘力＝出撃部隊×(1＋砲傘)。砲支援ぶん底上げ。</summary>
        [Test]
        public void CoveredEngagement_BoostedByGunSupport()
        {
            // 0.6 * (1 + 0.8*0.5) = 0.6 * 1.4 = 0.84
            Assert.AreEqual(0.84f, FortressSortieRules.CoveredEngagement(0.6f, 0.8f), 1e-4f);
            // 砲支援なしなら部隊そのまま
            Assert.AreEqual(0.6f, FortressSortieRules.CoveredEngagement(0.6f, 0f), 1e-4f);
        }

        /// <summary>攻囲線打撃＝掩護/(掩護＋塹壕)（比で1へ／0へ・両者0なら0）。</summary>
        [Test]
        public void SiegeLineDamage_RatioOfCoverToEntrenchment()
        {
            // 0.84 / (0.84 + 0.4) = 0.84/1.24 = 0.677419...
            Assert.AreEqual(0.677419f, FortressSortieRules.SiegeLineDamage(0.84f, 0.4f), 1e-4f);
            // 0.8 / (0.8 + 0.2) = 0.8
            Assert.AreEqual(0.8f, FortressSortieRules.SiegeLineDamage(0.8f, 0.2f), 1e-4f);
            // 両者0は0（ゼロ割安全）
            Assert.AreEqual(0f, FortressSortieRules.SiegeLineDamage(0f, 0f), 1e-4f);
        }

        /// <summary>引き上げの好機＝戦況×(1＋敵増援)。深追い前に戻る。</summary>
        [Test]
        public void WithdrawalTiming_RisesWithProgressAndReinforcement()
        {
            // 0.6 * (1 + 0.4*0.5) = 0.6 * 1.2 = 0.72
            Assert.AreEqual(0.72f, FortressSortieRules.WithdrawalTiming(0.6f, 0.4f), 1e-4f);
            // 戦況が進んでいなければ戻る必要なし
            Assert.AreEqual(0f, FortressSortieRules.WithdrawalTiming(0f, 1f), 1e-4f);
        }

        /// <summary>退路の安全＝要塞への近さ×砲の傘（近いほど・砲が厚いほど安全）。</summary>
        [Test]
        public void RetreatPathSecurity_FromProximityAndGunCover()
        {
            // dist=5: proximity=1-5/10=0.5, guns=0 → Lerp(0.5,1,0)=0.5
            Assert.AreEqual(0.5f, FortressSortieRules.RetreatPathSecurity(5f, 0f), 1e-4f);
            // dist=5, guns=1.0: Lerp(0.5,1,0.5)=0.75
            Assert.AreEqual(0.75f, FortressSortieRules.RetreatPathSecurity(5f, 1f), 1e-4f);
            // 要塞直下は完全に安全
            Assert.AreEqual(1f, FortressSortieRules.RetreatPathSecurity(0f, 0f), 1e-4f);
            // 遠方は退路なし（砲傘なし）
            Assert.AreEqual(0f, FortressSortieRules.RetreatPathSecurity(100f, 0f), 1e-4f);
        }

        /// <summary>損害リスク＝露出×敵戦力×(1-退路安全)（退路完全なら0）。</summary>
        [Test]
        public void SortieRisk_FromExposureEnemyAndRetreat()
        {
            // 0.6 * 0.8 * (1-0.5) * 1 = 0.24
            Assert.AreEqual(0.24f, FortressSortieRules.SortieRisk(0.6f, 0.8f, 0.5f), 1e-4f);
            // 退路が完全に安全ならリスク0
            Assert.AreEqual(0f, FortressSortieRules.SortieRisk(1f, 1f, 1f), 1e-4f);
        }

        /// <summary>正味価値＝攻囲線打撃−損害リスク（-1..1にclamp）。</summary>
        [Test]
        public void NetSortieValue_DamageMinusRisk()
        {
            // 0.8 - 0.24 = 0.56
            Assert.AreEqual(0.56f, FortressSortieRules.NetSortieValue(0.8f, 0.24f), 1e-4f);
            // 0.2 - 0.5 = -0.3（損害が勝てば負）
            Assert.AreEqual(-0.3f, FortressSortieRules.NetSortieValue(0.2f, 0.5f), 1e-4f);
        }

        /// <summary>出撃可否＝好機と正味価値が閾値以上（既定0.1）。</summary>
        [Test]
        public void ShouldSortie_RequiresOpportunityAndValue()
        {
            // 好機0.4・正味0.56 → 打って出る
            Assert.IsTrue(FortressSortieRules.ShouldSortie(0.4f, 0.56f));
            // 好機が無ければ値打ちがあっても籠城
            Assert.IsFalse(FortressSortieRules.ShouldSortie(0.05f, 0.56f));
            // 正味が負なら出ない
            Assert.IsFalse(FortressSortieRules.ShouldSortie(0.4f, -0.3f));
        }

        /// <summary>
        /// 物語：イゼルローン要塞の攻防。攻囲側が混乱し要塞は即応万全＝好機。要塞砲の傘の下で出撃部隊が
        /// 攻囲線へ打撃を与え、退路は要塞至近で完全に安全＝損害ほぼ無し。正味価値は正で「打って出よ」。
        /// その後、敵増援が迫り戦況も進む＝引き上げの好機が高まり、深追いせず砲の傘の内へ戻る。
        /// </summary>
        [Test]
        public void Narrative_FortressGunsCoverDecisiveSortie()
        {
            // 攻囲側が崩れ要塞は即応万全
            float opp = FortressSortieRules.SortieOpportunity(0.9f, 1f); // 0.9
            Assert.Greater(opp, 0.5f);

            // 要塞砲の傘の下で戦う出撃部隊は掩護戦闘力が底上げされる
            float covered = FortressSortieRules.CoveredEngagement(0.7f, 1f); // 0.7*1.5=1.05
            float damage = FortressSortieRules.SiegeLineDamage(covered, 0.3f); // 1.05/(1.05+0.3)=0.7777..

            // 要塞至近で出撃＝退路は完全に安全→損害は出ない
            float security = FortressSortieRules.RetreatPathSecurity(0f, 1f); // 1
            float risk = FortressSortieRules.SortieRisk(0.7f, 0.8f, security); // 0
            Assert.AreEqual(0f, risk, 1e-4f);

            // 打撃が大きく損害が無い＝正味価値は正、好機も揃う→打って出よ
            float net = FortressSortieRules.NetSortieValue(damage, risk);
            Assert.Greater(net, 0.5f);
            Assert.IsTrue(FortressSortieRules.ShouldSortie(opp, net));

            // 敵増援が迫り戦況も進む＝引き上げの好機が高い（深追いせず戻る）
            float pullback = FortressSortieRules.WithdrawalTiming(0.8f, 1f); // 0.8*1.5=1.2→clamp1.0
            Assert.AreEqual(1f, pullback, 1e-4f);
        }
    }
}
