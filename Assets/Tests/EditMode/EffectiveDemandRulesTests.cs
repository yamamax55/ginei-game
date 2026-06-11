using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 有効需要の原理（KEYN-1 #1540）の純ロジック EditMode テスト。
    /// 既定 <see cref="EffectiveDemandParams.Default"/>（オークン0.5・失業上限0.5・刺激効率0.8・需要制約閾値0.05）で期待値を固定。
    /// </summary>
    public class EffectiveDemandRulesTests
    {
        private const float Eps = 1e-4f;
        private static EffectiveDemandParams P => EffectiveDemandParams.Default;

        /// <summary>総需要＝C+I+G（各クランプ・合計0..3）。</summary>
        [Test]
        public void AggregateDemand_合計してクランプする()
        {
            // 0.3+0.2+0.1 = 0.6
            Assert.AreEqual(0.6f, EffectiveDemandRules.AggregateDemand(0.3f, 0.2f, 0.1f), Eps);
            // 各満額1+1+1=3（青天井にしない）
            Assert.AreEqual(3f, EffectiveDemandRules.AggregateDemand(1f, 1f, 1f), Eps);
            // 範囲外入力はクランプ（負→0）
            Assert.AreEqual(0.5f, EffectiveDemandRules.AggregateDemand(-1f, 0.5f, 0f), Eps);
        }

        /// <summary>現実産出は需要に制約される（需要が産出を決める＝min(需要,潜在)）。</summary>
        [Test]
        public void ActualOutput_需要が産出を決める()
        {
            // 需要不足：需要0.6 < 潜在1 → 産出は需要に制約され0.6
            Assert.AreEqual(0.6f, EffectiveDemandRules.ActualOutput(0.6f, 1f), Eps);
            // 需要過多：需要1.5 > 潜在0.8 → 潜在で頭打ち0.8（供給力が天井）
            Assert.AreEqual(0.8f, EffectiveDemandRules.ActualOutput(1.5f, 0.8f), Eps);
        }

        /// <summary>産出ギャップ＝潜在−現実（正＝遊休／負＝過熱）。</summary>
        [Test]
        public void OutputGap_潜在マイナス現実()
        {
            // 潜在1 − 現実0.6 = 0.4（需要不足の遊休）
            Assert.AreEqual(0.4f, EffectiveDemandRules.OutputGap(0.6f, 1f), Eps);
            // 現実が潜在を超える＝負（過熱）
            Assert.AreEqual(-0.2f, EffectiveDemandRules.OutputGap(1f, 0.8f), Eps);
        }

        /// <summary>遊休資源＝正のギャップぶんだけ遊ぶ（過熱は0）。</summary>
        [Test]
        public void IdleResources_需要不足のみ遊ぶ()
        {
            Assert.AreEqual(0.4f, EffectiveDemandRules.IdleResources(0.4f), Eps);
            // 過熱（負ギャップ）は遊休0
            Assert.AreEqual(0f, EffectiveDemandRules.IdleResources(-0.2f), Eps);
        }

        /// <summary>産出ギャップが非自発的失業を生む（オークン的・上限クランプ）。</summary>
        [Test]
        public void UnemploymentFromGap_ギャップが失業を生む()
        {
            // 遊休0.4 × オークン0.5 = 0.2
            Assert.AreEqual(0.2f, EffectiveDemandRules.UnemploymentFromGap(0.4f, P), Eps);
            // 巨大ギャップでも失業上限0.5で頭打ち（1.0×0.5=0.5 だが上限0.5）
            Assert.AreEqual(0.5f, EffectiveDemandRules.UnemploymentFromGap(2f, P), Eps);
            // 過熱（負ギャップ）は失業0
            Assert.AreEqual(0f, EffectiveDemandRules.UnemploymentFromGap(-0.3f, P), Eps);
        }

        /// <summary>政府支出は需要不足時のみ需要ギャップを埋める（遊休ぶんまで）。</summary>
        [Test]
        public void DemandStimulus_需要不足時のみ有効()
        {
            // 政府支出0.5 × 効率0.8 = 0.4、ギャップ0.6 未満なので0.4ぶん埋める
            Assert.AreEqual(0.4f, EffectiveDemandRules.DemandStimulus(0.5f, 0.6f, P), Eps);
            // 刺激0.8 が小さいギャップ0.2 を上回る → 埋められるのはギャップぶん0.2まで
            Assert.AreEqual(0.2f, EffectiveDemandRules.DemandStimulus(1f, 0.2f, P), Eps);
            // 過熱・均衡（gap<=0）では刺激は無効
            Assert.AreEqual(0f, EffectiveDemandRules.DemandStimulus(1f, 0f, P), Eps);
        }

        /// <summary>インフレギャップ＝需要が潜在を超えた過熱ぶん。</summary>
        [Test]
        public void InflationaryGap_潜在超過が過熱インフレ圧()
        {
            // 需要1.3 − 潜在1 = 0.3（過熱）
            Assert.AreEqual(0.3f, EffectiveDemandRules.InflationaryGap(1.3f, 1f), Eps);
            // 需要不足では過熱インフレギャップは0
            Assert.AreEqual(0f, EffectiveDemandRules.InflationaryGap(0.6f, 1f), Eps);
        }

        /// <summary>需要制約の判定（閾値より深い正ギャップ＝供給力はあるのに需要がない）。</summary>
        [Test]
        public void IsDemandConstrained_需要制約を判定する()
        {
            // ギャップ0.4 > 閾値0.05 → 需要制約（不況）
            Assert.IsTrue(EffectiveDemandRules.IsDemandConstrained(0.4f, P));
            // ギャップ0.02 は閾値0.05 以下 → ほぼ均衡＝制約でない
            Assert.IsFalse(EffectiveDemandRules.IsDemandConstrained(0.02f, P));
            // 過熱（負ギャップ）は需要制約でない
            Assert.IsFalse(EffectiveDemandRules.IsDemandConstrained(-0.2f, P));
        }
    }
}
