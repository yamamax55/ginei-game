using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 過剰拡張（ポール・ケネディ型）を固定する：公約負担は版図・前線・条約で膨らみ、負担/国力の比が
    /// 1を超えると過伸張ペナルティが非線形に効き（比1.0境界までは無傷）、超過は帝国の国力を蝕み、
    /// 戦略的収縮は過伸張時にのみ負担を下げる（撤退でしか治らない）、防御力は正面数で薄まる。
    /// クランプを担保。既定＝版図重み1/前線重み2/条約重み0.5/冪指数2/ペナルティ上限0.9/衰退率0.05/収縮率0.2。
    /// </summary>
    public class OverextensionRulesTests
    {
        private static readonly OverextensionParams P = OverextensionParams.Default;

        [Test]
        public void CommitmentBurden_GrowsWithTerritoryFrontierTreaty()
        {
            // 版図10×1 + 前線5×2 = 20、条約義務0なら増幅なし
            Assert.AreEqual(20f, OverextensionRules.CommitmentBurden(10f, 5f, 0f, P), 1e-4f);
            // 条約義務1.0＝(1+1×0.5)=1.5倍に増幅
            Assert.AreEqual(30f, OverextensionRules.CommitmentBurden(10f, 5f, 1f, P), 1e-4f);
            // 守るものが増えるほど負担増（単調増）
            Assert.Greater(OverextensionRules.CommitmentBurden(20f, 5f, 0f, P),
                           OverextensionRules.CommitmentBurden(10f, 5f, 0f, P));
            // 負入力はクランプ
            Assert.AreEqual(0f, OverextensionRules.CommitmentBurden(-10f, -5f, -1f, P), 1e-4f);
        }

        [Test]
        public void BurdenToCapacityRatio_OverOneIsOverextended()
        {
            // 負担120／国力100＝1.2＝過伸張
            Assert.AreEqual(1.2f, OverextensionRules.BurdenToCapacityRatio(120f, 100f), 1e-4f);
            // 負担80／国力100＝0.8＝余裕あり
            Assert.AreEqual(0.8f, OverextensionRules.BurdenToCapacityRatio(80f, 100f), 1e-4f);
            // 国力ゼロ＝事実上の崩壊（巨大な比）
            Assert.Greater(OverextensionRules.BurdenToCapacityRatio(50f, 0f), 100f);
            // 負担も国力もゼロ＝0
            Assert.AreEqual(0f, OverextensionRules.BurdenToCapacityRatio(0f, 0f), 1e-4f);
        }

        [Test]
        public void OverstretchPenalty_ZeroUntilRatioOne_ThenNonlinear()
        {
            // 比1.0境界まではペナルティ0＝国力内なら無傷
            Assert.AreEqual(0f, OverextensionRules.OverstretchPenalty(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, OverextensionRules.OverstretchPenalty(1f, P), 1e-5f);
            // 比1.5＝超過0.5^2=0.25
            Assert.AreEqual(0.25f, OverextensionRules.OverstretchPenalty(1.5f, P), 1e-5f);
            // 守るものが増える（比が伸びる）ほど加速して弱くなる（非線形）
            Assert.Greater(OverextensionRules.OverstretchPenalty(2f, P) - OverextensionRules.OverstretchPenalty(1.5f, P),
                           OverextensionRules.OverstretchPenalty(1.5f, P) - OverextensionRules.OverstretchPenalty(1f, P));
            // 上限0.9でクランプ（暴走しても各地全滅まではいかない）
            Assert.AreEqual(0.9f, OverextensionRules.OverstretchPenalty(5f, P), 1e-5f);
        }

        [Test]
        public void ImperialDecayTick_OnlyOverextensionErodesPower()
        {
            // 過伸張していない（比0.8）＝国力は減らない
            Assert.AreEqual(100f, OverextensionRules.ImperialDecayTick(100f, 0.8f, 1f, P), 1e-4f);
            Assert.AreEqual(100f, OverextensionRules.ImperialDecayTick(100f, 1f, 1f, P), 1e-4f);
            // 比1.5＝超過0.5×衰退率0.05×dt1 = 0.025 減る
            Assert.AreEqual(99.975f, OverextensionRules.ImperialDecayTick(100f, 1.5f, 1f, P), 1e-4f);
            // 過伸張が深いほど速く蝕まれる（軍事費が経済を圧迫）
            Assert.Less(OverextensionRules.ImperialDecayTick(100f, 3f, 10f, P),
                        OverextensionRules.ImperialDecayTick(100f, 1.5f, 10f, P));
            // 国力は0で下げ止まる
            Assert.AreEqual(0f, OverextensionRules.ImperialDecayTick(0.001f, 5f, 1000f, P), 1e-4f);
        }

        [Test]
        public void StrategicRetrenchmentGain_OnlyHelpsWhenOverextended()
        {
            // 過伸張していない（比1.0以下）＝撤退しても旨味なし（負担を減らすだけの恥）
            Assert.AreEqual(0f, OverextensionRules.StrategicRetrenchmentGain(0.9f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, OverextensionRules.StrategicRetrenchmentGain(1f, 1f, P), 1e-5f);
            // 比1.5・収縮幅1.0＝0.2×1×0.5 = 0.1 の負担減
            Assert.AreEqual(0.1f, OverextensionRules.StrategicRetrenchmentGain(1.5f, 1f, P), 1e-5f);
            // 過伸張が深いほど撤退の見返りが大きい＝賢明な撤退
            Assert.Greater(OverextensionRules.StrategicRetrenchmentGain(2.5f, 1f, P),
                           OverextensionRules.StrategicRetrenchmentGain(1.5f, 1f, P));
            // 収縮幅0＝撤退しなければ得るものなし／幅クランプ
            Assert.AreEqual(0f, OverextensionRules.StrategicRetrenchmentGain(1.5f, 0f, P), 1e-5f);
            Assert.AreEqual(OverextensionRules.StrategicRetrenchmentGain(1.5f, 1f, P),
                            OverextensionRules.StrategicRetrenchmentGain(1.5f, 2f, P), 1e-5f);
        }

        [Test]
        public void DefensibilityPerSector_ThinsAsFrontWidens()
        {
            // 国力100を1正面で守る＝100
            Assert.AreEqual(100f, OverextensionRules.DefensibilityPerSector(100f, 1f), 1e-4f);
            // 同じ国力でも4正面＝各所25＝戦線が伸びるほど薄い
            Assert.AreEqual(25f, OverextensionRules.DefensibilityPerSector(100f, 4f), 1e-4f);
            Assert.Less(OverextensionRules.DefensibilityPerSector(100f, 8f),
                        OverextensionRules.DefensibilityPerSector(100f, 2f));
            // 正面0以下／負国力はクランプ
            Assert.AreEqual(0f, OverextensionRules.DefensibilityPerSector(100f, 0f), 1e-4f);
            Assert.AreEqual(0f, OverextensionRules.DefensibilityPerSector(-100f, 4f), 1e-4f);
        }

        [Test]
        public void Params_CtorClamps()
        {
            var p = new OverextensionParams(-1f, -2f, -0.5f, 0.5f, 2f, -1f, -1f);
            Assert.AreEqual(0f, p.territoryWeight, 1e-5f);          // 非負
            Assert.AreEqual(0f, p.frontierWeight, 1e-5f);           // 非負
            Assert.AreEqual(0f, p.treatyWeight, 1e-5f);             // 非負
            Assert.AreEqual(1f, p.overstretchExponent, 1e-5f);      // 1以上
            Assert.AreEqual(1f, p.maxOverstretchPenalty, 1e-5f);    // 0..1
            Assert.AreEqual(0f, p.imperialDecayRate, 1e-5f);        // 非負
            Assert.AreEqual(0f, p.retrenchmentRate, 1e-5f);         // 非負
        }

        [Test]
        public void LongRunStory_OverstretchCuredOnlyByRetrenchment()
        {
            // 過伸張した帝国を「拡張維持」と「戦略的収縮」で20年回す（決定論シミュレート）
            float stayPower = 100f;   // 拡張を維持＝負担はそのまま
            float retreatPower = 100f; // 撤退＝負担を下げて国力を回復させる
            float stayBurden = 150f;
            float retreatBurden = 150f;
            for (int year = 0; year < 20; year++)
            {
                // 維持：比が国力を蝕み続ける
                float stayRatio = OverextensionRules.BurdenToCapacityRatio(stayBurden, stayPower);
                stayPower = OverextensionRules.ImperialDecayTick(stayPower, stayRatio, 1f, P);

                // 収縮：撤退で負担を下げ、過伸張が和らぐ
                float retreatRatio = OverextensionRules.BurdenToCapacityRatio(retreatBurden, retreatPower);
                retreatBurden -= OverextensionRules.StrategicRetrenchmentGain(retreatRatio, 1f, P);
                retreatPower = OverextensionRules.ImperialDecayTick(retreatPower, retreatRatio, 1f, P);
            }
            // 撤退した帝国の方が負担が軽く、過伸張も浅い＝賢明な撤退
            Assert.Less(retreatBurden, stayBurden);
            float stayFinal = OverextensionRules.BurdenToCapacityRatio(stayBurden, stayPower);
            float retreatFinal = OverextensionRules.BurdenToCapacityRatio(retreatBurden, retreatPower);
            Assert.Less(retreatFinal, stayFinal);          // 過伸張は撤退でしか治らない
            Assert.Greater(retreatPower, stayPower);       // 拡張維持は国力を蝕み続ける
        }
    }
}
