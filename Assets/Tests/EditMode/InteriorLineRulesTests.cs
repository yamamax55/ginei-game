using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 内線作戦の優位を固定する：内側経路が外側より短いほど内線優位、転用速度は距離×機動、各個撃破は
    /// 敵の連携の遅さに比例、集中比は前線過多で薄まり、中央配置の価値は多方向で活きるが過伸張で破綻する。
    /// 配列の null/空安全と境界を担保。
    /// </summary>
    public class InteriorLineRulesTests
    {
        private static readonly InteriorLineParams P = InteriorLineParams.Default;
        // 分散0.15/過伸張閾値3/急峻さ0.5/転用スケール10

        [Test]
        public void InteriorAdvantage_ShorterInnerPathWins()
        {
            // inner=4, outer=10 → 1-4/10=0.6
            Assert.AreEqual(0.6f, InteriorLineRules.InteriorAdvantage(
                new[] { 2f, 2f }, new[] { 5f, 5f }, P), 1e-4f);
            // 等距離なら優位なし
            Assert.AreEqual(0f, InteriorLineRules.InteriorAdvantage(
                new[] { 5f, 5f }, new[] { 5f, 5f }, P), 1e-4f);
            // 内側が外側より長い＝0にクランプ
            Assert.AreEqual(0f, InteriorLineRules.InteriorAdvantage(
                new[] { 9f, 9f }, new[] { 5f, 5f }, P), 1e-4f);
        }

        [Test]
        public void InteriorAdvantage_NullAndEmptySafe()
        {
            Assert.AreEqual(0f, InteriorLineRules.InteriorAdvantage(null, null, P), 1e-4f);
            Assert.AreEqual(0f, InteriorLineRules.InteriorAdvantage(new float[0], new float[0], P), 1e-4f);
            // 外側が動かない（空/null）なら優位なし
            Assert.AreEqual(0f, InteriorLineRules.InteriorAdvantage(new[] { 2f }, null, P), 1e-4f);
            // 内側が即時転用（空）＝最大優位
            Assert.AreEqual(1f, InteriorLineRules.InteriorAdvantage(new float[0], new[] { 5f }, P), 1e-4f);
        }

        [Test]
        public void RedeploymentSpeed_FasterWhenCloseAndMobile()
        {
            Assert.AreEqual(1f, InteriorLineRules.RedeploymentSpeed(0f, 1f, P), 1e-4f);    // 距離0＝即転用
            Assert.AreEqual(0.5f, InteriorLineRules.RedeploymentSpeed(10f, 1f, P), 1e-4f); // 距離=スケール
            Assert.AreEqual(0.25f, InteriorLineRules.RedeploymentSpeed(10f, 0.5f, P), 1e-4f);
            Assert.AreEqual(0f, InteriorLineRules.RedeploymentSpeed(10f, 0f, P), 1e-4f);   // 機動0＝動けない
        }

        [Test]
        public void DefeatInDetailChance_HigherWhenEnemyCoordinationSlow()
        {
            // window=1/(1+1)=0.5, speed=1 → 0.5
            Assert.AreEqual(0.5f, InteriorLineRules.DefeatInDetailChance(1f, 1f), 1e-4f);
            // window=3/4=0.75, speed=0.8 → 0.6
            Assert.AreEqual(0.6f, InteriorLineRules.DefeatInDetailChance(0.8f, 3f), 1e-4f);
            // 敵が即連携＝隙なし
            Assert.AreEqual(0f, InteriorLineRules.DefeatInDetailChance(1f, 0f), 1e-4f);
        }

        [Test]
        public void ConcentrationRatio_ThinnedByManyFronts()
        {
            // 1前線＝分散なし
            Assert.AreEqual(100f, InteriorLineRules.ConcentrationRatio(100f, 1, P), 1e-4f);
            // 3前線：perFront=33.333, dispersion=1-0.15*2=0.7 → 23.333
            Assert.AreEqual(23.3333f, InteriorLineRules.ConcentrationRatio(100f, 3, P), 1e-3f);
            // 負戦力は0
            Assert.AreEqual(0f, InteriorLineRules.ConcentrationRatio(-50f, 2, P), 1e-4f);
        }

        [Test]
        public void Overstretch_KicksInPastThreshold()
        {
            Assert.AreEqual(0f, InteriorLineRules.Overstretch(3, 100f, P), 1e-4f);   // 閾値ちょうど＝過伸張なし
            // 5前線・戦力100：excess=2, 2*0.5*(10/100)=0.1
            Assert.AreEqual(0.1f, InteriorLineRules.Overstretch(5, 100f, P), 1e-4f);
            // 5前線・戦力10：excess=2, 2*0.5*(10/10)=1.0
            Assert.AreEqual(1f, InteriorLineRules.Overstretch(5, 10f, P), 1e-4f);
        }

        [Test]
        public void ExteriorEncirclementRisk_RisesAsAdvantageCollapses()
        {
            Assert.AreEqual(0f, InteriorLineRules.ExteriorEncirclementRisk(1f), 1e-4f);
            Assert.AreEqual(0.4f, InteriorLineRules.ExteriorEncirclementRisk(0.6f), 1e-4f);
            Assert.AreEqual(1f, InteriorLineRules.ExteriorEncirclementRisk(0f), 1e-4f);
        }

        [Test]
        public void IsInteriorLineFavorable_AboveThreshold()
        {
            Assert.IsTrue(InteriorLineRules.IsInteriorLineFavorable(0.6f));        // 既定0.5超
            Assert.IsFalse(InteriorLineRules.IsInteriorLineFavorable(0.5f));       // 同値は不可
            Assert.IsTrue(InteriorLineRules.IsInteriorLineFavorable(0.3f, 0.2f));  // 閾値指定
        }

        // 物語：中央配置は多方向の敵を各個撃破できるが、前線が増えすぎると内線も破綻する。
        [Test]
        public void Story_CentralPositionThrivesThenCollapsesUnderTooManyFronts()
        {
            // 内線優位0.6・3方向・十分な戦力100：過伸張なし→中央配置が活きる
            // adv=0.6, multiFront=2/3=0.6667, stretch=Overstretch(3,100)=0 → 0.6*0.6667=0.4
            float thriving = InteriorLineRules.CentralPositionValue(0.6f, 3, 100f, P);
            Assert.AreEqual(0.4f, thriving, 1e-3f);
            Assert.IsTrue(thriving > 0f); // 中央配置に価値がある

            // 同じ内線優位でも8方向・薄い戦力10：過伸張が1.0に達して価値が消える
            // multiFront=7/8, stretch=Overstretch(8,10)=clamp(5*0.5*1)=1 → (1-1)=0 → 0
            float collapsed = InteriorLineRules.CentralPositionValue(0.6f, 8, 10f, P);
            Assert.AreEqual(0f, collapsed, 1e-4f);

            // 過伸張が崩壊を裏付ける：8前線・戦力10は完全過伸張
            Assert.AreEqual(1f, InteriorLineRules.Overstretch(8, 10f, P), 1e-4f);
            // 1方向なら中央配置の妙味なし（多方向ボーナス0）
            Assert.AreEqual(0f, InteriorLineRules.CentralPositionValue(0.9f, 1, 100f, P), 1e-4f);
        }
    }
}
