using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>国家意志・後発国の底力（#1433・坂の上の雲型）の純ロジックのテスト。</summary>
    public class NationalDeterminationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>国家意志＝存亡の切迫×0.6＋一体感×0.4＝両者最大で1.0・既定重みの加重和。</summary>
        [Test]
        public void NationalWill_存亡と一体感の加重和()
        {
            // 0.6*1 + 0.4*1 = 1.0
            Assert.AreEqual(1f, NationalDeterminationRules.NationalWill(1f, 1f), Eps);
            // 0.6*1 + 0.4*0 = 0.6
            Assert.AreEqual(0.6f, NationalDeterminationRules.NationalWill(1f, 0f), Eps);
            // 0.6*0.5 + 0.4*0.5 = 0.5
            Assert.AreEqual(0.5f, NationalDeterminationRules.NationalWill(0.5f, 0.5f), Eps);
            // 切迫も一体感も無ければ意志は湧かない
            Assert.AreEqual(0f, NationalDeterminationRules.NationalWill(0f, 0f), Eps);
        }

        /// <summary>劣勢の戦闘ボーナス＝劣るほど（forceRatio小）意志が底上げ・優勢や意志ゼロでは1.0。</summary>
        [Test]
        public void UnderdogCombatBonus_劣勢ほど意志が補正する()
        {
            // forceRatio=0.4, will=1.0: 1 + (1-0.4)*1*0.5 = 1.3
            Assert.AreEqual(1.3f, NationalDeterminationRules.UnderdogCombatBonus(0.4f, 1f), Eps);
            // 優勢（比1.0）はボーナスなし
            Assert.AreEqual(1f, NationalDeterminationRules.UnderdogCombatBonus(1f, 1f), Eps);
            // 意志ゼロは劣勢でも底力なし
            Assert.AreEqual(1f, NationalDeterminationRules.UnderdogCombatBonus(0.3f, 0f), Eps);
            // 劣勢ほど大きい（単調）
            float a = NationalDeterminationRules.UnderdogCombatBonus(0.2f, 0.8f);
            float b = NationalDeterminationRules.UnderdogCombatBonus(0.6f, 0.8f);
            Assert.Greater(a, b);
        }

        /// <summary>士気回復の加速＝意志×0.5×dt・意志ゼロや dt0 では加速なし。</summary>
        [Test]
        public void MoraleRecoveryAcceleration_意志が回復を速める()
        {
            // will=1, dt=2: 1*0.5*2 = 1.0
            Assert.AreEqual(1f, NationalDeterminationRules.MoraleRecoveryAcceleration(1f, 2f), Eps);
            // will=0.6, dt=1: 0.6*0.5 = 0.3
            Assert.AreEqual(0.3f, NationalDeterminationRules.MoraleRecoveryAcceleration(0.6f, 1f), Eps);
            // 意志ゼロは加速なし
            Assert.AreEqual(0f, NationalDeterminationRules.MoraleRecoveryAcceleration(0f, 5f), Eps);
        }

        /// <summary>背水の決意＝意志に背水度×0.5を上乗せ・後がないほど跳ねて上限1.0。</summary>
        [Test]
        public void LastStandResolve_背水で底力が跳ねる()
        {
            // will=0.6, wall=0.4: 0.6 + 0.4*0.5 = 0.8
            Assert.AreEqual(0.8f, NationalDeterminationRules.LastStandResolve(0.6f, 0.4f), Eps);
            // 退路なしでも背水ゼロなら意志そのまま
            Assert.AreEqual(0.6f, NationalDeterminationRules.LastStandResolve(0.6f, 0f), Eps);
            // 上限1.0でクランプ
            Assert.AreEqual(1f, NationalDeterminationRules.LastStandResolve(0.9f, 1f), Eps);
        }

        /// <summary>意志の摩耗＝長期戦×0.3×dt を差し引く・必死さは続かない（下限0）。</summary>
        [Test]
        public void DeterminationFatigue_長期戦で意志が剥落()
        {
            // will=1, duration=1, dt=1: 1 - 1*0.3*1 = 0.7
            Assert.AreEqual(0.7f, NationalDeterminationRules.DeterminationFatigue(1f, 1f, 1f), Eps);
            // 短期（duration小）は摩耗が小さい
            Assert.AreEqual(0.94f, NationalDeterminationRules.DeterminationFatigue(1f, 0.2f, 1f), Eps);
            // 摩耗は下限0でクランプ
            Assert.AreEqual(0f, NationalDeterminationRules.DeterminationFatigue(0.2f, 1f, 5f), Eps);
        }

        /// <summary>精神論の限界＝下限比0.2を割る致命的劣勢では意志でも覆せず素の比を返す。</summary>
        [Test]
        public void OverextensionVsResolve_限度を超えた戦力差は覆せない()
        {
            // 下限0.2を割る0.1: 意志があっても素の比のまま
            Assert.AreEqual(0.1f, NationalDeterminationRules.OverextensionVsResolve(0.1f, 1f), Eps);
            // 下限以上0.4, will=1: 0.4 * (1+(1-0.4)*1*0.5) = 0.4*1.3 = 0.52
            Assert.AreEqual(0.52f, NationalDeterminationRules.OverextensionVsResolve(0.4f, 1f), Eps);
            // 補正後は素の比より上（限界内なら意志が効く）
            Assert.Greater(NationalDeterminationRules.OverextensionVsResolve(0.4f, 1f), 0.4f);
        }

        /// <summary>危機の動員＝存亡の危機×潜在国力×0.6＝火事場の馬鹿力・危機ゼロや潜在ゼロでは0。</summary>
        [Test]
        public void MobilizationFromCrisis_危機が潜在国力を引き出す()
        {
            // stakes=1, latent=1: 1*1*0.6 = 0.6
            Assert.AreEqual(0.6f, NationalDeterminationRules.MobilizationFromCrisis(1f, 1f), Eps);
            // stakes=0.5, latent=0.8: 0.8*0.5*0.6 = 0.24
            Assert.AreEqual(0.24f, NationalDeterminationRules.MobilizationFromCrisis(0.5f, 0.8f), Eps);
            // 危機が無ければ動員されない
            Assert.AreEqual(0f, NationalDeterminationRules.MobilizationFromCrisis(0f, 1f), Eps);
        }

        /// <summary>闘志判定＝意志が閾値以上で true・既定閾値0.5。</summary>
        [Test]
        public void IsFightingSpirit_意志が戦力差を補う闘志()
        {
            Assert.IsTrue(NationalDeterminationRules.IsFightingSpirit(0.6f));
            Assert.IsFalse(NationalDeterminationRules.IsFightingSpirit(0.4f));
            Assert.IsTrue(NationalDeterminationRules.IsFightingSpirit(0.5f)); // 境界は以上
            // 明示閾値
            Assert.IsTrue(NationalDeterminationRules.IsFightingSpirit(0.8f, 0.7f));
            Assert.IsFalse(NationalDeterminationRules.IsFightingSpirit(0.6f, 0.7f));
        }
    }
}
