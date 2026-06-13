using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 宮廷の寵愛・讒言システムを固定する：寵は近侍×追従で速く・実績で遅く積もり、常に減衰する
    /// （「寵は日なた＝日は必ず動く」）。寵臣の専横は寵の二乗×監督の緩み、讒言は君主の眼力が防波堤
    /// （眼力1で無効）、追従の寵は一夜で消え実績の寵は粘り、陰謀度は廷臣数×寵の一極集中で激化する。
    /// 既定Paramsの具体値で担保。
    /// </summary>
    public class CourtFavorRulesTests
    {
        private static readonly CourtFavorParams P = CourtFavorParams.Default;

        [Test]
        public void FavorTick_FlatteryFast_MeritSlow_SunAlwaysMoves()
        {
            // 近侍×追従：0.5+(0.10-0.02)=0.58/dt＝速い
            Assert.AreEqual(0.58f, CourtFavorRules.FavorTick(0.5f, 1f, 1f, 0f, 1f, P), 1e-5f);
            // 実績のみ：0.5+(0.04-0.02)=0.52/dt＝遅い
            Assert.AreEqual(0.52f, CourtFavorRules.FavorTick(0.5f, 1f, 0f, 1f, 1f, P), 1e-5f);
            // 日は必ず動く：君主から遠ざかれば（proximity=0）寵は冷えるのみ＝0.5-0.02=0.48
            Assert.AreEqual(0.48f, CourtFavorRules.FavorTick(0.5f, 0f, 1f, 1f, 1f, P), 1e-5f);
            // 0..1 を超えない（クランプ）
            Assert.AreEqual(0f, CourtFavorRules.FavorTick(0.01f, 0f, 0f, 0f, 1f, P), 1e-5f);
            Assert.AreEqual(1f, CourtFavorRules.FavorTick(0.99f, 1f, 1f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void FavoriteTyranny_QuadraticInFavor_OversightSuppresses()
        {
            // 寵の二乗×監督の緩み：寵2倍で専横4倍＝深い寵ほど不釣り合いに増長
            Assert.AreEqual(0.8f, CourtFavorRules.FavoriteTyranny(1f, 0f), 1e-5f);  // 1²×1×0.8
            Assert.AreEqual(0.2f, CourtFavorRules.FavoriteTyranny(0.5f, 0f), 1e-5f); // 0.25×0.8
            // 君主の監督が行き届けば寵が深くても専横は抑えられる
            Assert.AreEqual(0f, CourtFavorRules.FavoriteTyranny(1f, 1f), 1e-5f);
            Assert.AreEqual(0.4f, CourtFavorRules.FavoriteTyranny(1f, 0.5f), 1e-5f);
        }

        [Test]
        public void SlanderEffect_DiscernmentIsTheBreakwater()
        {
            // 眼力0：讒言は素通り＝1×1×1×0.5=0.5
            Assert.AreEqual(0.5f, CourtFavorRules.SlanderEffect(1f, 1f, 0f), 1e-5f);
            // 眼力1：讒言は完全に無効（防波堤）
            Assert.AreEqual(0f, CourtFavorRules.SlanderEffect(1f, 1f, 1f), 1e-5f);
            // 眼力0.5：半分だけ通る
            Assert.AreEqual(0.25f, CourtFavorRules.SlanderEffect(1f, 1f, 0.5f), 1e-5f);
        }

        [Test]
        public void SlanderEffect_HighFavorIsTheTarget()
        {
            // 寵が高い者ほど落とせる落差が大きい＝讒言の的
            Assert.AreEqual(0.25f, CourtFavorRules.SlanderEffect(1f, 0.5f, 0f), 1e-5f);
            // 寵が無い者への讒言は無意味
            Assert.AreEqual(0f, CourtFavorRules.SlanderEffect(1f, 0f, 0f), 1e-5f);
            // 質の低い讒言は通りも低い
            Assert.AreEqual(0.25f, CourtFavorRules.SlanderEffect(0.5f, 1f, 0f), 1e-5f);
        }

        [Test]
        public void FavorVolatility_FlatteryFavorEvaporates_MeritFavorSticks()
        {
            // 追従で得た寵は一夜で消える（揺らぎ0.5）、実績の寵は粘る（揺らぎ0.05）
            Assert.AreEqual(0.5f, CourtFavorRules.FavorVolatility(1f), 1e-5f);
            Assert.AreEqual(0.05f, CourtFavorRules.FavorVolatility(0f), 1e-5f);
            // 中間は線形補間：0.05+(0.5-0.05)×0.5=0.275
            Assert.AreEqual(0.275f, CourtFavorRules.FavorVolatility(0.5f), 1e-5f);
            // 入力クランプ
            Assert.AreEqual(0.5f, CourtFavorRules.FavorVolatility(2f), 1e-5f);
        }

        [Test]
        public void CourtIntrigueLevel_CrowdAndConcentrationBreedIntrigue()
        {
            // 飽和曲線：半値廷臣数4＝4人で0.5、12人で0.75
            Assert.AreEqual(0.5f, CourtFavorRules.CourtIntrigueLevel(4, 1f), 1e-5f);
            Assert.AreEqual(0.75f, CourtFavorRules.CourtIntrigueLevel(12, 1f), 1e-5f);
            // 寵が分散していれば蹴落とし合いも緩む
            Assert.AreEqual(0.25f, CourtFavorRules.CourtIntrigueLevel(4, 0.5f), 1e-5f);
            // 廷臣が居なければ陰謀も無い
            Assert.AreEqual(0f, CourtFavorRules.CourtIntrigueLevel(0, 1f), 1e-5f);
        }

        [Test]
        public void Params_CtorClampsToValidRange()
        {
            var p = new CourtFavorParams(-1f, -1f, -0.5f, -0.1f, -2f, 2f, -0.5f, 0f);
            Assert.AreEqual(0f, p.flatteryGainRate, 1e-5f);
            Assert.AreEqual(0f, p.meritGainRate, 1e-5f);
            Assert.AreEqual(0f, p.favorDecayRate, 1e-5f);
            Assert.AreEqual(0f, p.tyrannyScale, 1e-5f);
            Assert.AreEqual(0f, p.slanderScale, 1e-5f);
            Assert.AreEqual(1f, p.flatteryVolatility, 1e-5f);
            Assert.AreEqual(0f, p.meritVolatility, 1e-5f);
            Assert.AreEqual(1f, p.intrigueHalfCount, 1e-5f);
        }
    }
}
