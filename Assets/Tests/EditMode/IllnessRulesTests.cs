using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 病臥（ラインハルト型）を固定する：発症（高齢×激務×虚弱・上限あり）、決定論判定、
    /// 執務能力の低下倍率（下限あり）、隠蔽の侵食（重いほど・側近が多いほど崩れる）、
    /// 漏洩の衝撃（皇帝の不治は国家を揺らす）、継承レース（後継の明確さが鎮める）、
    /// 病床の権威（死にゆく英雄の言葉は重い）。決定論・境界を担保。
    /// </summary>
    public class IllnessRulesTests
    {
        private static readonly IllnessParams P = IllnessParams.Default;
        // 基礎0.01/加齢開始40/加齢率0.005/激務重み1/虚弱重み1/発症上限0.5
        // 執務下限0.1/隠蔽崩れ0.2/側近重み1/レース過熱0.5/鎮静0.5/病床閾値0.5/病床ボーナス0.5

        [Test]
        public void OnsetChance_RisesWithAgeStressFrailty()
        {
            // 若く頑健で閑職＝基礎値のみ
            Assert.AreEqual(0.01f, IllnessRules.OnsetChance(30f, 0f, 1f, P), 1e-5f);
            // 高齢のみ：60歳＝0.01＋0.005×20＝0.11
            Assert.AreEqual(0.11f, IllnessRules.OnsetChance(60f, 0f, 1f, P), 1e-5f);
            // 高齢×激務×虚弱が掛け算で膨らむ：0.11×2×2＝0.44
            Assert.AreEqual(0.44f, IllnessRules.OnsetChance(60f, 1f, 0f, P), 1e-5f);
            // 上限で頭打ち（確実な発症はない）＋入力クランプ（stress>1・constitution<0）
            Assert.AreEqual(0.5f, IllnessRules.OnsetChance(200f, 5f, -1f, P), 1e-5f);
            // 負の年齢はクランプ＝基礎値のみ
            Assert.AreEqual(0.01f, IllnessRules.OnsetChance(-10f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void Strikes_Deterministic()
        {
            Assert.IsTrue(IllnessRules.Strikes(0.44f, 0.43f));   // 確率未満の roll＝倒れる
            Assert.IsFalse(IllnessRules.Strikes(0.44f, 0.44f));  // 境界ちょうどはセーフ
            Assert.IsFalse(IllnessRules.Strikes(0f, 0f));        // 確率0は決して倒れない
            Assert.IsFalse(IllnessRules.Strikes(1f, 1f));        // roll=1 は決して発症しない
        }

        [Test]
        public void CapacityFactor_DropsWithSeverityAndFloors()
        {
            Assert.AreEqual(1f, IllnessRules.CapacityFactor(0f, P), 1e-5f);     // 健康＝満額
            Assert.AreEqual(0.55f, IllnessRules.CapacityFactor(0.5f, P), 1e-5f); // 中等症＝半減弱
            Assert.AreEqual(0.1f, IllnessRules.CapacityFactor(1f, P), 1e-5f);    // 死の床＝下限
            Assert.AreEqual(0.1f, IllnessRules.CapacityFactor(2f, P), 1e-5f);    // 入力クランプ
            Assert.AreEqual(1f, IllnessRules.CapacityFactor(-1f, P), 1e-5f);
        }

        [Test]
        public void ConcealmentTick_HeavierAndCloserLeaksFaster()
        {
            // 重症0.5×側近満員：崩れ＝0.2×0.5×(1+1)＝0.2/単位時間
            Assert.AreEqual(0.8f, IllnessRules.ConcealmentTick(1f, 0.5f, 1f, 1f, P), 1e-5f);
            // 側近が居なければ半分の速さ＝0.1
            Assert.AreEqual(0.9f, IllnessRules.ConcealmentTick(1f, 0.5f, 0f, 1f, P), 1e-5f);
            // 病が無ければ隠すものがない＝崩れない
            Assert.AreEqual(1f, IllnessRules.ConcealmentTick(1f, 0f, 1f, 1f, P), 1e-5f);
            // 長時間で完全崩壊＝0で下げ止まる
            Assert.AreEqual(0f, IllnessRules.ConcealmentTick(1f, 1f, 1f, 100f, P), 1e-5f);
        }

        [Test]
        public void LeakImpact_EmperorsTerminalShakesTheState()
        {
            Assert.AreEqual(1f, IllnessRules.LeakImpact(1f, 1f), 1e-5f);       // 皇帝の不治＝最大の衝撃
            Assert.AreEqual(0.25f, IllnessRules.LeakImpact(0.5f, 0.5f), 1e-5f); // 中堅の中等症
            Assert.AreEqual(0f, IllnessRules.LeakImpact(0f, 1f), 1e-5f);        // 健康なら漏れても無風
            Assert.AreEqual(0f, IllnessRules.LeakImpact(1f, 0f), 1e-5f);        // 一兵卒の病は誰も揺らさない
            Assert.AreEqual(1f, IllnessRules.LeakImpact(2f, 2f), 1e-5f);        // 入力クランプ
        }

        [Test]
        public void SuccessionRaceTick_HeirClarityCalmsTheRace()
        {
            // 死期が見え後継が曖昧＝過熱：0＋0.5×1×(1−0)×1＝0.5
            Assert.AreEqual(0.5f, IllnessRules.SuccessionRaceTick(0f, 1f, 0f, 1f, P), 1e-5f);
            // 後継が明確なら同じ重症度でも鎮まる：0.5＋(0−0.5×1)×1＝0（立太子がレースを鎮める）
            Assert.AreEqual(0f, IllnessRules.SuccessionRaceTick(0.5f, 1f, 1f, 1f, P), 1e-5f);
            // 病が知覚されなければ過熱しない（隠蔽が効いている間は静か）
            Assert.AreEqual(0.3f, IllnessRules.SuccessionRaceTick(0.3f, 0f, 0f, 1f, P), 1e-5f);
            // 上限1で頭打ち
            Assert.AreEqual(1f, IllnessRules.SuccessionRaceTick(0.9f, 1f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void DeathbedAuthority_DyingWordsWeighMore()
        {
            Assert.AreEqual(1f, IllnessRules.DeathbedAuthority(0f, P), 1e-5f);     // 健康＝平時の重み
            Assert.AreEqual(1f, IllnessRules.DeathbedAuthority(0.5f, P), 1e-5f);   // 閾値ちょうど＝まだ平時
            Assert.AreEqual(1.25f, IllnessRules.DeathbedAuthority(0.75f, P), 1e-5f); // 死期が見え始める
            Assert.AreEqual(1.5f, IllnessRules.DeathbedAuthority(1f, P), 1e-5f);   // 死の床＝遺言は最大の効力
            Assert.AreEqual(1.5f, IllnessRules.DeathbedAuthority(2f, P), 1e-5f);   // 入力クランプ
        }
    }
}
