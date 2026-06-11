using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>グレーゾーン作戦（戦争未満の曖昧な攻撃・否認可能性モデル #1392）の純ロジック検証。</summary>
    public class GreyZoneRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>閾値への近さ＝行動規模/戦争閾値で、閾値以下に留まる間は1未満・到達で飽和。</summary>
        [Test]
        public void ThresholdProximity_ScalesToWarThreshold()
        {
            // 行動0.3・閾値0.6 → 0.3/0.6 = 0.5（半分まで迫る＝まだグレーゾーン）。
            Assert.AreEqual(0.5f, GreyZoneRules.ThresholdProximity(0.3f, 0.6f), Eps);
            // 行動が閾値到達 → 1.0（公然の戦争へ）。
            Assert.AreEqual(1f, GreyZoneRules.ThresholdProximity(0.6f, 0.6f), Eps);
            // 閾値を超えてもクランプで1.0。
            Assert.AreEqual(1f, GreyZoneRules.ThresholdProximity(0.9f, 0.6f), Eps);
        }

        /// <summary>否認可能性＝帰属困難×0.6＋代理使用×0.4で、曖昧さと代理が積み上がる。</summary>
        [Test]
        public void Deniability_WeightsAttributionAndProxy()
        {
            // 帰属困難1.0・代理1.0 → 0.6+0.4 = 1.0（完全に否認可能）。
            Assert.AreEqual(1f, GreyZoneRules.Deniability(1f, 1f), Eps);
            // 帰属困難のみ1.0 → 0.6。
            Assert.AreEqual(0.6f, GreyZoneRules.Deniability(1f, 0f), Eps);
            // 代理のみ1.0 → 0.4。
            Assert.AreEqual(0.4f, GreyZoneRules.Deniability(0f, 1f), Eps);
        }

        /// <summary>サラミ戦術＝小さく刻めば累積、許容(0.3)を超えて大きく刻むと反撃で利得が削られる。</summary>
        [Test]
        public void SalamiSlicing_SmallSlicesAccumulate_LargeSlicesGetClawedBack()
        {
            // 小スライス0.2（許容0.3以下）・既存0.5 → 0.5+0.2 = 0.7（そのまま積み上がる）。
            Assert.AreEqual(0.7f, GreyZoneRules.SalamiSlicing(0.2f, 0.5f), Eps);
            // 大スライス0.5（許容0.3超過0.2）・既存0.3 → 0.3+0.5−0.2 = 0.6（反撃ぶん相殺）。
            Assert.AreEqual(0.6f, GreyZoneRules.SalamiSlicing(0.5f, 0.3f), Eps);
        }

        /// <summary>反撃ライン以下の挑発＝行動がレッドライン未満なら true（撃ち返されない）。</summary>
        [Test]
        public void ProvocationBelowResponse_StaysUnderRedline()
        {
            // 行動0.3 < レッドライン0.5 → 反撃を招かない。
            Assert.IsTrue(GreyZoneRules.ProvocationBelowResponse(0.3f, 0.5f));
            // 行動0.5 はレッドライン0.5 ちょうど → 未満でないので踏む。
            Assert.IsFalse(GreyZoneRules.ProvocationBelowResponse(0.5f, 0.5f));
            // 行動0.6 > レッドライン0.5 → 反撃を招く。
            Assert.IsFalse(GreyZoneRules.ProvocationBelowResponse(0.6f, 0.5f));
        }

        /// <summary>反撃のジレンマ＝否認可能性×(1−閾値の近さ)で、曖昧で小さいほど見過ごす方へ深まる。</summary>
        [Test]
        public void ResponseDilemma_DeepensWhenDeniableAndSmall()
        {
            // 否認0.8・閾値の近さ0.25 → 0.8×0.75 = 0.6（反撃が過剰に見え見過ごしやすい）。
            Assert.AreEqual(0.6f, GreyZoneRules.ResponseDilemma(0.8f, 0.25f), Eps);
            // 閾値に迫る(1.0)とジレンマ消失＝公然の攻撃なら反撃が正当化される。
            Assert.AreEqual(0f, GreyZoneRules.ResponseDilemma(0.8f, 1f), Eps);
        }

        /// <summary>既成事実の累積＝サラミ利得が時間(熟成速度0.5)で大きな現状変更へ熟成し、放置で手遅れへ。</summary>
        [Test]
        public void CumulativeFaitAccompli_GrowsOverTime()
        {
            // 利得0.4・dt1.0 → 0.4 + 0.4×0.5×1.0 = 0.6（積み重ねが大局を動かす）。
            Assert.AreEqual(0.6f, GreyZoneRules.CumulativeFaitAccompli(0.4f, 1f), Eps);
            // dt0 → 熟成せず元の利得のまま。
            Assert.AreEqual(0.4f, GreyZoneRules.CumulativeFaitAccompli(0.4f, 0f), Eps);
        }

        /// <summary>エスカレーション制御＝否認可能性と退避路の積の補集合で、どちらか高ければ全面戦争を抑える。</summary>
        [Test]
        public void EscalationControl_AvoidsFullWarWhenDeniableOrOffRamped()
        {
            // 否認0.6・退避0.5 → 1−0.4×0.5 = 0.8（口実を奪い出口を残す）。
            Assert.AreEqual(0.8f, GreyZoneRules.EscalationControl(0.6f, 0.5f), Eps);
            // 両方ゼロ → 制御なし（梯子を昇らせる）。
            Assert.AreEqual(0f, GreyZoneRules.EscalationControl(0f, 0f), Eps);
            // 否認のみ1.0 → 1.0（口実を完全に奪えば制御できる）。
            Assert.AreEqual(1f, GreyZoneRules.EscalationControl(1f, 0f), Eps);
        }

        /// <summary>グレーゾーン侵略判定＝閾値以下かつ(1−閾値の近さ)×否認可能性がしきい(0.5)以上で成立。</summary>
        [Test]
        public void IsGreyZoneAggression_SmallAndDeniableSucceeds()
        {
            // 閾値の近さ0.2・否認0.8 → (1−0.2)×0.8 = 0.64 ≥ 0.5 → グレーゾーン侵略成立。
            Assert.IsTrue(GreyZoneRules.IsGreyZoneAggression(0.2f, 0.8f, 0.5f));
            // 否認が低い(0.3) → 0.8×0.3 = 0.24 < 0.5 → 不成立（曖昧さが足りない）。
            Assert.IsFalse(GreyZoneRules.IsGreyZoneAggression(0.2f, 0.3f, 0.5f));
            // 閾値到達(1.0) → もはやグレーゾーンでない＝公然の戦争で false。
            Assert.IsFalse(GreyZoneRules.IsGreyZoneAggression(1f, 1f, 0.5f));
        }
    }
}
