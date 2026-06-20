using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 私掠（認可された海賊＝民間の通商破壊）：私掠免許の動機・拿捕の成否・鹵獲の価値・敵交易への打撃・
    /// コスト削減・規律問題（海賊化）・国際的非難・正味価値。既定 Params で期待値固定（1e-4f）。
    /// </summary>
    public class PrivateeringRulesTests
    {
        [Test]
        public void LetterOfMarqueIncentive_ShareTimesRichness()
        {
            Assert.AreEqual(0.4f, PrivateeringRules.LetterOfMarqueIncentive(0.5f, 0.8f), 1e-4f); // 0.5×0.8
            Assert.AreEqual(0f, PrivateeringRules.LetterOfMarqueIncentive(0f, 0.9f), 1e-4f);     // 分け前0=動機なし
            Assert.AreEqual(1.0f, PrivateeringRules.LetterOfMarqueIncentive(1.5f, 1.5f), 1e-4f); // 過入力はclamp
        }

        [Test]
        public void PrizeCapture_StrengthOverDefense()
        {
            Assert.AreEqual(0.75f, PrivateeringRules.PrizeCapture(300f, 100f), 1e-4f); // 300/400
            Assert.AreEqual(0.5f, PrivateeringRules.PrizeCapture(50f, 50f), 1e-4f);    // 互角
            Assert.AreEqual(1.0f, PrivateeringRules.PrizeCapture(10f, 0f), 1e-4f);     // 無防備=確実に拿捕
        }

        [Test]
        public void PrizeValue_CaptureTimesCargo()
        {
            Assert.AreEqual(150f, PrivateeringRules.PrizeValue(0.75f, 200f), 1e-4f); // 0.75×200
            Assert.AreEqual(0f, PrivateeringRules.PrizeValue(0f, 500f), 1e-4f);      // 拿捕失敗は無価値
        }

        [Test]
        public void EnemyCommerceDamage_CountTimesCapture()
        {
            Assert.AreEqual(2.0f, PrivateeringRules.EnemyCommerceDamage(4, 0.5f), 1e-4f); // 4×0.5×1.0
            Assert.AreEqual(0f, PrivateeringRules.EnemyCommerceDamage(0, 1.0f), 1e-4f);   // 私掠船0=打撃なし
        }

        [Test]
        public void StateCostSaving_CountTimesBudget()
        {
            Assert.AreEqual(300f, PrivateeringRules.StateCostSaving(3, 100f), 1e-4f); // 3×100×1.0
            Assert.AreEqual(0f, PrivateeringRules.StateCostSaving(5, -10f), 1e-4f);   // 負予算はclamp
        }

        [Test]
        public void DisciplineProblem_ShareTimesLooseControl()
        {
            Assert.AreEqual(0.6f, PrivateeringRules.DisciplineProblem(0.8f, 0.25f), 1e-4f); // 0.8×(1-0.25)
            Assert.AreEqual(0f, PrivateeringRules.DisciplineProblem(0.9f, 1.0f), 1e-4f);    // 完全統制=海賊化なし
        }

        [Test]
        public void InternationalCondemnation_ExcessTimesNeutralHarm()
        {
            Assert.AreEqual(0.3f, PrivateeringRules.InternationalCondemnation(0.5f, 0.6f), 1e-4f); // 0.5×0.6
            Assert.AreEqual(0f, PrivateeringRules.InternationalCondemnation(0.9f, 0f), 1e-4f);     // 中立船被害なし=非難なし
        }

        [Test]
        public void NetPrivateeringValue_BenefitMinusCondemnation()
        {
            // 0.4+0.3-0.5＝0.2
            Assert.AreEqual(0.2f, PrivateeringRules.NetPrivateeringValue(0.4f, 0.3f, 0.5f), 1e-4f);
            // 打撃も節約も小さく非難が大きい＝割に合わない（負）
            Assert.AreEqual(-0.9f, PrivateeringRules.NetPrivateeringValue(0.1f, 0f, 1.0f), 1e-4f);
            // 便益が大きく非難なし＝上限clamp
            Assert.AreEqual(1.0f, PrivateeringRules.NetPrivateeringValue(0.9f, 0.9f, 0f), 1e-4f);
        }

        [Test]
        public void Narrative_RichTradeLuresPrivateersThenPiracyAndCondemnation()
        {
            // 豊かな敵交易＋厚い分け前で私掠免許に応募が殺到する
            float incentive = PrivateeringRules.LetterOfMarqueIncentive(0.8f, 0.9f);
            Assert.Greater(incentive, 0.6f);

            // 無防備な商船を高確率で拿捕し、積荷を鹵獲する
            float capture = PrivateeringRules.PrizeCapture(200f, 40f); // 200/240
            Assert.Greater(capture, 0.8f);
            float prize = PrivateeringRules.PrizeValue(capture, 150f);
            Assert.Greater(prize, 100f);

            // 多数の私掠船が敵交易を干上がらせ、正規海軍を肩代わりして国費を浮かす
            float damage = PrivateeringRules.EnemyCommerceDamage(6, capture);
            float saving = PrivateeringRules.StateCostSaving(6, 50f);
            Assert.Greater(damage, 0f);
            Assert.Greater(saving, 0f);

            // だが厚い分け前＋緩い統制は海賊化を招く
            float discipline = PrivateeringRules.DisciplineProblem(0.8f, 0.2f);
            Assert.Greater(discipline, 0.5f);

            // 行き過ぎて中立船まで襲うと国際的非難を浴びる
            float condemnation = PrivateeringRules.InternationalCondemnation(0.7f, 0.6f);
            Assert.Greater(condemnation, 0.3f);

            // それでも打撃＋節約が非難を上回れば私掠は割に合う（正味プラス）
            float net = PrivateeringRules.NetPrivateeringValue(0.6f, 0.4f, condemnation);
            Assert.Greater(net, 0f);
        }
    }
}
