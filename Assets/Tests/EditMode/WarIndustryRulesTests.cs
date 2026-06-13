using NUnit.Framework;
using UnityEngine;
using Ginei;
using WIParams = Ginei.WarIndustryRules.WarIndustryParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍産複合体＝戦争利得のロビー力学（WarIndustryRules）を固定する：軍需シェアの戦時拡大と粘性
    /// （深まりやすく解けにくい）、講和抵抗（雇用を握ると「平和は失業」）、脅威誇張の誘因（情報の汚染源）、
    /// 天下りの癒着（監督の弱さ×産業規模）、転換費用（軍縮の最大の敵は経済構造＝超線形）、
    /// 恒久戦争均衡（継続圧≥閾値＝講和が割に合わない構造・強い監督は歯止めになる）。既定 Params の具体値で期待値固定。
    /// </summary>
    public class WarIndustryRulesTests
    {
        // シェア更新：戦争20単位で目標0.325（飽和の中点）、平時は0.05へ戻る。dt0は無変化・目標を行き過ぎない
        [Test]
        public void IndustryShareTick_DefaultParams_FixedValues()
        {
            // 戦時成長：target=Lerp(0.05,0.6,20/(20+20))=0.325 → 0.1+0.1=0.2
            Assert.AreEqual(0.2f, WarIndustryRules.IndustryShareTick(0.1f, 20f, 1f), 1e-4f);
            // 超長期戦：目標は上限0.6へ飽和（それ以上は深まらない）
            Assert.AreEqual(0.6f, WarIndustryRules.IndustryShareTick(0.6f, 1e6f, 1f), 1e-3f);
            // 平時：0.05へ戻るが縮小は遅い（growth0.1×shrinkRatio0.5=0.05/tick）
            Assert.AreEqual(0.25f, WarIndustryRules.IndustryShareTick(0.3f, 0f, 1f), 1e-4f);
            // dt0/負は無変化・目標を行き過ぎない
            Assert.AreEqual(0.1f, WarIndustryRules.IndustryShareTick(0.1f, 20f, 0f), 1e-4f);
            Assert.AreEqual(0.1f, WarIndustryRules.IndustryShareTick(0.1f, 20f, -1f), 1e-4f);
            Assert.AreEqual(0.325f, WarIndustryRules.IndustryShareTick(0.32f, 20f, 1f), 1e-4f);
        }

        // 粘性：同じ目標へ向かう成長は+0.1/tick・縮小は−0.05/tick＝依存は深まりやすく解けにくい
        [Test]
        public void IndustryShareTick_GrowsFastShrinksSlow()
        {
            float growStep = WarIndustryRules.IndustryShareTick(0.2f, 20f, 1f) - 0.2f;   // target0.325へ成長
            float shrinkStep = 0.45f - WarIndustryRules.IndustryShareTick(0.45f, 20f, 1f); // target0.325へ縮小
            Assert.AreEqual(0.1f, growStep, 1e-4f);
            Assert.AreEqual(0.05f, shrinkStep, 1e-4f);
            Assert.Greater(growStep, shrinkStep); // 構造の粘性
        }

        // 講和抵抗：利潤動機だけで半分（emp0でshare×0.5）、雇用を握ると倍化（emp1でshare×1）＝平和は失業
        [Test]
        public void PeaceResistance_EmploymentMakesPeaceUnemployment()
        {
            Assert.AreEqual(0.25f, WarIndustryRules.PeaceResistance(0.5f, 0f), 1e-4f); // 利潤動機のみ
            Assert.AreEqual(0.5f, WarIndustryRules.PeaceResistance(0.5f, 1f), 1e-4f);  // 雇用依存で倍
            Assert.AreEqual(0f, WarIndustryRules.PeaceResistance(0f, 1f), 1e-4f);      // 産業なし＝抵抗なし
            Assert.AreEqual(1f, WarIndustryRules.PeaceResistance(1f, 1f), 1e-4f);
            Assert.AreEqual(1f, WarIndustryRules.PeaceResistance(2f, 2f), 1e-4f);      // 入力クランプ
        }

        // 脅威誇張：平時シェア0.05以下は0（誇張する予算動機なし）、0.525で0.5、完全依存で1
        [Test]
        public void ThreatInflationIncentive_DefaultParams_FixedValues()
        {
            Assert.AreEqual(0f, WarIndustryRules.ThreatInflationIncentive(0.05f), 1e-4f);
            Assert.AreEqual(0f, WarIndustryRules.ThreatInflationIncentive(0.01f), 1e-4f);
            Assert.AreEqual(0.5f, WarIndustryRules.ThreatInflationIncentive(0.525f), 1e-4f); // t=(0.525-0.05)/0.95=0.5
            Assert.AreEqual(1f, WarIndustryRules.ThreatInflationIncentive(1f), 1e-4f);
            Assert.AreEqual(1f, WarIndustryRules.ThreatInflationIncentive(5f), 1e-4f);       // 入力クランプ
        }

        // 天下り癒着：シェア×(1−監督)。完全監督なら0・無監督の巨大産業で1
        [Test]
        public void RevolvingDoorCorruption_OversightSuppresses()
        {
            Assert.AreEqual(0.25f, WarIndustryRules.RevolvingDoorCorruption(0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(1f, WarIndustryRules.RevolvingDoorCorruption(1f, 0f), 1e-4f);  // 野放し＝完全癒着
            Assert.AreEqual(0f, WarIndustryRules.RevolvingDoorCorruption(1f, 1f), 1e-4f);  // 完全監督＝癒着なし
            Assert.AreEqual(0f, WarIndustryRules.RevolvingDoorCorruption(0f, 0f), 1e-4f);  // 産業なし＝癒着なし
        }

        // 転換費用：シェアの2乗＝浅い依存の軍縮は安く深い依存は超線形に痛い（0.3→0.09・0.6→0.36）
        [Test]
        public void ConversionCost_DeepDependenceIsSuperlinear()
        {
            float shallow = WarIndustryRules.ConversionCost(0.3f);
            float deep = WarIndustryRules.ConversionCost(0.6f);
            Assert.AreEqual(0.09f, shallow, 1e-4f);
            Assert.AreEqual(0.36f, deep, 1e-4f);
            Assert.AreEqual(1f, WarIndustryRules.ConversionCost(1f), 1e-4f);
            Assert.AreEqual(0f, WarIndustryRules.ConversionCost(0f), 1e-4f);
            Assert.Greater(deep, shallow * 3f); // シェア2倍で費用4倍＝超線形（早い軍縮は安い）
        }

        // 恒久戦争均衡：継続圧＝抵抗＋転換費用−実効監督（監督×(1−癒着)）が閾値0.5以上で成立
        [Test]
        public void PerpetualWarEquilibrium_DeepEmployedUnchecked_IsRational()
        {
            // 深い依存×雇用×弱監督：0.6+0.36−0.2×(1−0.48)=0.856 → 均衡成立
            Assert.AreEqual(0.856f, WarIndustryRules.WarContinuationPressure(0.6f, 1f, 0.2f), 1e-4f);
            Assert.IsTrue(WarIndustryRules.PerpetualWarEquilibrium(0.6f, 1f, 0.2f));
            // 雇用依存ゼロでも野放しなら成立：0.3+0.36−0=0.66 → 利潤動機だけで戦争が続く
            Assert.IsTrue(WarIndustryRules.PerpetualWarEquilibrium(0.6f, 0f, 0f));
            // 浅い依存×強い監督：0.075+0.01−0.784=−0.699 → 不成立
            Assert.IsFalse(WarIndustryRules.PerpetualWarEquilibrium(0.1f, 0.5f, 0.8f));
        }

        // 歯止め：同じ深い依存でも完全監督なら癒着0で監督が骨抜きにならず均衡は崩せる
        [Test]
        public void PerpetualWarEquilibrium_StrongOversightBreaksIt()
        {
            // 0.6+0.36−1×(1−0)=−0.04 → 不成立＝監督は効く
            Assert.AreEqual(-0.04f, WarIndustryRules.WarContinuationPressure(0.6f, 1f, 1f), 1e-4f);
            Assert.IsFalse(WarIndustryRules.PerpetualWarEquilibrium(0.6f, 1f, 1f));
            // 同条件で監督を弱めると成立＝差は監督の有無だけ（構造が同じでも歯止めで結末が変わる）
            Assert.IsTrue(WarIndustryRules.PerpetualWarEquilibrium(0.6f, 1f, 0.2f));
        }

        // Params ctor：全フィールドがクランプされる（負・範囲外を入れても安全）
        [Test]
        public void Params_Constructor_ClampsAllFields()
        {
            var p = new WIParams(-1f, 2f, 0f, -1f, 5f, -1f, -1f, -1f, 0.5f, -1f, 5f);
            Assert.AreEqual(0f, p.peacetimeShare, 1e-4f);
            Assert.AreEqual(1f, p.maxShare, 1e-4f);
            Assert.AreEqual(0.01f, p.warSaturation, 1e-4f);  // 0除算防止
            Assert.AreEqual(0f, p.growthRate, 1e-4f);
            Assert.AreEqual(1f, p.shrinkRatio, 1e-4f);
            Assert.AreEqual(0f, p.resistanceScale, 1e-4f);
            Assert.AreEqual(1f, p.conversionExponent, 1e-4f); // 線形未満にしない
            Assert.AreEqual(2f, p.equilibriumThreshold, 1e-4f);
            // maxShare は peacetimeShare 未満にならない
            var q = new WIParams(0.5f, 0.1f, 20f, 0.1f, 0.5f, 1f, 1f, 1f, 2f, 1f, 0.5f);
            Assert.AreEqual(0.5f, q.maxShare, 1e-4f);
        }
    }
}
