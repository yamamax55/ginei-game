using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 残骸宙域を固定する：撃沈数×激戦度で残骸が積もり、それが索敵を曇らせ（擾乱）航行を阻む（衝突）が、
    /// 同時に遮蔽と隠密接近の隠れ蓑になり、回収すれば資源になる。生存者は残骸と時間に呑まれる。
    /// 既定 Params の期待値と境界（clamp・ゼロ割回避）を担保。
    /// </summary>
    public class DebrisFieldRulesTests
    {
        private static readonly DebrisFieldParams P = DebrisFieldParams.Default;
        // 蓄積0.01/隻・擾乱上限1.0・衝突1.0・遮蔽0.8・接近1.0・救助難化1.0・回収1.0・ε0.001

        [Test]
        public void DebrisAccumulation_ShipsTimesIntensity()
        {
            // 50隻×激戦1×0.01 = 0.5
            Assert.AreEqual(0.5f, DebrisFieldRules.DebrisAccumulation(50, 1f, P), 1e-4f);
            // 半端な激戦度：50隻×0.5×0.01 = 0.25
            Assert.AreEqual(0.25f, DebrisFieldRules.DebrisAccumulation(50, 0.5f, P), 1e-4f);
            // 撃沈ゼロ＝残骸なし
            Assert.AreEqual(0f, DebrisFieldRules.DebrisAccumulation(0, 1f, P), 1e-4f);
            // 大殲滅は上限1にclamp（200隻×1×0.01=2 → 1）
            Assert.AreEqual(1f, DebrisFieldRules.DebrisAccumulation(200, 1f, P), 1e-4f);
            // 負の撃沈は0にclamp
            Assert.AreEqual(0f, DebrisFieldRules.DebrisAccumulation(-10, 1f, P), 1e-4f);
        }

        [Test]
        public void SensorClutter_DebrisOverDebrisPlusFilter()
        {
            // 残骸0.5/(0.5+0.5+ε) ≈ 0.4995
            Assert.AreEqual(0.4995f, DebrisFieldRules.SensorClutter(0.5f, 0.5f, P), 1e-4f);
            // フィルタが強い（2.0）と擾乱が下がる：0.5/(0.5+2.0+ε) ≈ 0.19992
            Assert.AreEqual(0.19992f, DebrisFieldRules.SensorClutter(0.5f, 2f, P), 1e-4f);
            // 残骸ゼロ＝擾乱ゼロ
            Assert.AreEqual(0f, DebrisFieldRules.SensorClutter(0f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void CollisionHazard_DebrisTimesSpeed()
        {
            // 残骸0.5×速度0.5×1.0 = 0.25
            Assert.AreEqual(0.25f, DebrisFieldRules.CollisionHazard(0.5f, 0.5f, P), 1e-4f);
            // 最大残骸×最大速度 = 1
            Assert.AreEqual(1f, DebrisFieldRules.CollisionHazard(1f, 1f, P), 1e-4f);
            // 停止すれば衝突せず
            Assert.AreEqual(0f, DebrisFieldRules.CollisionHazard(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void DebrisCover_ProportionalToDebris()
        {
            // 残骸0.5×0.8 = 0.4
            Assert.AreEqual(0.4f, DebrisFieldRules.DebrisCover(0.5f, P), 1e-4f);
            // 満杯×0.8 = 0.8
            Assert.AreEqual(0.8f, DebrisFieldRules.DebrisCover(1f, P), 1e-4f);
            Assert.AreEqual(0f, DebrisFieldRules.DebrisCover(0f, P), 1e-4f);
        }

        [Test]
        public void HiddenApproach_ClutterTimesCover()
        {
            // 擾乱0.5×遮蔽0.4×1.0 = 0.2
            Assert.AreEqual(0.2f, DebrisFieldRules.HiddenApproach(0.5f, 0.4f, P), 1e-4f);
            // どちらかゼロなら隠れられない
            Assert.AreEqual(0f, DebrisFieldRules.HiddenApproach(0f, 1f, P), 1e-4f);
            Assert.AreEqual(0f, DebrisFieldRules.HiddenApproach(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void SurvivorRescue_EffortOverDebrisTimesTime()
        {
            // 努力1/(努力1+残骸0.5×時間1×1+ε) = 1/1.501 ≈ 0.66622
            Assert.AreEqual(0.66622f, DebrisFieldRules.SurvivorRescue(1f, 0.5f, 1f, P), 1e-4f);
            // 時間が経つほど救助率が落ちる：1/(1+0.5×4+ε) = 1/3.001 ≈ 0.33322
            Assert.AreEqual(0.33322f, DebrisFieldRules.SurvivorRescue(1f, 0.5f, 4f, P), 1e-4f);
            // 救助努力ゼロ＝誰も救えない
            Assert.AreEqual(0f, DebrisFieldRules.SurvivorRescue(0f, 0.5f, 1f, P), 1e-4f);
            // 残骸も時間もゼロ＝努力がそのまま満点近く：1/(1+ε) ≈ 0.999
            Assert.AreEqual(0.999f, DebrisFieldRules.SurvivorRescue(1f, 0f, 0f, P), 1e-3f);
        }

        [Test]
        public void SalvageYield_DebrisTimesCapacity()
        {
            // 残骸0.5×回収0.5×1.0 = 0.25
            Assert.AreEqual(0.25f, DebrisFieldRules.SalvageYield(0.5f, 0.5f, P), 1e-4f);
            // 残骸満杯×回収満杯 = 1
            Assert.AreEqual(1f, DebrisFieldRules.SalvageYield(1f, 1f, P), 1e-4f);
            // 回収能力ゼロ＝何も拾えない
            Assert.AreEqual(0f, DebrisFieldRules.SalvageYield(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void IsFieldNavigable_HazardUnderThreshold()
        {
            Assert.IsTrue(DebrisFieldRules.IsFieldNavigable(0.3f, 0.5f));
            Assert.IsTrue(DebrisFieldRules.IsFieldNavigable(0.5f, 0.5f));  // 閾値ちょうどは可
            Assert.IsFalse(DebrisFieldRules.IsFieldNavigable(0.6f, 0.5f));
            // 既定閾値委譲版（0.5）
            Assert.IsTrue(DebrisFieldRules.IsFieldNavigable(0.4f));
            Assert.IsFalse(DebrisFieldRules.IsFieldNavigable(0.7f));
        }

        [Test]
        public void Story_AnnihilationLeavesAHauntedField()
        {
            // 殲滅級の会戦：100隻撃沈×激戦1 → 残骸が満ちる
            float debris = DebrisFieldRules.DebrisAccumulation(100, 1f, P);
            Assert.AreEqual(1f, debris, 1e-4f);

            // 平凡なセンサー（フィルタ0.5）では残骸に紛れて索敵がほぼ機能不全
            float clutter = DebrisFieldRules.SensorClutter(debris, 0.5f, P);
            Assert.Greater(clutter, 0.6f);

            // 残骸を盾に身を隠せる（遮蔽が高い）
            float cover = DebrisFieldRules.DebrisCover(debris, P);
            Assert.Greater(cover, 0.7f);

            // 曇った索敵＋濃い遮蔽 → 残骸に紛れた隠密接近が成立
            float approach = DebrisFieldRules.HiddenApproach(clutter, cover, P);
            Assert.Greater(approach, 0.4f);

            // だが全速で突っ込めば衝突必至＝航行不能
            float hazard = DebrisFieldRules.CollisionHazard(debris, 1f, P);
            Assert.IsFalse(DebrisFieldRules.IsFieldNavigable(hazard));

            // 漂流する生存者は時間が経つほど救えなくなる
            float rescueEarly = DebrisFieldRules.SurvivorRescue(1f, debris, 1f, P);
            float rescueLate = DebrisFieldRules.SurvivorRescue(1f, debris, 10f, P);
            Assert.Greater(rescueEarly, rescueLate);

            // 一方で残骸は回収すれば資源になる（戦場の負の遺産が次の糧）
            float salvage = DebrisFieldRules.SalvageYield(debris, 0.8f, P);
            Assert.Greater(salvage, 0.7f);
        }
    }
}
