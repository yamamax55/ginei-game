using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 対反乱（COIN作戦＝住民隔離・諜報・支持遮断）：住民管理・諜報浸透・細胞摘発・支持遮断・
    /// 住民疎外（逆効果）・正統性回復・COIN総合効果（-1..1）・封じ込め判定。既定Params期待値固定。
    /// </summary>
    public class CounterInsurgencyRulesTests
    {
        [Test]
        public void PopulationControl_CheckpointPlusRegistrationOverTerrain()
        {
            // check=0.8*0.6=0.48 / openness=1-0.2=0.8 / reg=0.6*0.8*0.4=0.192 / sum=0.672
            Assert.AreEqual(0.672f, CounterInsurgencyRules.PopulationControl(0.8f, 0.6f, 0.2f), 1e-4f);
            // 険しい地形（terrain=1）は登録の効きを殺し検問分のみ＝0.6*0.6=0.36
            Assert.AreEqual(0.36f, CounterInsurgencyRules.PopulationControl(0.6f, 1f, 1f), 1e-4f);
        }

        [Test]
        public void IntelPenetration_NeedsControlAndInformants()
        {
            // 0.672*0.5*0.9=0.3024
            Assert.AreEqual(0.3024f, CounterInsurgencyRules.IntelPenetration(0.672f, 0.5f), 1e-4f);
            // 密告網ゼロなら浸透しない
            Assert.AreEqual(0f, CounterInsurgencyRules.IntelPenetration(1f, 0f), 1e-4f);
        }

        [Test]
        public void CellDisruption_IntelTimesRaid()
        {
            // 0.3024*0.8*0.9=0.217728
            Assert.AreEqual(0.217728f, CounterInsurgencyRules.CellDisruption(0.3024f, 0.8f), 1e-4f);
            // 急襲能力ゼロなら摘発できない
            Assert.AreEqual(0f, CounterInsurgencyRules.CellDisruption(0.9f, 0f), 1e-4f);
        }

        [Test]
        public void SupportSeverance_ControlTimesIntel()
        {
            // 0.672*0.3024*0.8=0.16257024
            Assert.AreEqual(0.16257024f, CounterInsurgencyRules.SupportSeverance(0.672f, 0.3024f), 1e-4f);
        }

        [Test]
        public void OverreachAlienation_ForceTimesHarm()
        {
            // 0.8*0.5*0.7=0.28（過剰武力の逆効果）
            Assert.AreEqual(0.28f, CounterInsurgencyRules.OverreachAlienation(0.8f, 0.5f), 1e-4f);
            // 住民被害ゼロなら疎外なし
            Assert.AreEqual(0f, CounterInsurgencyRules.OverreachAlienation(1f, 0f), 1e-4f);
        }

        [Test]
        public void LegitimacyRecovery_GeometricMeanOfThree()
        {
            // Pow(0.5*0.5*0.5,1/3)=0.5 / *0.8=0.4
            Assert.AreEqual(0.4f, CounterInsurgencyRules.LegitimacyRecovery(0.5f, 0.5f, 0.5f), 1e-3f);
            // どれか1つでも欠けると幾何平均が崩れて回復ゼロ
            Assert.AreEqual(0f, CounterInsurgencyRules.LegitimacyRecovery(1f, 1f, 0f), 1e-3f);
        }

        [Test]
        public void CoinEffectiveness_GainMinusAlienationSigned()
        {
            // gain=(0.6+0.4)/2=0.5 / penalty=0.2 / =0.3
            Assert.AreEqual(0.3f, CounterInsurgencyRules.CoinEffectiveness(0.6f, 0.4f, 0.2f), 1e-4f);
            // 疎外が成果を上回ると負（逆効果）：gain=(0.2+0.2)/2=0.2 / penalty=0.9 / =-0.7
            Assert.AreEqual(-0.7f, CounterInsurgencyRules.CoinEffectiveness(0.2f, 0.2f, 0.9f), 1e-4f);
        }

        [Test]
        public void IsInsurgencyContained_ThresholdDefault()
        {
            Assert.IsTrue(CounterInsurgencyRules.IsInsurgencyContained(0.5f));   // 既定閾値0.5＝境界で封じ込め
            Assert.IsFalse(CounterInsurgencyRules.IsInsurgencyContained(0.49f));
            Assert.IsFalse(CounterInsurgencyRules.IsInsurgencyContained(-0.7f)); // 逆効果は封じ込め失敗
        }

        [Test]
        public void Narrative_DisciplinedClearHoldVersusHeavyHanded()
        {
            // 規律ある清郷：高い検問・登録・密告網、開けた地形、巻き添えを抑え、守って開発する
            float control = CounterInsurgencyRules.PopulationControl(0.9f, 0.8f, 0.1f);
            float intel = CounterInsurgencyRules.IntelPenetration(control, 0.8f);
            float disrupt = CounterInsurgencyRules.CellDisruption(intel, 0.9f);
            float severance = CounterInsurgencyRules.SupportSeverance(control, intel);
            float alien = CounterInsurgencyRules.OverreachAlienation(0.4f, 0.1f); // 抑制された武力
            float legit = CounterInsurgencyRules.LegitimacyRecovery(0.8f, 0.7f, severance);
            float coin = CounterInsurgencyRules.CoinEffectiveness(disrupt, legit, alien);

            // 同条件で武力を強め巻き添えを増やすと、住民疎外が成果を食い効果が下がる
            float alienHeavy = CounterInsurgencyRules.OverreachAlienation(1f, 1f);
            float coinHeavy = CounterInsurgencyRules.CoinEffectiveness(disrupt, legit, alienHeavy);

            Assert.Greater(coin, coinHeavy);            // 規律＞強硬
            Assert.Greater(coin, 0f);                   // 規律あるCOINは正味プラス
            Assert.Less(coinHeavy, coin - 0.3f);        // 過剰武力は効果を大きく削る
        }
    }
}
