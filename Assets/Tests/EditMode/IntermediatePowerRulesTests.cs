using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// IntermediatePowerRules（MONT-4 #1446・モンテスキューの中間権力）の純ロジックテスト。
    /// 既定 Params 具体値で期待値を固定する。
    /// </summary>
    public class IntermediatePowerRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>中間権力の総合強度＝貴族/聖職者/法院/都市の加重平均（既定重み）。</summary>
        [Test]
        public void IntermediateStrength_重み付き平均で総合強度を出す()
        {
            // 0.6*0.35 + 0.4*0.2 + 0.8*0.3 + 0.2*0.15 = 0.56（wSum=1）
            float s = IntermediatePowerRules.IntermediateStrength(0.6f, 0.4f, 0.8f, 0.2f);
            Assert.AreEqual(0.56f, s, Eps);
            // 全団体が満杯なら総合強度も1
            Assert.AreEqual(1f, IntermediatePowerRules.IntermediateStrength(1f, 1f, 1f, 1f), Eps);
            // 中間団体が皆無なら緩衝層なし
            Assert.AreEqual(0f, IntermediatePowerRules.IntermediateStrength(0f, 0f, 0f, 0f), Eps);
        }

        /// <summary>専制への緩衝＝総合強度×bufferScale（0.9）。厚い中間権力は専制を緩衝する。</summary>
        [Test]
        public void BufferAgainstDespotism_中間権力が専制を緩衝する()
        {
            Assert.AreEqual(0.504f, IntermediatePowerRules.BufferAgainstDespotism(0.56f), Eps);
            // 中間権力ゼロなら緩衝ゼロ＝専制を止める者がいない
            Assert.AreEqual(0f, IntermediatePowerRules.BufferAgainstDespotism(0f), Eps);
        }

        /// <summary>専制への滑落リスク＝緩衝が薄く君主の野心が強いほど高い。</summary>
        [Test]
        public void DespotismSlideRisk_緩衝が薄く野心が強いほど専制へ滑る()
        {
            // 強度0.56→緩衝0.504、野心0.8 → (1-0.504)*0.8 = 0.3968
            float risk = IntermediatePowerRules.DespotismSlideRisk(0.56f, 0.8f);
            Assert.AreEqual(0.3968f, risk, Eps);
            // 中間権力が厚いほど（同じ野心でも）リスクは下がる
            float strongRisk = IntermediatePowerRules.DespotismSlideRisk(0.9f, 0.8f);
            Assert.Less(strongRisk, risk);
            // 君主に野心が無ければ滑落しない
            Assert.AreEqual(0f, IntermediatePowerRules.DespotismSlideRisk(0.2f, 0f), Eps);
        }

        /// <summary>中央集権化の圧力が中間権力を侵食する（君主が貴族・法院を潰す）。</summary>
        [Test]
        public void IntermediateErosionTick_中央集権化が中間権力を破壊する()
        {
            // 0.5 - 0.06*0.8*1.0 = 0.452
            float eroded = IntermediatePowerRules.IntermediateErosionTick(0.5f, 0.8f, 1f);
            Assert.AreEqual(0.452f, eroded, Eps);
            // 圧力ゼロ・dtゼロなら不変（基準非破壊）
            Assert.AreEqual(0.5f, IntermediatePowerRules.IntermediateErosionTick(0.5f, 0f, 1f), Eps);
            Assert.AreEqual(0.5f, IntermediatePowerRules.IntermediateErosionTick(0.5f, 0.8f, 0f), Eps);
        }

        /// <summary>中間権力を失った君主政は専制と区別がつかなくなる（モンテスキューの警告）。</summary>
        [Test]
        public void MonarchyWithoutIntermediaries_中間権力を失えば実質専制()
        {
            // 閾値0.3：強度0.2は専制化、0.5は君主政のまま
            Assert.IsTrue(IntermediatePowerRules.MonarchyWithoutIntermediaries(0.2f, 0.3f));
            Assert.IsFalse(IntermediatePowerRules.MonarchyWithoutIntermediaries(0.5f, 0.3f));
        }

        /// <summary>法的中間権力＝高等法院×法の伝統が恣意的支配を阻む（法による緩衝）。</summary>
        [Test]
        public void LegalChannelStrength_法院と法の伝統で恣意を阻む()
        {
            Assert.AreEqual(0.4f, IntermediatePowerRules.LegalChannelStrength(0.8f, 0.5f), Eps);
            // 法の伝統が無ければ法院があっても機能しない（積）
            Assert.AreEqual(0f, IntermediatePowerRules.LegalChannelStrength(0.8f, 0f), Eps);
        }

        /// <summary>貴族の特権が逆説的に専制への防壁になる（特権層が王に抵抗する）。</summary>
        [Test]
        public void PrivilegeAsBulwark_特権が専制への防壁になる()
        {
            Assert.AreEqual(0.7f, IntermediatePowerRules.PrivilegeAsBulwark(0.7f), Eps);
            // 特権ゼロなら抵抗の足場もない、クランプも効く
            Assert.AreEqual(0f, IntermediatePowerRules.PrivilegeAsBulwark(0f), Eps);
            Assert.AreEqual(1f, IntermediatePowerRules.PrivilegeAsBulwark(1.5f), Eps);
        }

        /// <summary>緩衝に縛られた穏健な君主政の判定（閾値0.4）。</summary>
        [Test]
        public void IsConstrainedMonarchy_緩衝が厚ければ穏健な君主政()
        {
            // 緩衝0.504は閾値0.4以上＝穏健な君主政
            float buffer = IntermediatePowerRules.BufferAgainstDespotism(0.56f);
            Assert.IsTrue(IntermediatePowerRules.IsConstrainedMonarchy(buffer, 0.4f));
            // 緩衝が薄ければ穏健と言えない
            Assert.IsFalse(IntermediatePowerRules.IsConstrainedMonarchy(0.2f, 0.4f));
        }
    }
}
