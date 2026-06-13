using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>義理・恩の負債構造（KIKU-2 #1835・『菊と刀』）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class GiriRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>恩の重さ＝恵み×(1＋格×0.5)。同じ恵みでも格上から受けるほど重い。</summary>
        [Test]
        public void OnIncurred_格上の恩ほど重い()
        {
            // 0.4 × (1 + 0×0.5) = 0.4
            Assert.AreEqual(0.4f, GiriRules.OnIncurred(0.4f, 0f), Eps);
            // 0.4 × (1 + 1×0.5) = 0.6
            Assert.AreEqual(0.6f, GiriRules.OnIncurred(0.4f, 1f), Eps);
            // クランプ（0..1）
            Assert.AreEqual(1f, GiriRules.OnIncurred(1f, 1f), Eps);
        }

        /// <summary>恩は返すまで消えず累積する負債。</summary>
        [Test]
        public void DebtAccumulation_恩は累積する()
        {
            Assert.AreEqual(0.7f, GiriRules.DebtAccumulation(0.3f, 0.4f), Eps);
            // 負の入力は0扱い
            Assert.AreEqual(0.3f, GiriRules.DebtAccumulation(0.3f, -1f), Eps);
        }

        /// <summary>返済行為が負債を減らす量＝返済行為×負債規模、もとの負債を超えない。</summary>
        [Test]
        public void RepaymentValue_負債を超えて返せない()
        {
            // 0.5 × 0.8 = 0.4
            Assert.AreEqual(0.4f, GiriRules.RepaymentValue(0.5f, 0.8f), Eps);
            // 全力返済でも負債規模まで
            Assert.AreEqual(0.8f, GiriRules.RepaymentValue(2f, 0.8f), Eps);
        }

        /// <summary>重荷＝未返済×(1−返済能力)×0.9。返せないほど重い。</summary>
        [Test]
        public void DebtBurden_返済能力が低いほど重い()
        {
            // 0.8 × (1-0.5) × 0.9 = 0.36
            Assert.AreEqual(0.36f, GiriRules.DebtBurden(0.8f, 0.5f), Eps);
            // 返済能力満点なら重荷なし
            Assert.AreEqual(0f, GiriRules.DebtBurden(0.8f, 1f), Eps);
            // 返済能力が低いほど単調に重い
            Assert.Greater(GiriRules.DebtBurden(0.8f, 0.2f), GiriRules.DebtBurden(0.8f, 0.8f));
        }

        /// <summary>返済から育つ忠誠＝返した割合×1.0（既定効率）。</summary>
        [Test]
        public void LoyaltyFromRepayment_返すほど忠誠が育つ()
        {
            Assert.AreEqual(0.6f, GiriRules.LoyaltyFromRepayment(0.6f), Eps);
            Assert.AreEqual(1f, GiriRules.LoyaltyFromRepayment(1f), Eps);
            Assert.AreEqual(0f, GiriRules.LoyaltyFromRepayment(0f), Eps);
        }

        /// <summary>恩を返さぬ恥＝未返済×可視性×0.8。人目に晒されるほど恥が大きい。</summary>
        [Test]
        public void ShameFromDefault_未返済と可視性で恥が増える()
        {
            // 0.5 × 0.5 × 0.8 = 0.2
            Assert.AreEqual(0.2f, GiriRules.ShameFromDefault(0.5f, 0.5f), Eps);
            // 人目につかなければ恥なし
            Assert.AreEqual(0f, GiriRules.ShameFromDefault(1f, 0f), Eps);
        }

        /// <summary>恩の優先＝より重い義理（未返済×格）を先に返す（-1=A / 0=同等 / +1=B）。</summary>
        [Test]
        public void ObligationPriority_重い義理を先に返す()
        {
            // A=未返済0.8・格1.0（重み1.6）, B=未返済0.5・格0.0（重み0.5）→ A優先
            var heavy = new ObligationDebt(0.8f, 0f, 1f);
            var light = new ObligationDebt(0.5f, 0f, 0f);
            Assert.AreEqual(-1, GiriRules.ObligationPriority(heavy, light));
            Assert.AreEqual(1, GiriRules.ObligationPriority(light, heavy));
            // 同一なら0
            Assert.AreEqual(0, GiriRules.ObligationPriority(heavy, heavy));
        }

        /// <summary>感謝↔怨み＝重荷が閾値0.5未満は感謝(正)、超は怨み(負)。返せる恩は感謝に、返せぬ重荷は怨みに。</summary>
        [Test]
        public void GratitudeVsResentment_重荷が許容を超えると怨みに転じる()
        {
            // 閾値ちょうど＝0
            Assert.AreEqual(0f, GiriRules.GratitudeVsResentment(0.5f), Eps);
            // 重荷ゼロ＝最大の感謝
            Assert.AreEqual(1f, GiriRules.GratitudeVsResentment(0f), Eps);
            // 重荷最大＝最大の怨み
            Assert.AreEqual(-1f, GiriRules.GratitudeVsResentment(1f), Eps);
            // 軽い重荷は正（感謝）、重い重荷は負（怨み）
            Assert.Greater(GiriRules.GratitudeVsResentment(0.25f), 0f);
            Assert.Less(GiriRules.GratitudeVsResentment(0.75f), 0f);
        }

        /// <summary>恩義の拘束＝未返済が閾値超で「恩義に縛られた」状態。</summary>
        [Test]
        public void IsIndebted_閾値超で恩義に縛られる()
        {
            Assert.IsTrue(GiriRules.IsIndebted(0.7f, 0.5f));
            Assert.IsFalse(GiriRules.IsIndebted(0.3f, 0.5f));
        }

        /// <summary>物語＝受けた恩は負債として累積し、返せば忠誠を生むが、返せぬ重荷は怨みになる。</summary>
        [Test]
        public void 物語_恩は返せば忠誠返せねば怨みになる()
        {
            // 格上の主君から恩を受ける＝重い恩が累積
            float on1 = GiriRules.OnIncurred(0.6f, 1.0f);          // 0.6×1.5=0.9
            float debt = GiriRules.DebtAccumulation(0f, on1);      // 0.9
            Assert.AreEqual(0.9f, debt, Eps);
            Assert.IsTrue(GiriRules.IsIndebted(debt, 0.5f), "重い恩で恩義に縛られる");

            // ケースA：返済能力が高く、恩を返せる家臣
            var paid = new ObligationDebt(0.9f, 0.81f, 1f);        // 9割返済
            float loyalty = GiriRules.LoyaltyFromRepayment(paid.RepaidRatio);
            Assert.Greater(loyalty, 0.8f, "恩を返し合うほど忠誠が育つ");
            float burdenLow = GiriRules.DebtBurden(paid.Remaining, 0.9f); // 残0.09・能力高→重荷ほぼ無
            Assert.Greater(GiriRules.GratitudeVsResentment(burdenLow), 0f, "返せる恩は感謝になる");

            // ケースB：返済能力が低く、返せぬ重荷を抱えた家臣
            float burdenHigh = GiriRules.DebtBurden(debt, 0.0f);   // 0.9×1×0.9=0.81＞閾値
            Assert.Greater(burdenHigh, 0.5f);
            Assert.Less(GiriRules.GratitudeVsResentment(burdenHigh), 0f, "返せぬ重荷は怨みに転じる");
            // 未返済かつ人目に晒されれば恥も生む
            Assert.Greater(GiriRules.ShameFromDefault(debt, 1f), 0f, "返さぬ恩は恥に直結する");
        }
    }
}
