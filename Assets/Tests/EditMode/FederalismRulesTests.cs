using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>連邦制の純ロジック（垂直の権力分立）の EditMode テスト。決定論・既定 Params で期待値固定。</summary>
    public class FederalismRulesTests
    {
        const float Eps = 1e-4f;

        // --- LocalFitness ---

        [Test]
        public void LocalFitness_画一政策は多様な国で外れ_分権で回復()
        {
            // 多様1×集権0＝完全に外れる
            Assert.AreEqual(0f, FederalismRules.LocalFitness(0f, 1f), Eps);
            // 完全分権なら多様でも完全適合
            Assert.AreEqual(1f, FederalismRules.LocalFitness(1f, 1f), Eps);
            // 中間：1 - 0.8*(1-0.5) = 0.6
            Assert.AreEqual(0.6f, FederalismRules.LocalFitness(0.5f, 0.8f), Eps);
            // 均質な国は画一政策でも常に適合
            Assert.AreEqual(1f, FederalismRules.LocalFitness(0f, 0f), Eps);
            // 範囲外入力はクランプ（devolution=2→1, diversity=-1→0）
            Assert.AreEqual(1f, FederalismRules.LocalFitness(2f, -1f), Eps);
        }

        // --- UnifiedActionSpeed ---

        [Test]
        public void UnifiedActionSpeed_分権ほど鈍る_既定下限04()
        {
            // 集権＝最速1.0
            Assert.AreEqual(1f, FederalismRules.UnifiedActionSpeed(0f), Eps);
            // 完全分権＝既定下限0.4（停止はしない）
            Assert.AreEqual(0.4f, FederalismRules.UnifiedActionSpeed(1f), Eps);
            // 中間は線形：Lerp(1, 0.4, 0.5) = 0.7
            Assert.AreEqual(0.7f, FederalismRules.UnifiedActionSpeed(0.5f), Eps);
            // 単調減少
            Assert.Greater(FederalismRules.UnifiedActionSpeed(0.2f), FederalismRules.UnifiedActionSpeed(0.8f));
        }

        // --- PolicyExperimentValue ---

        [Test]
        public void PolicyExperimentValue_州数の収穫逓減と飽和()
        {
            // 1州以下＝比較不能で0
            Assert.AreEqual(0f, FederalismRules.PolicyExperimentValue(1f, 1), Eps);
            Assert.AreEqual(0f, FederalismRules.PolicyExperimentValue(1f, 0), Eps);
            // 2州：1*(1-1/2) = 0.5
            Assert.AreEqual(0.5f, FederalismRules.PolicyExperimentValue(1f, 2), Eps);
            // 既定キャップ10州：1*(1-1/10) = 0.9
            Assert.AreEqual(0.9f, FederalismRules.PolicyExperimentValue(1f, 10), Eps);
            // キャップ超えは飽和＝10州と同値
            Assert.AreEqual(0.9f, FederalismRules.PolicyExperimentValue(1f, 100), Eps);
            // 集権では州が幾つあっても実験できず0
            Assert.AreEqual(0f, FederalismRules.PolicyExperimentValue(0f, 10), Eps);
        }

        // --- SecessionGradient ---

        [Test]
        public void SecessionGradient_自治と地域意識の積_どちらか0で0()
        {
            // 自治が無ければ独立の足場も無い
            Assert.AreEqual(0f, FederalismRules.SecessionGradient(0f, 1f), Eps);
            // 地域意識が無ければ自治は安全
            Assert.AreEqual(0f, FederalismRules.SecessionGradient(1f, 0f), Eps);
            // 既定slope=1：0.5*0.8 = 0.4
            Assert.AreEqual(0.4f, FederalismRules.SecessionGradient(0.5f, 0.8f), Eps);
            // 最大の滑り坂
            Assert.AreEqual(1f, FederalismRules.SecessionGradient(1f, 1f), Eps);
            // slope調整＋上限クランプ：2*0.8*0.8=1.28 → 1
            var steep = new FederalismParams(0.4f, 10, 2f, 1f);
            Assert.AreEqual(1f, FederalismRules.SecessionGradient(0.8f, 0.8f, steep), Eps);
        }

        // --- CentralizationPressure ---

        [Test]
        public void CentralizationPressure_脅威に比例_重みで増幅()
        {
            // 平時＝圧力なし／総力戦＝最大
            Assert.AreEqual(0f, FederalismRules.CentralizationPressure(0f), Eps);
            Assert.AreEqual(1f, FederalismRules.CentralizationPressure(1f), Eps);
            // 既定weight=1：そのまま比例
            Assert.AreEqual(0.6f, FederalismRules.CentralizationPressure(0.6f), Eps);
            // 重み2なら小脅威でも強く振れる（上限1クランプ）：0.6*2=1.2 → 1
            var jumpy = new FederalismParams(0.4f, 10, 1f, 2f);
            Assert.AreEqual(1f, FederalismRules.CentralizationPressure(0.6f, jumpy), Eps);
            // 範囲外脅威はクランプ
            Assert.AreEqual(1f, FederalismRules.CentralizationPressure(5f), Eps);
        }

        // --- OptimalDevolution（均衡点の妥当性＝振り子の力学） ---

        [Test]
        public void OptimalDevolution_平時は分権_戦時は集権の振り子()
        {
            // 平時×多様＝多様性ぶん分権が賢い
            Assert.AreEqual(0.8f, FederalismRules.OptimalDevolution(0.8f, 0f), Eps);
            // 総力戦＝多様でも集権が速い
            Assert.AreEqual(0f, FederalismRules.OptimalDevolution(0.8f, 1f), Eps);
            // 中間：0.8*(1-0.5) = 0.4
            Assert.AreEqual(0.4f, FederalismRules.OptimalDevolution(0.8f, 0.5f), Eps);
            // 均質な国は分権の利得が無く常に0
            Assert.AreEqual(0f, FederalismRules.OptimalDevolution(0f, 0f), Eps);
            // 妥当性：多様性に単調増加・脅威に単調減少（綱引きの向き）
            Assert.Greater(FederalismRules.OptimalDevolution(0.9f, 0.3f), FederalismRules.OptimalDevolution(0.4f, 0.3f));
            Assert.Greater(FederalismRules.OptimalDevolution(0.7f, 0.2f), FederalismRules.OptimalDevolution(0.7f, 0.8f));
            // 妥当性：均衡点では戦時ほど統一行動が速くなる側へ寄る（分権↓→速度↑）
            float peaceOpt = FederalismRules.OptimalDevolution(1f, 0f);
            float warOpt = FederalismRules.OptimalDevolution(1f, 0.9f);
            Assert.Greater(FederalismRules.UnifiedActionSpeed(warOpt), FederalismRules.UnifiedActionSpeed(peaceOpt));
        }

        // --- FederalismParams ---

        [Test]
        public void FederalismParams_クランプとDefault()
        {
            var p = FederalismParams.Default;
            Assert.AreEqual(0.4f, p.minUnifiedSpeed, Eps);
            Assert.AreEqual(10, p.experimentRegionCap);
            Assert.AreEqual(1f, p.secessionSlope, Eps);
            Assert.AreEqual(1f, p.warCentralizationWeight, Eps);
            // ctor クランプ：速度0..1／州数最低1／負の傾き・重みは0
            var bad = new FederalismParams(5f, 0, -1f, -2f);
            Assert.AreEqual(1f, bad.minUnifiedSpeed, Eps);
            Assert.AreEqual(1, bad.experimentRegionCap);
            Assert.AreEqual(0f, bad.secessionSlope, Eps);
            Assert.AreEqual(0f, bad.warCentralizationWeight, Eps);
        }
    }
}
