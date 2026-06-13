using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 漸進的社会工学 vs ユートピア的社会工学（POPR-2 #1514・ポパー型）の純ロジック検証。
    /// 既定 Params＝漸進便益0.4・全体改造便益1.0・漸進下振れ上限0.25・全体改造下振れ上限0.9・
    /// 漸進学習0.8・全体改造学習0.2。
    /// </summary>
    public class PiecemealEngineeringRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>期待便益＝全体改造は成功すれば大きい（漸進より上振れ）。野心・能力が同じでも差が出る。</summary>
        [Test]
        public void ExpectedBenefit_全体改造は漸進より上振れする()
        {
            float piecemeal = PiecemealEngineeringRules.ExpectedBenefit(ReformMode.漸進的, 1f, 1f);
            float utopian = PiecemealEngineeringRules.ExpectedBenefit(ReformMode.全体改造, 1f, 1f);
            // 漸進=0.4*1*1=0.4、全体改造=1.0*1*1=1.0。
            Assert.AreEqual(0.4f, piecemeal, Eps);
            Assert.AreEqual(1.0f, utopian, Eps);
            Assert.Greater(utopian, piecemeal);
        }

        /// <summary>下振れリスク＝全体改造は壊滅的（漸進は限定的）。複雑×不確実が高いほど差が開く。</summary>
        [Test]
        public void DownsideRisk_全体改造は壊滅的で漸進は限定的()
        {
            float piecemeal = PiecemealEngineeringRules.DownsideRisk(ReformMode.漸進的, 1f, 1f);
            float utopian = PiecemealEngineeringRules.DownsideRisk(ReformMode.全体改造, 1f, 1f);
            // severity=(1+1)/2=1。漸進=0.25*1=0.25、全体改造=0.9*1=0.9。
            Assert.AreEqual(0.25f, piecemeal, Eps);
            Assert.AreEqual(0.9f, utopian, Eps);
            Assert.Greater(utopian, piecemeal);
        }

        /// <summary>取り消し可能性＝漸進は高く（0.9）全体改造は不可逆（0.1）。</summary>
        [Test]
        public void Reversibility_漸進は高く全体改造は不可逆()
        {
            Assert.AreEqual(0.9f, PiecemealEngineeringRules.Reversibility(ReformMode.漸進的), Eps);
            Assert.AreEqual(0.1f, PiecemealEngineeringRules.Reversibility(ReformMode.全体改造), Eps);
        }

        /// <summary>学習速度＝漸進は速く（小さく試せる）全体改造は鈍い（学ぶ前に手遅れ）。</summary>
        [Test]
        public void LearningRate_漸進は速く全体改造は鈍い()
        {
            float piecemeal = PiecemealEngineeringRules.LearningRate(ReformMode.漸進的, 1f);
            float utopian = PiecemealEngineeringRules.LearningRate(ReformMode.全体改造, 1f);
            // 漸進=0.8*1=0.8、全体改造=0.2*1=0.2。
            Assert.AreEqual(0.8f, piecemeal, Eps);
            Assert.AreEqual(0.2f, utopian, Eps);
            Assert.Greater(piecemeal, utopian);
        }

        /// <summary>リスク分布＝同じ下振れでも全体改造は高分散（一発勝負）漸進は低分散（小分けで均す）。</summary>
        [Test]
        public void RiskDistribution_全体改造は高分散で漸進は低分散()
        {
            float dr = 0.8f;
            float piecemeal = PiecemealEngineeringRules.RiskDistribution(ReformMode.漸進的, dr);
            float utopian = PiecemealEngineeringRules.RiskDistribution(ReformMode.全体改造, dr);
            // 漸進=0.8*0.3=0.24、全体改造=0.8*1=0.8。
            Assert.AreEqual(0.24f, piecemeal, Eps);
            Assert.AreEqual(0.8f, utopian, Eps);
            Assert.Greater(utopian, piecemeal);
        }

        /// <summary>最適モード＝不確実性が高いほど漸進が最適（分からない時は小さく試す）。</summary>
        [Test]
        public void OptimalMode_不確実性が高いほど漸進が最適()
        {
            // 不確実・高賭け金・要可逆＝平均0.9≥0.5→漸進。
            Assert.AreEqual(ReformMode.漸進的, PiecemealEngineeringRules.OptimalMode(0.9f, 0.9f, 0.9f));
            // 確実・低賭け金・引き返し不要＝平均0.1<0.5→全体改造で一気に。
            Assert.AreEqual(ReformMode.全体改造, PiecemealEngineeringRules.OptimalMode(0.1f, 0.1f, 0.1f));
        }

        /// <summary>ユートピアの傲慢＝知識の限界を無視するほど（高野心×大きな限界）最大化。野心ゼロで消える。</summary>
        [Test]
        public void UtopianHubris_知識の限界を無視する傲慢()
        {
            // 高野心(1)×大きな知識の限界(1)＝傲慢最大。
            Assert.AreEqual(1.0f, PiecemealEngineeringRules.UtopianHubris(1f, 1f), Eps);
            // 野心はあるが知識の限界が小さい（本当に分かっている）＝傲慢は薄い。
            Assert.AreEqual(0.2f, PiecemealEngineeringRules.UtopianHubris(1f, 0.2f), Eps);
            // 野心ゼロなら傲慢なし。
            Assert.AreEqual(0f, PiecemealEngineeringRules.UtopianHubris(0f, 1f), Eps);
        }

        /// <summary>漸進の積み重ね＝小さな改良が時間をかけて着実に積み上がり後戻りしない。</summary>
        [Test]
        public void CumulativeImprovement_小さな改良が着実に積み上がる()
        {
            // 現状0.5、ステップ0.5、dt=1：0.5+0.8*0.5*1=0.9。
            float improved = PiecemealEngineeringRules.CumulativeImprovement(0.5f, 0.5f, 1f);
            Assert.AreEqual(0.9f, improved, Eps);
            // 1で頭打ち（過剰に積んでも壊れない）。
            float capped = PiecemealEngineeringRules.CumulativeImprovement(0.9f, 1f, 5f);
            Assert.AreEqual(1.0f, capped, Eps);
            // dt=0なら不変（時間が進まなければ積み上がらない）。
            Assert.AreEqual(0.5f, PiecemealEngineeringRules.CumulativeImprovement(0.5f, 1f, 0f), Eps);
        }
    }
}
