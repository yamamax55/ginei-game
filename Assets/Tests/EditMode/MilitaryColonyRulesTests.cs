using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 屯田制（軍事農業植民地・#1107）を固定する：兵農のトレードオフ＝農耕に回す兵が多く土地が肥沃なほど
    /// 食糧が自給できるが（FoodSelfSufficiency）戦闘即応性は落ちる（CombatReadinessPenalty）・自給ぶん補給線が
    /// 軽くなる（SupplyLineRelief）・屯田は育成期間を経て漸近的に成熟し初年は実らない（ColonyMaturityTick）・
    /// 根付けば占領地が自国領になる（PermanentSettlement）・自給拠点が前線を支える（StrategicDepth）。
    /// 既定Paramsの具体値で期待値を固定し、トレードオフとクランプを担保する。
    /// </summary>
    public class MilitaryColonyRulesTests
    {
        // 既定＝自給上限1.0/即応低下0.8/基礎即応0.2/成熟速度0.5/入植成熟0.8/定着年数3/縦深0.25
        private static readonly MilitaryColonyParams P = MilitaryColonyParams.Default;

        [Test]
        public void FoodSelfSufficiency_MoreFarmersAndFertileLand_FeedsMore()
        {
            // 全兵農耕・肥沃1・dt1 → 1.0×1×1×1＝1.0（完全自給）
            Assert.AreEqual(1f, MilitaryColonyRules.FoodSelfSufficiency(1f, 1f, 1f, P), 1e-4f);
            // 半分農耕・肥沃1 → 0.5（兵を畑に出すほど自給できる＝農側）
            Assert.AreEqual(0.5f, MilitaryColonyRules.FoodSelfSufficiency(0.5f, 1f, 1f, P), 1e-4f);
            // 全兵農耕・肥沃0.5 → 0.5（痩せた土地は実らない）
            Assert.AreEqual(0.5f, MilitaryColonyRules.FoodSelfSufficiency(1f, 0.5f, 1f, P), 1e-4f);
            // dt0（まだ実らない）→ 0
            Assert.AreEqual(0f, MilitaryColonyRules.FoodSelfSufficiency(1f, 1f, 0f, P), 1e-5f);
            // 入力過大はクランプ
            Assert.AreEqual(1f, MilitaryColonyRules.FoodSelfSufficiency(2f, 2f, 2f, P), 1e-4f);
        }

        [Test]
        public void CombatReadinessPenalty_FarmingTroopsAreSlowerToFight()
        {
            // 農耕割合0 → 1.0（全力即応）
            Assert.AreEqual(1f, MilitaryColonyRules.CombatReadinessPenalty(0f, P), 1e-4f);
            // 全員農耕 → 基礎即応0.2まで落ちる（drop＝1−1×0.8＝0.2 → 0.2＋0.8×0.2＝0.36）
            Assert.AreEqual(0.36f, MilitaryColonyRules.CombatReadinessPenalty(1f, P), 1e-4f);
            // 半分農耕 → drop＝1−0.5×0.8＝0.6 → 0.2＋0.8×0.6＝0.68
            Assert.AreEqual(0.68f, MilitaryColonyRules.CombatReadinessPenalty(0.5f, P), 1e-4f);
        }

        [Test]
        public void FoodVsReadiness_IsTradeoff()
        {
            // 農耕割合を上げると自給は増え即応は減る＝兵農のトレードオフ
            float lowFood = MilitaryColonyRules.FoodSelfSufficiency(0.3f, 1f, 1f, P);
            float highFood = MilitaryColonyRules.FoodSelfSufficiency(0.7f, 1f, 1f, P);
            float lowReady = MilitaryColonyRules.CombatReadinessPenalty(0.3f, P);
            float highReady = MilitaryColonyRules.CombatReadinessPenalty(0.7f, P);
            Assert.Greater(highFood, lowFood);   // 農耕を増やすと自給↑
            Assert.Less(highReady, lowReady);    // 同時に即応↓
        }

        [Test]
        public void SupplyLineRelief_CappedAtGarrisonDemand()
        {
            // 自給0.4・需要100 → 40だけ後方輸送が要らない
            Assert.AreEqual(40f, MilitaryColonyRules.SupplyLineRelief(0.4f, 100f), 1e-4f);
            // 完全自給 → 需要まるごと不要
            Assert.AreEqual(100f, MilitaryColonyRules.SupplyLineRelief(1f, 100f), 1e-4f);
            // 自給率は0..1にクランプ・負需要は0
            Assert.AreEqual(100f, MilitaryColonyRules.SupplyLineRelief(1.5f, 100f), 1e-4f);
            Assert.AreEqual(0f, MilitaryColonyRules.SupplyLineRelief(0.5f, -10f), 1e-5f);
        }

        [Test]
        public void ColonyMaturityTick_GrowsAsymptotically_FirstYearNotFull()
        {
            // 開墾直後0・dt1 → (1−0)×0.5×1＝0.5（初年は半分しか実らない＝育成期間）
            float y1 = MilitaryColonyRules.ColonyMaturityTick(0f, 1f, P);
            Assert.AreEqual(0.5f, y1, 1e-4f);
            // 翌年 → 0.5＋(1−0.5)×0.5＝0.75（漸近的に成熟）
            float y2 = MilitaryColonyRules.ColonyMaturityTick(y1, 1f, P);
            Assert.AreEqual(0.75f, y2, 1e-4f);
            // dt0 は据え置き・上限1
            Assert.AreEqual(0.6f, MilitaryColonyRules.ColonyMaturityTick(0.6f, 0f, P), 1e-5f);
            Assert.AreEqual(1f, MilitaryColonyRules.ColonyMaturityTick(0.99f, 100f, P), 1e-4f);
        }

        [Test]
        public void PermanentSettlement_NeedsBothMaturityAndYears()
        {
            // 成熟0.8以上かつ定着3年以上で根付く（占領地が自国領に）
            Assert.IsTrue(MilitaryColonyRules.PermanentSettlement(0.8f, 3f, P));
            Assert.IsTrue(MilitaryColonyRules.PermanentSettlement(1f, 5f, P));
            // 成熟が足りない（育ち切らぬ屯田は根付かない）
            Assert.IsFalse(MilitaryColonyRules.PermanentSettlement(0.7f, 5f, P));
            // 年数が足りない（時間が要る）
            Assert.IsFalse(MilitaryColonyRules.PermanentSettlement(1f, 2f, P));
        }

        [Test]
        public void StrategicDepth_SelfSufficientColoniesSupportTheFront()
        {
            // 拠点4・完全自給 → 4×0.25×1＝1.0（前線を支える縦深）
            Assert.AreEqual(1f, MilitaryColonyRules.StrategicDepth(4, 1f, P), 1e-4f);
            // 自給半分 → 4×0.25×0.5＝0.5（食えない拠点ほど支えが薄い）
            Assert.AreEqual(0.5f, MilitaryColonyRules.StrategicDepth(4, 0.5f, P), 1e-4f);
            // 拠点ゼロ・自給ゼロは縦深ゼロ
            Assert.AreEqual(0f, MilitaryColonyRules.StrategicDepth(0, 1f, P), 1e-5f);
            Assert.AreEqual(0f, MilitaryColonyRules.StrategicDepth(4, 0f, P), 1e-5f);
            // 負の拠点数はクランプ
            Assert.AreEqual(0f, MilitaryColonyRules.StrategicDepth(-5, 1f, P), 1e-5f);
        }
    }
}
