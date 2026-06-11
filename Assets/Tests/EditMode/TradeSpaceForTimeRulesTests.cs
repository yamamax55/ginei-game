using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦略的受動撤退ドクトリン（空間を時間で買う・#1421）の純ロジック検証。
    /// 既定 Params（空間時間効率0.8／敵圧崩し0.6／過伸張誘発0.7／時間価値0.6／消耗速度0.2／政治代償0.5／反攻好機0.7）で期待値固定。
    /// </summary>
    public class TradeSpaceForTimeRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>空間を時間に＝譲る土地×戦略縦深×効率。広大な国土ほど時間を稼げる。</summary>
        [Test]
        public void SpaceForTime_土地と縦深を時間に換える()
        {
            // 0.5×0.5×0.8 = 0.2
            Assert.AreEqual(0.2f, TradeSpaceForTimeRules.SpaceForTimeRate(0.5f, 0.5f), Eps);
            // 縦深ゼロは時間に換わらない
            Assert.AreEqual(0f, TradeSpaceForTimeRules.SpaceForTimeRate(1f, 0f), Eps);
            // 広大（縦深1）に多く譲れば最大効率
            Assert.AreEqual(0.8f, TradeSpaceForTimeRules.SpaceForTimeRate(1f, 1f), Eps);
        }

        /// <summary>決戦の回避＝撤退規律が敵圧で損なわれる。捕捉されると計画的撤退が崩れる。</summary>
        [Test]
        public void DecisiveBattleAvoidance_敵圧が規律を崩す()
        {
            // 敵圧ゼロなら規律そのまま
            Assert.AreEqual(0.9f, TradeSpaceForTimeRules.DecisiveBattleAvoidance(0.9f, 0f), Eps);
            // 0.9×(1−0.6×0.5)=0.9×0.7=0.63
            Assert.AreEqual(0.63f, TradeSpaceForTimeRules.DecisiveBattleAvoidance(0.9f, 0.5f), Eps);
            // 敵圧が高いほど規律は損なわれる（単調減少）
            float low = TradeSpaceForTimeRules.DecisiveBattleAvoidance(0.9f, 0.3f);
            float high = TradeSpaceForTimeRules.DecisiveBattleAvoidance(0.9f, 0.8f);
            Assert.Less(high, low);
        }

        /// <summary>敵の過伸張誘発＝空間時間レート×敵の進撃欲。深追いするほど補給線が伸びきる。</summary>
        [Test]
        public void EnemyOverextension_奥地へ誘い込み補給線を伸ばす()
        {
            // 0.5×0.8×0.7 = 0.28
            Assert.AreEqual(0.28f, TradeSpaceForTimeRules.EnemyOverextensionInduced(0.5f, 0.8f), Eps);
            // 進撃欲ゼロ（深追いしない敵）は伸びきらない
            Assert.AreEqual(0f, TradeSpaceForTimeRules.EnemyOverextensionInduced(0.5f, 0f), Eps);
        }

        /// <summary>稼いだ時間の価値＝動員が進むほど時間が活きる。時間が味方になる。</summary>
        [Test]
        public void TimeBought_動員が進むほど時間が活きる()
        {
            // 動員0：rate0.5×(1−0.6×1)=0.5×0.4=0.2
            Assert.AreEqual(0.2f, TradeSpaceForTimeRules.TimeBoughtValue(0.5f, 0f), Eps);
            // 動員1：rate0.5×1.0=0.5（時間が最大限に活きる）
            Assert.AreEqual(0.5f, TradeSpaceForTimeRules.TimeBoughtValue(0.5f, 1f), Eps);
            // 動員が進むほど価値が増す
            float early = TradeSpaceForTimeRules.TimeBoughtValue(0.5f, 0.2f);
            float late = TradeSpaceForTimeRules.TimeBoughtValue(0.5f, 0.8f);
            Assert.Less(early, late);
        }

        /// <summary>戦わず消耗＝敵過伸張×遊撃×速度×dt。会戦せず補給難・遊撃で削る。</summary>
        [Test]
        public void AttritionWithoutBattle_決戦せず敵を削る()
        {
            // 0.8×0.5×0.2×1 = 0.08
            Assert.AreEqual(0.08f, TradeSpaceForTimeRules.AttritionWithoutBattle(0.8f, 0.5f, 1f), Eps);
            // dt 比例（2倍の時間で2倍削る）
            Assert.AreEqual(0.16f, TradeSpaceForTimeRules.AttritionWithoutBattle(0.8f, 0.5f, 2f), Eps);
            // 敵が伸びきっていない（過伸張ゼロ）なら削れない
            Assert.AreEqual(0f, TradeSpaceForTimeRules.AttritionWithoutBattle(0f, 1f, 1f), Eps);
        }

        /// <summary>退却の政治的代償＝譲る土地×（士気の低さ）。国民・宮廷の非難。</summary>
        [Test]
        public void PoliticalCost_土地を譲る代償()
        {
            // 0.6×(1−0.4)×0.5 = 0.6×0.6×0.5 = 0.18
            Assert.AreEqual(0.18f, TradeSpaceForTimeRules.PoliticalCostOfRetreat(0.6f, 0.4f), Eps);
            // 国民士気が高ければ非難は小さい（単調減少）
            float lowMorale = TradeSpaceForTimeRules.PoliticalCostOfRetreat(0.6f, 0.2f);
            float highMorale = TradeSpaceForTimeRules.PoliticalCostOfRetreat(0.6f, 0.9f);
            Assert.Less(highMorale, lowMorale);
            // 士気満点なら代償なし
            Assert.AreEqual(0f, TradeSpaceForTimeRules.PoliticalCostOfRetreat(1f, 1f), Eps);
        }

        /// <summary>反攻の好機＝敵の終末点到達×自軍残存。誘い込んだ敵を叩く好機。</summary>
        [Test]
        public void CounterOffensive_終末点で反攻の窓が開く()
        {
            // 0.9×0.8×0.7 = 0.504
            Assert.AreEqual(0.504f, TradeSpaceForTimeRules.CounterOffensiveWindow(0.9f, 0.8f), Eps);
            // 敵がまだ伸びきっていない（終末点未到達）なら好機なし
            Assert.AreEqual(0f, TradeSpaceForTimeRules.CounterOffensiveWindow(0f, 1f), Eps);
            // 自軍が消尽していれば反攻できない
            Assert.AreEqual(0f, TradeSpaceForTimeRules.CounterOffensiveWindow(1f, 0f), Eps);
        }

        /// <summary>弾力的縦深防御の判定＝決戦回避と空間時間レートがともに閾値以上で成立。</summary>
        [Test]
        public void IsElasticDefense_計画的撤退と時間獲得の両立()
        {
            // 両方が閾値0.4以上＝弾力的防御が機能
            Assert.IsTrue(TradeSpaceForTimeRules.IsElasticDefense(0.6f, 0.5f, 0.4f));
            // 空間時間レートが閾値を割る＝無為な土地喪失
            Assert.IsFalse(TradeSpaceForTimeRules.IsElasticDefense(0.6f, 0.3f, 0.4f));
            // 決戦回避が閾値を割る＝潰走
            Assert.IsFalse(TradeSpaceForTimeRules.IsElasticDefense(0.3f, 0.6f, 0.4f));
        }
    }
}
