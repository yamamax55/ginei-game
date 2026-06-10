using NUnit.Framework;
using Ginei;
using RP = Ginei.ReserveCurrencyParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 基軸通貨特権（通貨覇権の力学）を固定する：基軸度の遅い構築と信認崩壊の速い下落（非対称）・
    /// 法外な特権（赤字を刷って埋める）・通貨発行益・濫用の蓄積（世界の我慢）・
    /// 代替通貨の出現が崩壊閾値を下げる効果・崩壊時の返済ショック。
    /// 「基軸通貨は世界からの無利子借金＝返済日は信認が決める」。すべて決定論・純ロジック。
    /// </summary>
    public class ReserveCurrencyRulesTests
    {
        // ===== ReserveStatusTick =====

        [Test]
        public void ReserveStatusTick_RisesSlowly_ButTrustCollapseFallsFast()
        {
            var p = RP.Default; // rise 0.05 / fall 0.1 / trustCrashScale 4
            // 全条件最良（交易1・軍事1・信認1）でも上昇は riseRate＝一夜で覇権は築けない。
            Assert.AreEqual(0.05f, ReserveCurrencyRules.ReserveStatusTick(0f, 1f, 1f, 1f, 1f, p), 1e-4f);
            // 信認0なら下落速度は fallRate×(1+4)=0.5 ＝ 1 → 0.5。
            Assert.AreEqual(0.5f, ReserveCurrencyRules.ReserveStatusTick(1f, 1f, 1f, 0f, 1f, p), 1e-4f);
            // 非対称の担保：信認崩壊の下落幅(0.5) ≫ 最良条件の上昇幅(0.05)。
            float rise = ReserveCurrencyRules.ReserveStatusTick(0f, 1f, 1f, 1f, 1f, p) - 0f;
            float fall = 1f - ReserveCurrencyRules.ReserveStatusTick(1f, 1f, 1f, 0f, 1f, p);
            Assert.Greater(fall, rise * 5f);
        }

        [Test]
        public void ReserveStatusTick_TrustKeptFallIsSlow_AndClamps()
        {
            var p = RP.Default;
            // 実体（交易・軍事）を失っても信認1なら下落は基礎 fallRate のみ＝1 → 0.9（ゆっくり崩れる）。
            Assert.AreEqual(0.9f, ReserveCurrencyRules.ReserveStatusTick(1f, 0f, 0f, 1f, 1f, p), 1e-4f);
            // dt 負は0扱い＝変化なし。
            Assert.AreEqual(1f, ReserveCurrencyRules.ReserveStatusTick(1f, 0f, 0f, 1f, -1f, p), 1e-4f);
            // 巨大 dt でも目標を飛び越えない（speed×dt はClamp01）＝目標に一致。
            Assert.AreEqual(0f, ReserveCurrencyRules.ReserveStatusTick(1f, 0f, 0f, 0f, 100f, p), 1e-4f);
            // 入力>1 はクランプ＝目標は (0.5+0.5)×1=1 のまま。
            Assert.AreEqual(0.05f, ReserveCurrencyRules.ReserveStatusTick(0f, 5f, 5f, 5f, 1f, p), 1e-4f);
        }

        // ===== ExorbitantPrivilege / SeigniorageIncome =====

        [Test]
        public void ExorbitantPrivilege_StatusTimesDeficit()
        {
            // 基軸度1×赤字0.5 → 赤字の半分を刷って埋められる。
            Assert.AreEqual(0.5f, ReserveCurrencyRules.ExorbitantPrivilege(1f, 0.5f), 1e-4f);
            // 基軸度0＝特権なし（赤字は借金か増税でしか埋まらない）。
            Assert.AreEqual(0f, ReserveCurrencyRules.ExorbitantPrivilege(0f, 1f), 1e-4f);
            // 入力クランプ：2×2 でも 1×1=1。
            Assert.AreEqual(1f, ReserveCurrencyRules.ExorbitantPrivilege(2f, 2f), 1e-4f);
        }

        [Test]
        public void SeigniorageIncome_ScalesWithStatusAndVolume()
        {
            var p = RP.Default; // seigniorageRate 0.05
            // 基軸度0.8×世界交易量1000×0.05 = 40。
            Assert.AreEqual(40f, ReserveCurrencyRules.SeigniorageIncome(0.8f, 1000f, p), 1e-3f);
            // 負の交易量は0へクランプ。
            Assert.AreEqual(0f, ReserveCurrencyRules.SeigniorageIncome(1f, -100f, p), 1e-4f);
        }

        // ===== DebasementToleratedTick =====

        [Test]
        public void DebasementToleratedTick_AccumulatesAndStatusDampens()
        {
            var p = RP.Default; // abuseAccumRate 0.5 / abuseDecayRate 0.05 / StatusToleranceDamping 0.5
            // 全力濫用（基軸度0）＝0.5×1tick 蓄積。
            Assert.AreEqual(0.5f, ReserveCurrencyRules.DebasementToleratedTick(0f, 1f, 0f, 1f, p), 1e-4f);
            // 基軸度1なら世界が呑み込み蓄積は半減（0.25）＝我慢される。が、ゼロにはならない。
            float tolerated = ReserveCurrencyRules.DebasementToleratedTick(0f, 1f, 1f, 1f, p);
            Assert.AreEqual(0.25f, tolerated, 1e-4f);
            Assert.Greater(tolerated, 0f);
            // 濫用をやめても減衰は遅い：0.5 → 0.45（疑念は急には消えない）。
            Assert.AreEqual(0.45f, ReserveCurrencyRules.DebasementToleratedTick(0.5f, 0f, 0f, 1f, p), 1e-4f);
        }

        // ===== TrustCollapseThreshold / IsTrustCollapsed =====

        [Test]
        public void TrustCollapse_NoAlternativeNeverCollapses_AlternativeLowersThreshold()
        {
            var p = RP.Default; // noAlt 1.2 / fullAlt 0.4
            // 代替なし＝閾値1.2＞1 ＝濫用が満タン(1)でも崩れない。
            Assert.AreEqual(1.2f, ReserveCurrencyRules.TrustCollapseThreshold(0f, p), 1e-4f);
            Assert.IsFalse(ReserveCurrencyRules.IsTrustCollapsed(1f, 0f, p));
            // 代替が現れた瞬間に閾値が下がる：完全代替で0.4・中間0.5で0.8（単調低下）。
            Assert.AreEqual(0.4f, ReserveCurrencyRules.TrustCollapseThreshold(1f, p), 1e-4f);
            Assert.AreEqual(0.8f, ReserveCurrencyRules.TrustCollapseThreshold(0.5f, p), 1e-4f);
            // 同じ濫用0.5でも、代替なしでは安泰・完全代替では崩壊＝引き金は濫用でなく代替の出現。
            Assert.IsFalse(ReserveCurrencyRules.IsTrustCollapsed(0.5f, 0f, p));
            Assert.IsTrue(ReserveCurrencyRules.IsTrustCollapsed(0.5f, 1f, p));
            // 閾値未満は崩れない（境界）。
            Assert.IsFalse(ReserveCurrencyRules.IsTrustCollapsed(0.39f, 1f, p));
        }

        // ===== CollapseShock =====

        [Test]
        public void CollapseShock_ProportionalToStatus()
        {
            var p = RP.Default; // collapseShockScale 2
            // 基軸度1の崩壊＝ショック2.0（享受した特権が一気に逆流）。
            Assert.AreEqual(2f, ReserveCurrencyRules.CollapseShock(1f, p), 1e-4f);
            // 基軸度0.5なら1.0＝特権が小さければ返済も小さい。
            Assert.AreEqual(1f, ReserveCurrencyRules.CollapseShock(0.5f, p), 1e-4f);
            // 基軸度0＝特権を持たなかった国に返済日は来ない。
            Assert.AreEqual(0f, ReserveCurrencyRules.CollapseShock(0f, p), 1e-4f);
            // 入力>1 はクランプ。
            Assert.AreEqual(2f, ReserveCurrencyRules.CollapseShock(5f, p), 1e-4f);
        }

        // ===== Params ctor clamp =====

        [Test]
        public void Params_CtorClampsInvalidValues()
        {
            var p = new ReserveCurrencyParams(-1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, 5f, -1f);
            Assert.AreEqual(0f, p.tradeWeight, 1e-4f);          // 0..1へ
            Assert.AreEqual(0f, p.riseRate, 1e-4f);             // 非負
            Assert.AreEqual(0f, p.trustCrashScale, 1e-4f);      // 非負
            Assert.AreEqual(0.01f, p.noAlternativeThreshold, 1e-4f); // 下限0.01
            // 全代替閾値は無代替閾値を超えない（代替の出現で閾値が「上がる」逆転を許さない）。
            Assert.LessOrEqual(p.fullAlternativeThreshold, p.noAlternativeThreshold);
            Assert.AreEqual(0f, p.collapseShockScale, 1e-4f);   // 非負
        }
    }
}
