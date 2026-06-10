using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 対外援助を固定する：雪中の炭（困窮時の援助は同額でも3倍効く）、敵対国への災害救援は
    /// 平時援助より強く記憶される、紐付き援助の実利と反発のトレードオフ（紐を付けすぎると負）、
    /// 援助依存の形成（形成は速く回復は遅い）、依存させてから切る武器化、援助疲れ。クランプを担保。
    /// </summary>
    public class ForeignAidRulesTests
    {
        private static readonly ForeignAidParams P = ForeignAidParams.Default;
        // 基礎opinion 0.1/困窮上乗せ2/敵対救援2倍/実利0.5/反発0.8/依存形成0.05/自立回復0.01/カットショック0.5/援助疲れ0.01

        [Test]
        public void OpinionGain_NeedMultiplies_雪中の炭()
        {
            // 平時の援助10＝10×0.1×1=1.0
            Assert.AreEqual(1f, ForeignAidRules.OpinionGain(10f, 0f, P), 1e-5f);
            // 困窮最大なら同額で3倍効く＝雪中の炭
            Assert.AreEqual(3f, ForeignAidRules.OpinionGain(10f, 1f, P), 1e-5f);
            // 中間＝2倍
            Assert.AreEqual(2f, ForeignAidRules.OpinionGain(10f, 0.5f, P), 1e-5f);
            // 負の援助は無効・困窮は1にクランプ
            Assert.AreEqual(0f, ForeignAidRules.OpinionGain(-5f, 1f, P), 1e-5f);
            Assert.AreEqual(3f, ForeignAidRules.OpinionGain(10f, 2f, P), 1e-5f);
        }

        [Test]
        public void DisasterDiplomacyBonus_HostileRemembersKindness()
        {
            // 敵対国への災害救援＝10×0.1×2=2.0（敵の善意は記憶に残る）
            Assert.AreEqual(2f, ForeignAidRules.DisasterDiplomacyBonus(10f, true, P), 1e-5f);
            // 友好国への救援＝平時援助と同じ基礎効果
            Assert.AreEqual(1f, ForeignAidRules.DisasterDiplomacyBonus(10f, false, P), 1e-5f);
            // 敵対への救援は平時援助（need0）より強い
            Assert.Greater(ForeignAidRules.DisasterDiplomacyBonus(10f, true, P),
                           ForeignAidRules.OpinionGain(10f, 0f, P));
        }

        [Test]
        public void StringsAttached_TradeoffTurnsNegative()
        {
            // 紐なし＝実利なし
            Assert.AreEqual(0f, ForeignAidRules.StringsAttached(10f, 0f, P), 1e-5f);
            // ほどよい紐＝純益プラス：10×(0.5×0.5−0.8×0.25)=0.5
            Assert.AreEqual(0.5f, ForeignAidRules.StringsAttached(10f, 0.5f, P), 1e-5f);
            // 紐を付けすぎると反発が実利を上回り負：10×(0.5−0.8)=−3
            Assert.AreEqual(-3f, ForeignAidRules.StringsAttached(10f, 1f, P), 1e-5f);
            // 条件度はクランプ（2を渡しても1と同じ）
            Assert.AreEqual(-3f, ForeignAidRules.StringsAttached(10f, 2f, P), 1e-5f);
        }

        [Test]
        public void DependencyTick_GrowsWithFlow_DecaysSlowlyWithout()
        {
            // 援助フロー1×dt10＝0+(0.05×1×1−0)×10=0.5 依存が形成される
            Assert.AreEqual(0.5f, ForeignAidRules.DependencyTick(0f, 1f, 10f, P), 1e-5f);
            // 援助停止＝ゆっくり自立へ戻る：0.5−0.01×0.5×10=0.45
            Assert.AreEqual(0.45f, ForeignAidRules.DependencyTick(0.5f, 0f, 10f, P), 1e-5f);
            // 形成は回復より速い（同じdtで動く量が大きい）
            float grown = ForeignAidRules.DependencyTick(0.5f, 1f, 10f, P) - 0.5f;
            float decayed = 0.5f - ForeignAidRules.DependencyTick(0.5f, 0f, 10f, P);
            Assert.Greater(grown, decayed);
            // 上限1にクランプ
            Assert.AreEqual(1f, ForeignAidRules.DependencyTick(0.9f, 10f, 10f, P), 1e-5f);
        }

        [Test]
        public void SuddenWithdrawalShock_WeaponizedDependency()
        {
            // 依存ゼロの相手を切っても無傷＝依存させてから切るのが武器化
            Assert.AreEqual(0f, ForeignAidRules.SuddenWithdrawalShock(0f, P), 1e-5f);
            Assert.AreEqual(0.25f, ForeignAidRules.SuddenWithdrawalShock(0.5f, P), 1e-5f);
            Assert.AreEqual(0.5f, ForeignAidRules.SuddenWithdrawalShock(1f, P), 1e-5f);
            // 依存はクランプ（2を渡しても1と同じ）
            Assert.AreEqual(0.5f, ForeignAidRules.SuddenWithdrawalShock(2f, P), 1e-5f);
        }

        [Test]
        public void WithdrawalAfterDependence_HurtsMoreThanNeverAiding()
        {
            // 援助を流して依存を形成→カット＝何もしなかった場合より重い打撃を与えられる
            float dep = 0f;
            for (int i = 0; i < 5; i++) dep = ForeignAidRules.DependencyTick(dep, 1f, 2f, P);
            Assert.Greater(dep, 0.3f); // 依存が育っている
            float shock = ForeignAidRules.SuddenWithdrawalShock(dep, P);
            Assert.Greater(shock, ForeignAidRules.SuddenWithdrawalShock(0f, P));
            Assert.Greater(shock, 0.15f);
        }

        [Test]
        public void DonorFatigue_InvisibleResultsExhaustSupport()
        {
            // 成果ゼロの累積援助100＝疲れ最大1.0
            Assert.AreEqual(1f, ForeignAidRules.DonorFatigue(100f, 0f, P), 1e-5f);
            // 成果が完全に見えていれば疲れは積もらない
            Assert.AreEqual(0f, ForeignAidRules.DonorFatigue(100f, 1f, P), 1e-5f);
            // 中間：50×0.01×0.5=0.25
            Assert.AreEqual(0.25f, ForeignAidRules.DonorFatigue(50f, 0.5f, P), 1e-5f);
            // 負の累積は無効
            Assert.AreEqual(0f, ForeignAidRules.DonorFatigue(-10f, 0f, P), 1e-5f);
        }
    }
}
