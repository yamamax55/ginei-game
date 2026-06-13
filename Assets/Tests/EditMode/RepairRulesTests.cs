using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 修理を固定する：応急修理は最大戦力×上限割合まで・ドック修理だけが完全回復・完全回復所要時間・
    /// ドック要否の境界。クランプと速度ゼロの無限大を担保。
    /// </summary>
    public class RepairRulesTests
    {
        private static readonly RepairParams P = RepairParams.Default; // 応急上限0.7/応急速度1/ドック係数1

        [Test]
        public void FieldRepairCeiling_FractionOfMax()
        {
            Assert.AreEqual(70f, RepairRules.FieldRepairCeiling(100f, P), 1e-4f);
        }

        [Test]
        public void FieldRepairTick_HealsTowardCeilingOnly()
        {
            // 50 → 60（1×10dt）まで回復、上限70 で頭打ち
            Assert.AreEqual(60f, RepairRules.FieldRepairTick(50f, 100f, 10f, P), 1e-4f);
            Assert.AreEqual(70f, RepairRules.FieldRepairTick(50f, 100f, 100f, P), 1e-4f);
            // 既に上限以上なら据え置き（応急では削らない）
            Assert.AreEqual(80f, RepairRules.FieldRepairTick(80f, 100f, 10f, P), 1e-4f);
        }

        [Test]
        public void DockRepairTick_HealsToFull()
        {
            // 設備力5×dt10=50 回復、最大100 まで
            Assert.AreEqual(100f, RepairRules.DockRepairTick(80f, 100f, 5f, 10f, P), 1e-4f);
            Assert.AreEqual(90f, RepairRules.DockRepairTick(80f, 100f, 1f, 10f, P), 1e-4f);
        }

        [Test]
        public void TimeToFull_DeficitOverRate()
        {
            Assert.AreEqual(20f, RepairRules.TimeToFull(80f, 100f, 1f, P), 1e-4f);
            Assert.AreEqual(0f, RepairRules.TimeToFull(100f, 100f, 1f, P), 1e-5f);   // 無傷＝0
            Assert.IsTrue(float.IsPositiveInfinity(RepairRules.TimeToFull(50f, 100f, 0f, P))); // 設備なし＝永遠
        }

        [Test]
        public void NeedsDock_OnlyAboveFieldCeilingButDamaged()
        {
            Assert.IsTrue(RepairRules.NeedsDock(80f, 100f, P));   // 応急上限70超・損傷あり＝ドック行き
            Assert.IsTrue(RepairRules.NeedsDock(70f, 100f, P));   // ちょうど上限＝応急では進まない
            Assert.IsFalse(RepairRules.NeedsDock(50f, 100f, P));  // まだ応急で戻せる
            Assert.IsFalse(RepairRules.NeedsDock(100f, 100f, P)); // 無傷
        }
    }
}
