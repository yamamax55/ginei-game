using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>告発カスケード（MNIA-4 #1625）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class AccusationCascadeRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>スケープゴート圧力＝損失×責任転嫁×0.8（既定blameScale）。</summary>
        [Test]
        public void ScapegoatPressure_損失と責任転嫁の積に係数()
        {
            // 0.5 × 0.5 × 0.8 = 0.2
            Assert.AreEqual(0.2f, AccusationCascadeRules.ScapegoatPressure(0.5f, 0.5f), Eps);
            // クランプ（入力過大でも0..1）
            Assert.AreEqual(0.8f, AccusationCascadeRules.ScapegoatPressure(2f, 2f), Eps);
            // 責任転嫁ゼロなら圧力ゼロ
            Assert.AreEqual(0f, AccusationCascadeRules.ScapegoatPressure(1f, 0f), Eps);
        }

        /// <summary>制度抑制倍率＝1−制度強度×0.9（既定）。強い法治ほど増殖を殺す。</summary>
        [Test]
        public void InstitutionalBrake_強い制度ほど倍率が小さい()
        {
            Assert.AreEqual(1f, AccusationCascadeRules.InstitutionalBrake(0f), Eps);     // 制度なし＝抑制なし
            Assert.AreEqual(0.55f, AccusationCascadeRules.InstitutionalBrake(0.5f), Eps); // 1-0.5*0.9
            Assert.AreEqual(0.1f, AccusationCascadeRules.InstitutionalBrake(1f), Eps);    // 1-0.9
        }

        /// <summary>自己増殖＝弱い制度では告発が告発を呼んで強度が上がる。</summary>
        [Test]
        public void CascadeTick_弱い制度で告発が増殖する()
        {
            // intensity0.5, 制度0（brake=1）, dt1: 0.5 + 0.6*0.5*0.5*1*1 - 0.1*0.5*1 = 0.5+0.15-0.05 = 0.6
            float next = AccusationCascadeRules.CascadeTick(0.5f, 0f, 1f);
            Assert.AreEqual(0.6f, next, Eps);
            Assert.Greater(next, 0.5f, "弱い制度では告発強度が増す");
        }

        /// <summary>強い制度は増殖を殺し、沈静が勝って強度が下がる＝連鎖を止めるのは制度だけ。</summary>
        [Test]
        public void CascadeTick_強い制度は連鎖を止める()
        {
            // intensity0.5, 制度1（brake=0.1）, dt1: 0.5 + 0.6*0.5*0.5*0.1*1 - 0.1*0.5*1 = 0.5+0.015-0.05 = 0.465
            float next = AccusationCascadeRules.CascadeTick(0.5f, 1f, 1f);
            Assert.AreEqual(0.465f, next, Eps);
            Assert.Less(next, 0.5f, "強い制度では告発強度が下がる");

            // 同条件で制度が弱いほど強度は高い（単調）
            float weak = AccusationCascadeRules.CascadeTick(0.5f, 0.2f, 1f);
            float strong = AccusationCascadeRules.CascadeTick(0.5f, 0.8f, 1f);
            Assert.Greater(weak, strong, "制度が弱いほど連鎖が進む");
        }

        /// <summary>次の標的＝強度がrollを上回れば現れる（決定論）。</summary>
        [Test]
        public void NextTargetEmergence_強度がrollを超えれば次が立つ()
        {
            Assert.IsTrue(AccusationCascadeRules.NextTargetEmergence(0.7f, 0.5f));
            Assert.IsFalse(AccusationCascadeRules.NextTargetEmergence(0.3f, 0.5f));
            Assert.IsFalse(AccusationCascadeRules.NextTargetEmergence(0.5f, 0.5f)); // 同値は不成立
        }

        /// <summary>冤罪比率＝強度×(1−証拠の質)×0.7（既定上限）。熱狂が高く証拠が薄いほど無実が告発される。</summary>
        [Test]
        public void FalseAccusationRatio_熱狂と証拠不足で冤罪が増える()
        {
            // 0.8 × (1-0.5) × 0.7 = 0.28
            Assert.AreEqual(0.28f, AccusationCascadeRules.FalseAccusationRatio(0.8f, 0.5f), Eps);
            // 証拠完璧なら冤罪なし
            Assert.AreEqual(0f, AccusationCascadeRules.FalseAccusationRatio(1f, 1f), Eps);
            // 証拠ゼロ＋強度最大で上限
            Assert.AreEqual(0.7f, AccusationCascadeRules.FalseAccusationRatio(1f, 0f), Eps);
        }

        /// <summary>魔女狩り判定＝強度が閾値超で成立。</summary>
        [Test]
        public void IsWitchHunt_閾値超で魔女狩り()
        {
            Assert.IsTrue(AccusationCascadeRules.IsWitchHunt(0.8f, 0.7f));
            Assert.IsFalse(AccusationCascadeRules.IsWitchHunt(0.6f, 0.7f));
        }

        /// <summary>時間沈静＝新規入力なしで強度が自然減衰（既定subsidence0.1）。</summary>
        [Test]
        public void Subsidence_時間で熱狂が冷める()
        {
            // 0.5 - 0.1*0.5*1 = 0.45
            Assert.AreEqual(0.45f, AccusationCascadeRules.Subsidence(0.5f, 1f), Eps);
            // 反復で単調減少
            float a = AccusationCascadeRules.Subsidence(0.5f, 1f);
            float b = AccusationCascadeRules.Subsidence(a, 1f);
            Assert.Less(b, a);
        }
    }
}
