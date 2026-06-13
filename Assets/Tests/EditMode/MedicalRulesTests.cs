using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 救護・衛生を固定する：重傷生存率は救護能力で線形、損耗内訳は保存則(dead+returning+invalided=損耗)、
    /// 黄金の1時間が生存に乗算、医療崩壊のトリアージ下限、衛生投資の配当（見えない兵力）。境界を担保。
    /// </summary>
    public class MedicalRulesTests
    {
        private static readonly MedicalParams P = MedicalParams.Default;
        // 即死0.3/軽傷0.3(重傷0.4)/生存0.2〜0.9/傷痍0.25/黄金下限0.5/崩壊下限0.3/経験0.8/士気0.1

        [Test]
        public void SevereSurvival_LinearInCapacity()
        {
            Assert.AreEqual(0.2f, MedicalRules.SevereSurvival(0f, P), 1e-5f);
            Assert.AreEqual(0.9f, MedicalRules.SevereSurvival(1f, P), 1e-5f);
            Assert.AreEqual(0.55f, MedicalRules.SevereSurvival(0.5f, P), 1e-5f);
        }

        [Test]
        public void CasualtySplit_ConservesTotal_AndCapacityMatters()
        {
            // 救護万全（能力1）：severe40→saved36、dead=30+4=34、invalided=9、returning=57
            var hi = MedicalRules.CasualtySplit(100f, 1f, P);
            Assert.AreEqual(34f, hi.dead, 1e-4f);
            Assert.AreEqual(57f, hi.returning, 1e-4f);
            Assert.AreEqual(9f, hi.invalided, 1e-4f);
            Assert.AreEqual(100f, hi.Total, 1e-4f); // 保存則
            // 救護なし（能力0）：saved8、dead62、returning36、invalided2
            var lo = MedicalRules.CasualtySplit(100f, 0f, P);
            Assert.AreEqual(62f, lo.dead, 1e-4f);
            Assert.AreEqual(100f, lo.Total, 1e-4f);
            // 救護が重傷者の生死を分ける＝戻る兵が増え死者が減る
            Assert.Greater(hi.returning, lo.returning);
            Assert.Less(hi.dead, lo.dead);
        }

        [Test]
        public void GoldenHour_MultipliesSurvival()
        {
            Assert.AreEqual(1f, MedicalRules.GoldenHourEffect(1f, P), 1e-5f);
            Assert.AreEqual(0.5f, MedicalRules.GoldenHourEffect(0f, P), 1e-5f);   // 後送遅れで手遅れ
            Assert.AreEqual(0.75f, MedicalRules.GoldenHourEffect(0.5f, P), 1e-5f);
            // 後送込みの内訳：能力1×後送0＝生存0.45、saved18、dead52
            var slow = MedicalRules.CasualtySplit(100f, 1f, 0f, P);
            Assert.AreEqual(52f, slow.dead, 1e-4f);
            Assert.AreEqual(100f, slow.Total, 1e-4f);
        }

        [Test]
        public void ReturnRate_AndDividend()
        {
            Assert.AreEqual(0.57f, MedicalRules.ReturnRate(1f, P), 1e-5f);
            Assert.AreEqual(0.36f, MedicalRules.ReturnRate(0f, P), 1e-5f);
            // 衛生投資の配当＝同じ損耗で救護あり−なしの復帰差＝見えない兵力
            Assert.AreEqual(21f, MedicalRules.MedicalDividend(100f, 1f, P), 1e-4f);
            Assert.AreEqual(0f, MedicalRules.MedicalDividend(100f, 0f, P), 1e-4f);
        }

        [Test]
        public void CapacityOverloadPenalty_TriageFloor()
        {
            Assert.AreEqual(1f, MedicalRules.CapacityOverloadPenalty(0.5f, 1f, P), 1e-5f);  // 処理能力内
            Assert.AreEqual(0.5f, MedicalRules.CapacityOverloadPenalty(1f, 0.5f, P), 1e-5f); // 超過＝崩落
            Assert.AreEqual(0.3f, MedicalRules.CapacityOverloadPenalty(1f, 0.2f, P), 1e-5f); // 下限で頭打ち
        }

        [Test]
        public void VeteranPreservation_AndMoraleAssurance()
        {
            Assert.AreEqual(0.456f, MedicalRules.VeteranPreservation(0.57f, P), 1e-5f); // 0.57×0.8
            Assert.AreEqual(1.1f, MedicalRules.MoraleAssurance(1f, P), 1e-5f);          // 拾ってもらえる安心
            Assert.AreEqual(1f, MedicalRules.MoraleAssurance(0f, P), 1e-5f);
        }
    }
}
