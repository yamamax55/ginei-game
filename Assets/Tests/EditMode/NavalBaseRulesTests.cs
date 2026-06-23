using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>NavalBaseRules（軍事根拠地の戦略価値）の EditMode テスト。既定 Params の具体値で期待値を固定。</summary>
    public class NavalBaseRulesTests
    {
        const float Eps = 1e-4f;     // 四則のみ
        const float EpsPow = 1e-3f;  // Sqrt/Lerp を含む箇所

        [Test]
        public void BasePositionValue_重み付き平均()
        {
            // (0.6*0.8 + 0.4*0.6)/1.0 = 0.72
            Assert.AreEqual(0.72f, NavalBaseRules.BasePositionValue(0.8f, 0.6f), Eps);
        }

        [Test]
        public void BasePositionValue_入力をクランプ()
        {
            // strategic>1→1, frontier<0→0 ＝ (0.6*1 + 0.4*0)/1.0 = 0.6
            Assert.AreEqual(0.6f, NavalBaseRules.BasePositionValue(2f, -1f), Eps);
        }

        [Test]
        public void FleetSustainmentCapacity_整備と補給の幾何平均()
        {
            // sqrt(0.9*0.4) = sqrt(0.36) = 0.6
            Assert.AreEqual(0.6f, NavalBaseRules.FleetSustainmentCapacity(0.9f, 0.4f), EpsPow);
            // どちらか0なら維持できない
            Assert.AreEqual(0f, NavalBaseRules.FleetSustainmentCapacity(1f, 0f), EpsPow);
        }

        [Test]
        public void OperationalRadius_維持と燃料の積と下限()
        {
            // 0.6*0.5 = 0.30（下限0.1を上回る）
            Assert.AreEqual(0.30f, NavalBaseRules.OperationalRadius(0.6f, 0.5f), Eps);
            // 維持・燃料0でも下限0.1が残る
            Assert.AreEqual(0.1f, NavalBaseRules.OperationalRadius(0f, 0f), Eps);
        }

        [Test]
        public void ForwardBaseValue_位置と行動半径の幾何平均()
        {
            // sqrt(0.72*0.30) = sqrt(0.216) = 0.464758...
            Assert.AreEqual(0.464758f, NavalBaseRules.ForwardBaseValue(0.72f, 0.30f), EpsPow);
        }

        [Test]
        public void BaseDependency_狭い半径と遠距離で依存深まる()
        {
            // (1-0.30)*0.8 = 0.56
            Assert.AreEqual(0.56f, NavalBaseRules.BaseDependency(0.30f, 0.8f), Eps);
            // 行動半径が満タンなら依存ゼロ
            Assert.AreEqual(0f, NavalBaseRules.BaseDependency(1f, 1f), Eps);
        }

        [Test]
        public void Fortification_施設と守備の重み付き平均()
        {
            // (0.5*0.8 + 0.5*0.6)/1.0 = 0.7
            Assert.AreEqual(0.7f, NavalBaseRules.Fortification(0.8f, 0.6f), Eps);
        }

        [Test]
        public void BaseLossImpact_維持能力と依存度の積()
        {
            // 0.6*0.56 = 0.336
            Assert.AreEqual(0.336f, NavalBaseRules.BaseLossImpact(0.6f, 0.56f), Eps);
        }

        [Test]
        public void StrategicBaseValue_要塞化で底上げ喪失で減点()
        {
            // 0.464758 * Lerp(0.5,1,0.7) - 0.5*0.336
            //  = 0.464758 * 0.85 - 0.168 = 0.395045 - 0.168 = 0.227045
            Assert.AreEqual(0.227045f, NavalBaseRules.StrategicBaseValue(0.464758f, 0.7f, 0.336f), EpsPow);
        }

        [Test]
        public void StrategicBaseValue_喪失リスク過大で0にクランプ()
        {
            // forward低・fort低・loss最大 → 負になるが0へクランプ
            Assert.AreEqual(0f, NavalBaseRules.StrategicBaseValue(0.1f, 0f, 1f), EpsPow);
        }

        // 物語テスト：イゼルローン要塞型の根拠地が高い戦略価値を持つ
        [Test]
        public void 物語_イゼルローン型要塞は最前線の堅固な拠点として高価値()
        {
            var p = NavalBaseParams.Default;

            // 回廊の要・前線直結＝位置価値が高い
            float pos = NavalBaseRules.BasePositionValue(1f, 1f, p);
            Assert.AreEqual(1f, pos, Eps);

            // 整備・補給が充実＝維持能力が高い
            float sustain = NavalBaseRules.FleetSustainmentCapacity(0.9f, 0.9f, p);
            // 燃料兵站も潤沢＝広い行動半径
            float radius = NavalBaseRules.OperationalRadius(sustain, 0.9f, p);
            float forward = NavalBaseRules.ForwardBaseValue(pos, radius, p);

            // 要塞砲＋大守備隊＝堅固
            float fort = NavalBaseRules.Fortification(1f, 1f, p);
            Assert.AreEqual(1f, fort, Eps);

            // 母港から動かない＝距離ゼロで依存・喪失打撃は限定的
            float dep = NavalBaseRules.BaseDependency(radius, 0f, p);
            float loss = NavalBaseRules.BaseLossImpact(sustain, dep, p);
            Assert.AreEqual(0f, dep, Eps);
            Assert.AreEqual(0f, loss, Eps);

            float izerlohn = NavalBaseRules.StrategicBaseValue(forward, fort, loss, p);

            // 対照＝後方の脆い哨戒拠点（好位置でなく維持も薄く防御もない）
            float weakPos = NavalBaseRules.BasePositionValue(0.3f, 0.1f, p);
            float weakSustain = NavalBaseRules.FleetSustainmentCapacity(0.3f, 0.3f, p);
            float weakRadius = NavalBaseRules.OperationalRadius(weakSustain, 0.3f, p);
            float weakForward = NavalBaseRules.ForwardBaseValue(weakPos, weakRadius, p);
            float weakFort = NavalBaseRules.Fortification(0.1f, 0.1f, p);
            float weakDep = NavalBaseRules.BaseDependency(weakRadius, 0.9f, p);
            float weakLoss = NavalBaseRules.BaseLossImpact(weakSustain, weakDep, p);
            float weak = NavalBaseRules.StrategicBaseValue(weakForward, weakFort, weakLoss, p);

            Assert.Greater(izerlohn, weak, "前線直結の堅固な要塞は後方の脆い拠点より戦略価値が高い");
            Assert.Greater(izerlohn, 0.5f, "イゼルローン型は高い戦略価値を持つ");
        }
    }
}
