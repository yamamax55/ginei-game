using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>愛国心・国民感情（戦時の国民の結束と動員）の純ロジックテスト。既定Paramsで期待値を固定。</summary>
    public class PatriotismRulesTests
    {
        const float Tol = 1e-4f;
        const float PowTol = 1e-3f;

        /// <summary>愛国の高揚＝国民の誇りが外敵脅威で増幅する。</summary>
        [Test]
        public void PatrioticFervor_誇りが脅威で増幅する()
        {
            // 0.6×(1+0.5×0.5)=0.6×1.25=0.75
            Assert.AreEqual(0.75f, PatriotismRules.PatrioticFervor(0.6f, 0.5f), Tol);
            // 誇り0なら高揚なし
            Assert.AreEqual(0f, PatriotismRules.PatrioticFervor(0f, 1f), Tol);
            // 脅威が高いほど同じ誇りでも燃える
            Assert.Greater(PatriotismRules.PatrioticFervor(0.6f, 1f),
                PatriotismRules.PatrioticFervor(0.6f, 0f));
        }

        /// <summary>旗の下集結効果＝外敵脅威で指導部の下に結集する（脅威がなければ集結なし）。</summary>
        [Test]
        public void RallyEffect_脅威と指導部結束で集結する()
        {
            // 0.6×(0.5+0.5×0.8)=0.6×0.9=0.54
            Assert.AreEqual(0.54f, PatriotismRules.RallyEffect(0.6f, 0.8f), Tol);
            // 脅威0なら集結なし（脅威で乗算）
            Assert.AreEqual(0f, PatriotismRules.RallyEffect(0f, 1f), Tol);
            // 指導部が結束しているほど効果が増す
            Assert.Greater(PatriotismRules.RallyEffect(0.6f, 1f),
                PatriotismRules.RallyEffect(0.6f, 0f));
        }

        /// <summary>戦意の動員＝愛国の高揚と旗の下集結の重み付き和。</summary>
        [Test]
        public void MobilizationWill_高揚と集結で動員する()
        {
            // 0.75×0.6+0.54×0.4=0.45+0.216=0.666
            Assert.AreEqual(0.666f, PatriotismRules.MobilizationWill(0.75f, 0.54f), Tol);
            // 両方0なら動員なし
            Assert.AreEqual(0f, PatriotismRules.MobilizationWill(0f, 0f), Tol);
            // 両方高いと動員も高い
            Assert.Greater(PatriotismRules.MobilizationWill(0.9f, 0.9f),
                PatriotismRules.MobilizationWill(0.3f, 0.3f));
        }

        /// <summary>排外主義への暴走＝過剰な愛国が扇動と結びつき外敵憎悪へ歪む。</summary>
        [Test]
        public void Xenophobia_愛国と扇動で排外へ歪む()
        {
            // 0.8×(0.6×0.5+0.4)=0.8×0.7=0.56
            Assert.AreEqual(0.56f, PatriotismRules.Xenophobia(0.8f, 0.5f), Tol);
            // 愛国0なら排外なし
            Assert.AreEqual(0f, PatriotismRules.Xenophobia(0f, 1f), Tol);
            // 扇動が強いほど排外が増す
            Assert.Greater(PatriotismRules.Xenophobia(0.8f, 1f),
                PatriotismRules.Xenophobia(0.8f, 0f));
        }

        /// <summary>愛国心の減衰＝戦争の長期化と犠牲が厭戦を生み愛国を冷ます。</summary>
        [Test]
        public void WarWearinessDecay_長期化と犠牲で愛国が冷める()
        {
            // 0.8×(0.5×0.6+0.6×0.5)=0.8×(0.3+0.3)=0.8×0.6=0.48
            Assert.AreEqual(0.48f, PatriotismRules.WarWearinessDecay(0.8f, 0.6f, 0.5f), Tol);
            // 長期化も犠牲も0なら減衰なし
            Assert.AreEqual(0f, PatriotismRules.WarWearinessDecay(0.8f, 0f, 0f), Tol);
            // 犠牲が増えるほど厭戦が募る
            Assert.Greater(PatriotismRules.WarWearinessDecay(0.8f, 0.6f, 1f),
                PatriotismRules.WarWearinessDecay(0.8f, 0.6f, 0f));
        }

        /// <summary>国民士気の正味＝動員意欲から戦争疲れを差し引いた向き（-1..1）。</summary>
        [Test]
        public void NetNationalMorale_動員から戦争疲れを引く()
        {
            // 0.666-0.48=0.186
            Assert.AreEqual(0.186f, PatriotismRules.NetNationalMorale(0.666f, 0.48f), Tol);
            // 厭戦が勝てば負
            Assert.Less(PatriotismRules.NetNationalMorale(0.2f, 0.9f), 0f);
            // -1..1へclamp（厭戦極大で下限割れしない）
            Assert.GreaterOrEqual(PatriotismRules.NetNationalMorale(0f, 1f), -1f);
        }

        /// <summary>好戦的熱狂のリスク＝排外と扇動が揃うと非線形に暴走する。</summary>
        [Test]
        public void JingoismRisk_排外と扇動が揃うと暴走する()
        {
            // (0.56×0.5)^1.5=0.28^1.5=0.28×sqrt(0.28)=0.148162
            Assert.AreEqual(0.148162f, PatriotismRules.JingoismRisk(0.56f, 0.5f), PowTol);
            // どちらか0なら暴走なし（積）
            Assert.AreEqual(0f, PatriotismRules.JingoismRisk(1f, 0f), Tol);
            Assert.AreEqual(0f, PatriotismRules.JingoismRisk(0f, 1f), Tol);
            // 両方極大で暴走最大
            Assert.AreEqual(1f, PatriotismRules.JingoismRisk(1f, 1f), Tol);
        }

        /// <summary>結束判定＝国民士気の正味が閾値を超えたら結束（既定閾値0.1）。</summary>
        [Test]
        public void IsNationUnified_正味が閾値超で結束()
        {
            Assert.IsTrue(PatriotismRules.IsNationUnified(0.186f));   // 既定0.1超
            Assert.IsFalse(PatriotismRules.IsNationUnified(0.05f));   // 既定0.1未満
            Assert.IsTrue(PatriotismRules.IsNationUnified(0.3f, 0.2f));
            Assert.IsFalse(PatriotismRules.IsNationUnified(-0.1f, 0.0f));
        }

        /// <summary>
        /// 物語：外敵の侵攻で誇り高い国民が旗の下に集結し総力戦へ動員されるが、戦争が泥沼化し
        /// 犠牲が積み上がると厭戦が動員意欲を上回り、結束が崩れて士気が反転する。
        /// </summary>
        [Test]
        public void Story_旗の下集結から泥沼の厭戦へ()
        {
            // 開戦：誇り0.7・脅威0.9・指導部結束0.8で国民が燃え集結する
            float fervor = PatriotismRules.PatrioticFervor(0.7f, 0.9f);
            // 0.7×(1+0.9×0.5)=0.7×1.45=1.015→clamp 1.0
            Assert.AreEqual(1f, fervor, Tol);
            float rally = PatriotismRules.RallyEffect(0.9f, 0.8f);   // 0.9×0.9=0.81
            Assert.AreEqual(0.81f, rally, Tol);
            float mob = PatriotismRules.MobilizationWill(fervor, rally); // 0.6+0.324=0.924
            Assert.AreEqual(0.924f, mob, Tol);

            // 緒戦（短期・低犠牲）：厭戦は小さく国民は結束を保つ
            float earlyDecay = PatriotismRules.WarWearinessDecay(fervor, 0.2f, 0.2f); // 1.0×(0.1+0.12)=0.22
            float earlyNet = PatriotismRules.NetNationalMorale(mob, earlyDecay);     // 0.924-0.22=0.704
            Assert.Greater(earlyNet, 0f);
            Assert.IsTrue(PatriotismRules.IsNationUnified(earlyNet));

            // 泥沼（長期化・高犠牲）：厭戦が動員意欲を上回り結束が崩れる
            float lateDecay = PatriotismRules.WarWearinessDecay(fervor, 0.9f, 0.9f); // 1.0×(0.45+0.54)=0.99
            float lateNet = PatriotismRules.NetNationalMorale(mob, lateDecay);       // 0.924-0.99=-0.066
            Assert.Less(lateNet, 0f);
            Assert.IsFalse(PatriotismRules.IsNationUnified(lateNet));
            // 厭戦は時間とともに増す
            Assert.Greater(lateDecay, earlyDecay);
        }
    }
}
