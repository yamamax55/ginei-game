using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国家緊急権を固定する：発動中のみ危機処理が加速、権力集中は長引くほど加速、
    /// 常態化リスク＝危機が去ったのに解除されない度合い（全権委任法の罠）、
    /// 萎縮は集中に比例、復帰難度は萎縮×慢性化、時限条項は執行の信憑性が無ければ紙切れ。境界を担保。
    /// </summary>
    public class EmergencyPowersRulesTests
    {
        private static readonly EmergencyPowersParams P = EmergencyPowersParams.Default;
        // 処理倍率2/集中0.02/常態化開始20/萎縮0.05/慢性化60

        [Test]
        public void CrisisResponseSpeed_OnlyWhilePowersActive()
        {
            Assert.AreEqual(2f, EmergencyPowersRules.CrisisResponseSpeed(true, P), 1e-5f);  // 本来の効用
            Assert.AreEqual(1f, EmergencyPowersRules.CrisisResponseSpeed(false, P), 1e-5f); // 平時＝等速
        }

        [Test]
        public void PowerConcentrationTick_AcceleratesWithDuration()
        {
            // 発動直後：0.5＋0.02×(1+0/20)＝0.52
            Assert.AreEqual(0.52f, EmergencyPowersRules.PowerConcentrationTick(0.5f, 0f, 1f, P), 1e-5f);
            // 常態化開始ちょうど：0.5＋0.02×(1+20/20)＝0.54＝集中は集中を呼ぶ
            Assert.AreEqual(0.54f, EmergencyPowersRules.PowerConcentrationTick(0.5f, 20f, 1f, P), 1e-5f);
            // 上限1にクランプ
            Assert.AreEqual(1f, EmergencyPowersRules.PowerConcentrationTick(0.99f, 100f, 1f, P), 1e-5f);
        }

        [Test]
        public void NormalizationRisk_ZeroWhileCrisisIsReal()
        {
            // 本物の危機が続く限り、どれだけ長くても延長は正当＝リスク0
            Assert.AreEqual(0f, EmergencyPowersRules.NormalizationRisk(100f, 1f, P), 1e-5f);
            // 開始前は危機が去っていてもまだリスクにならない
            Assert.AreEqual(0f, EmergencyPowersRules.NormalizationRisk(10f, 0f, P), 1e-5f);
        }

        [Test]
        public void NormalizationRisk_GrowsWhenCrisisGoneButPowersRemain()
        {
            // 全権委任法の罠：危機0×継続30＝(30-20)/20=0.5
            Assert.AreEqual(0.5f, EmergencyPowersRules.NormalizationRisk(30f, 0f, P), 1e-5f);
            // 継続40で飽和＝1（終わらない非常事態）
            Assert.AreEqual(1f, EmergencyPowersRules.NormalizationRisk(40f, 0f, P), 1e-5f);
            // 危機が半分残っていればリスクも半分＝0.5×0.5
            Assert.AreEqual(0.25f, EmergencyPowersRules.NormalizationRisk(30f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void InstitutionalAtrophyTick_ProportionalToConcentration()
        {
            // 0.2＋0.05×0.5＝0.225＝迂回される機関ほど速く痩せる
            Assert.AreEqual(0.225f, EmergencyPowersRules.InstitutionalAtrophyTick(0.2f, 0.5f, 1f, P), 1e-5f);
            // 集中0なら萎縮は進まない
            Assert.AreEqual(0.2f, EmergencyPowersRules.InstitutionalAtrophyTick(0.2f, 0f, 1f, P), 1e-5f);
            // 上限1にクランプ
            Assert.AreEqual(1f, EmergencyPowersRules.InstitutionalAtrophyTick(0.99f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void RestorationDifficulty_AtrophyAndChronicDuration()
        {
            Assert.AreEqual(0f, EmergencyPowersRules.RestorationDifficulty(0f, 0f, P), 1e-5f);   // 早解除＝安い
            // 0.6×0.5＋0.4×(30/60)＝0.5
            Assert.AreEqual(0.5f, EmergencyPowersRules.RestorationDifficulty(0.5f, 30f, P), 1e-5f);
            // 完全萎縮×慢性化＝1＝制度は戻し方を忘れた
            Assert.AreEqual(1f, EmergencyPowersRules.RestorationDifficulty(1f, 60f, P), 1e-5f);
        }

        [Test]
        public void SunsetClauseValue_NeedsBothClauseAndCredibility()
        {
            Assert.AreEqual(0.8f, EmergencyPowersRules.SunsetClauseValue(true, 0.8f), 1e-5f); // 生きた時限
            Assert.AreEqual(0f, EmergencyPowersRules.SunsetClauseValue(true, 0f), 1e-5f);     // 紙の上の時限＝全権委任法
            Assert.AreEqual(0f, EmergencyPowersRules.SunsetClauseValue(false, 1f), 1e-5f);    // 条項なし
        }
    }
}
