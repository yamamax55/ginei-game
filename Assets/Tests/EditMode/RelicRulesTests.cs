using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 遺失技術を固定する：発掘は遺跡の豊かさ×考古の努力（掘らねば出ない・roll で決定論判定）・
    /// 遺物の技術価値は格で指数的に跳ねる（高位は現代技術の数世代先＝1超）・解析は科学水準が閾値以下なら
    /// 一切進まない（宝の持ち腐れ＝理解できない遺物は飾り）・独占の優位は理解した分だけ・機密は諜報ゼロでも
    /// 必ず漏れる（<see cref="InnovationDiffusionRules"/> と同型＝独占は時限）・公開は名声を返し・
    /// 未理解の高位遺物は暴走リスク（分不相応な力は持ち主を焼く）。クランプを担保。
    /// <see cref="ResearchRules"/>（自前研究）・<see cref="DisclosureRules"/>（物語の開示）とは別系統の発掘の利得。
    /// </summary>
    public class RelicRulesTests
    {
        private static readonly RelicParams P = RelicParams.Default; // 発掘上限0.6/基準価値0.2/格倍率1.8/格上限5/解析速度0.1/必要科学0.3/自然漏出0.01/諜報重み0.05/名声スケール0.5/暴走上限0.5

        [Test]
        public void ExcavationChance_RichnessTimesEffort_DeterministicRoll()
        {
            // 最豊×全力でも上限0.6＝一点物は簡単には出ない
            Assert.AreEqual(0.6f, RelicRules.ExcavationChance(1f, 1f, P), 1e-4f);
            // 中間：0.6×0.5×0.5 = 0.15
            Assert.AreEqual(0.15f, RelicRules.ExcavationChance(0.5f, 0.5f, P), 1e-4f);
            // 掘らなければ出ない・不毛の地からも出ない
            Assert.AreEqual(0f, RelicRules.ExcavationChance(1f, 0f, P), 1e-5f);
            Assert.AreEqual(0f, RelicRules.ExcavationChance(0f, 1f, P), 1e-5f);
            // roll で決定論判定（roll < chance で発掘・境界は不発）
            Assert.IsTrue(RelicRules.Excavates(0.6f, 0.59f));
            Assert.IsFalse(RelicRules.Excavates(0.6f, 0.6f));
        }

        [Test]
        public void RelicTechValue_JumpsWithTier_GenerationsAhead()
        {
            // 格1＝基準値0.2、格で指数的に跳ねる：格2=0.36・格3=0.648
            Assert.AreEqual(0.2f, RelicRules.RelicTechValue(1, P), 1e-4f);
            Assert.AreEqual(0.36f, RelicRules.RelicTechValue(2, P), 1e-4f);
            Assert.AreEqual(0.648f, RelicRules.RelicTechValue(3, P), 1e-4f);
            // 最高位（格5）＝0.2×1.8^4 = 2.09952 ＝1超＝自前研究では届かない数世代先
            Assert.AreEqual(2.09952f, RelicRules.RelicTechValue(5, P), 1e-3f);
            Assert.Greater(RelicRules.RelicTechValue(5, P), 1f);
            // 格上限でクランプ・格0以下はガラクタ
            Assert.AreEqual(RelicRules.RelicTechValue(5, P), RelicRules.RelicTechValue(99, P), 1e-5f);
            Assert.AreEqual(0f, RelicRules.RelicTechValue(0, P), 1e-5f);
        }

        [Test]
        public void ReverseEngineeringTick_LowScienceMakesRelicOrnament()
        {
            // 科学水準が閾値（0.3）以下では何年かけても理解が進まない＝宝の持ち腐れ
            Assert.AreEqual(0.2f, RelicRules.ReverseEngineeringTick(0.2f, 0.3f, 100f, P), 1e-5f);
            Assert.AreEqual(0.2f, RelicRules.ReverseEngineeringTick(0.2f, 0f, 100f, P), 1e-5f);
            // 最高科学（1.0）：係数1で 0.2 + 0.1×1×1 = 0.3
            Assert.AreEqual(0.3f, RelicRules.ReverseEngineeringTick(0.2f, 1f, 1f, P), 1e-4f);
            // 中間科学0.65：係数(0.65-0.3)/0.7=0.5 で 0.2 + 0.05 = 0.25 ＝読める文明にだけ価値を返す
            Assert.AreEqual(0.25f, RelicRules.ReverseEngineeringTick(0.2f, 0.65f, 1f, P), 1e-4f);
            // 上限1でクランプ・dt 負は据え置き
            Assert.AreEqual(1f, RelicRules.ReverseEngineeringTick(0.99f, 1f, 10f, P), 1e-5f);
            Assert.AreEqual(0.2f, RelicRules.ReverseEngineeringTick(0.2f, 1f, -1f, P), 1e-5f);
        }

        [Test]
        public void MonopolyAdvantage_OnlyAsMuchAsUnderstood()
        {
            float tier5 = RelicRules.RelicTechValue(5, P); // 2.09952
            // 未解析の独占は優位ゼロ（漏出リスクだけ負う）
            Assert.AreEqual(0f, RelicRules.MonopolyAdvantage(tier5, 0f), 1e-5f);
            // 半分理解＝半分の優位：2.09952×0.5 = 1.04976
            Assert.AreEqual(1.04976f, RelicRules.MonopolyAdvantage(tier5, 0.5f), 1e-3f);
            // 完全理解で全価値・負の価値や過大な理解はクランプ
            Assert.AreEqual(tier5, RelicRules.MonopolyAdvantage(tier5, 1f), 1e-5f);
            Assert.AreEqual(0f, RelicRules.MonopolyAdvantage(-1f, 1f), 1e-5f);
            Assert.AreEqual(tier5, RelicRules.MonopolyAdvantage(tier5, 2f), 1e-5f);
        }

        [Test]
        public void SecrecyErosionTick_LeaksEvenWithoutSpies_MonopolyIsTemporary()
        {
            // 諜報ゼロでも自然漏出：1.0 − 0.01×10 = 0.9 ＝独占は時限（InnovationDiffusion と同型の運命）
            Assert.AreEqual(0.9f, RelicRules.SecrecyErosionTick(1f, 0f, 10f, P), 1e-4f);
            // 諜報全力で加速：1.0 − (0.01+0.05)×10 = 0.4
            Assert.AreEqual(0.4f, RelicRules.SecrecyErosionTick(1f, 1f, 10f, P), 1e-4f);
            // 下限0でクランプ（マイナスに沈まない）
            Assert.AreEqual(0f, RelicRules.SecrecyErosionTick(0.1f, 1f, 100f, P), 1e-5f);
        }

        [Test]
        public void PublicationPrestige_ContributionToHumanity()
        {
            // 格3の公開：0.648×0.5 = 0.324 ＝正統性・外交ボーナスの原資
            Assert.AreEqual(0.324f, RelicRules.PublicationPrestige(RelicRules.RelicTechValue(3, P), P), 1e-4f);
            // 最高位の公開は名声上限1でクランプ（2.09952×0.5 = 1.04976 → 1）
            Assert.AreEqual(1f, RelicRules.PublicationPrestige(RelicRules.RelicTechValue(5, P), P), 1e-5f);
            // 価値ゼロ・負の価値からは名声ゼロ
            Assert.AreEqual(0f, RelicRules.PublicationPrestige(0f, P), 1e-5f);
            Assert.AreEqual(0f, RelicRules.PublicationPrestige(-1f, P), 1e-5f);
        }

        [Test]
        public void CurseRisk_MisunderstoodHighTierBurnsItsHolder()
        {
            // 最高位×無理解＝最悪0.5 ＝分不相応な力は持ち主を焼く
            Assert.AreEqual(0.5f, RelicRules.CurseRisk(5, 0f, P), 1e-4f);
            // 完全理解なら最高位でもゼロ＝遺物は鏡（律せる文明には災いにならない）
            Assert.AreEqual(0f, RelicRules.CurseRisk(5, 1f, P), 1e-5f);
            // 低位は軽い：格1×無理解＝0.5×0.2 = 0.1
            Assert.AreEqual(0.1f, RelicRules.CurseRisk(1, 0f, P), 1e-4f);
            // 中間：格3×半理解＝0.5×0.6×0.5 = 0.15
            Assert.AreEqual(0.15f, RelicRules.CurseRisk(3, 0.5f, P), 1e-4f);
            // 格0以下はゼロ・過大な格はクランプ
            Assert.AreEqual(0f, RelicRules.CurseRisk(0, 0f, P), 1e-5f);
            Assert.AreEqual(0.5f, RelicRules.CurseRisk(99, 0f, P), 1e-4f);
        }
    }
}
