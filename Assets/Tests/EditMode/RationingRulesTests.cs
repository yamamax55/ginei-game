using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 配給制＝統制経済の表側（BlackMarketRules と対）を固定する：一人あたり配給量と生存ライン割れ、
    /// 特権層の例外が公平感を殺す（指導層が同じ列に並ぶ国は耐える）、欠乏下の士気は量より公平
    /// （少なくても公平＞豊富でも不公平）、闇市流出圧（飢えが源泉・不公平が増幅）、
    /// 買いだめパニックの自己成就（信頼が防波堤）。
    /// </summary>
    public class RationingRulesTests
    {
        private static readonly RationingParams P = RationingParams.Default;
        // 不透明天井0.6/公平比重0.7/充足配給1.0/不公平増幅1.0/パニック湧き0.2/自己増殖0.3/鎮静0.5

        [Test]
        public void RationLevel_And_SubsistenceGap()
        {
            // 供給100を人口200で割る＝一人あたり0.5
            Assert.AreEqual(0.5f, RationingRules.RationLevel(100f, 200f), 1e-5f);
            // 人口0以下＝配る相手がいない、供給負はクランプ
            Assert.AreEqual(0f, RationingRules.RationLevel(100f, 0f), 1e-5f);
            Assert.AreEqual(0f, RationingRules.RationLevel(-5f, 100f), 1e-5f);
            // 生存必要量1に対し配給0.5＝半分割れ（飢餓）
            Assert.AreEqual(0.5f, RationingRules.SubsistenceGap(0.5f, 1f), 1e-5f);
            // 足りていれば0・配給ゼロは1（飢餓最大）・必要量0は飢えようがない
            Assert.AreEqual(0f, RationingRules.SubsistenceGap(1.5f, 1f), 1e-5f);
            Assert.AreEqual(1f, RationingRules.SubsistenceGap(0f, 1f), 1e-5f);
            Assert.AreEqual(0f, RationingRules.SubsistenceGap(0f, 0f), 1e-5f);
        }

        [Test]
        public void FairnessPerception_EliteExemptionKillsFairness()
        {
            // 例外無し×完全透明＝公平感満点（指導層が同じ列に並ぶ国は耐える）
            Assert.AreEqual(1f, RationingRules.FairnessPerception(0f, 1f, P), 1e-5f);
            // 特権層の例外は乗算で殺す：例外全開なら透明でも0
            Assert.AreEqual(0f, RationingRules.FairnessPerception(1f, 1f, P), 1e-5f);
            // 例外半分×完全透明＝0.5
            Assert.AreEqual(0.5f, RationingRules.FairnessPerception(0.5f, 1f, P), 1e-5f);
            // 例外無しでも不透明なら天井0.6＝見えない制度は疑われる
            Assert.AreEqual(0.6f, RationingRules.FairnessPerception(0f, 0f, P), 1e-5f);
        }

        [Test]
        public void MoraleUnderScarcity_FairScarcityBeatsUnfairPlenty()
        {
            // 少なくても公平（配給半分×公平1）：0.7×1＋0.3×0.5＝0.85
            float fairScarce = RationingRules.MoraleUnderScarcity(0.5f, 1f, P);
            Assert.AreEqual(0.85f, fairScarce, 1e-5f);
            // 豊富でも不公平（配給満額×公平0.2）：0.7×0.2＋0.3×1＝0.44
            float unfairPlenty = RationingRules.MoraleUnderScarcity(1f, 0.2f, P);
            Assert.AreEqual(0.44f, unfairPlenty, 1e-5f);
            // 飢えは耐えられるが不正は耐えられない＝公平な欠乏が不公平な豊富に勝つ
            Assert.Greater(fairScarce, unfairPlenty);
            // 境界：満額×公平満点＝1、配給ゼロ×公平ゼロ＝0
            Assert.AreEqual(1f, RationingRules.MoraleUnderScarcity(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, RationingRules.MoraleUnderScarcity(0f, 0f, P), 1e-5f);
        }

        [Test]
        public void BlackMarketPressure_HungerIsSourceUnfairnessAmplifies()
        {
            // 飢えが無ければ不公平でも流れない（配る物があるうちは列に並ぶ）
            Assert.AreEqual(0f, RationingRules.BlackMarketPressure(0f, 0f, P), 1e-5f);
            // 公平なら欠乏ぶんだけ：gap0.5×公平1＝0.5
            Assert.AreEqual(0.5f, RationingRules.BlackMarketPressure(0.5f, 1f, P), 1e-5f);
            // 不公平の露見が増幅：gap0.4×公平0.5＝0.4×(1＋0.5)＝0.6
            Assert.AreEqual(0.6f, RationingRules.BlackMarketPressure(0.4f, 0.5f, P), 1e-5f);
            // 公平感0なら倍化＝gap0.5で上限1にクランプ（BlackMarketRules.MarketSizeTick の scarcity 入力になる）
            Assert.AreEqual(1f, RationingRules.BlackMarketPressure(0.5f, 0f, P), 1e-5f);
        }

        [Test]
        public void HoardingPanicTick_ShortageExpectationSelfFulfills()
        {
            // パニック0・予想1×信頼0・dt1：湧き0.2
            Assert.AreEqual(0.2f, RationingRules.HoardingPanicTick(0f, 1f, 0f, 1f, P), 1e-5f);
            // 既存0.5は自己増殖で肥える：0.5＋湧き0.2＋複利0.3×0.5＝0.85（不足の予想が不足を作る）
            Assert.AreEqual(0.85f, RationingRules.HoardingPanicTick(0.5f, 1f, 0f, 1f, P), 1e-4f);
            // 不足の予想が無ければ湧かない（信頼ゼロでも）
            Assert.AreEqual(0f, RationingRules.HoardingPanicTick(0f, 0f, 0f, 1f, P), 1e-5f);
            // 上限1にクランプ
            Assert.AreEqual(1f, RationingRules.HoardingPanicTick(0.9f, 1f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void HoardingPanicTick_TrustIsTheBreakwater()
        {
            // 完全な信頼：湧きも複利も0、鎮静0.5×0.5＝0.25 → 0.25 に減る
            Assert.AreEqual(0.25f, RationingRules.HoardingPanicTick(0.5f, 1f, 1f, 1f, P), 1e-5f);
            // 信頼半分：0.5＋0.2×0.5＋0.3×0.5×0.5−0.5×0.5×0.5＝0.55 ＝防波堤が低いと増勢
            Assert.AreEqual(0.55f, RationingRules.HoardingPanicTick(0.5f, 1f, 0.5f, 1f, P), 1e-4f);
            // 同条件なら信頼が高いほどパニックは小さい
            Assert.Less(RationingRules.HoardingPanicTick(0.5f, 1f, 1f, 1f, P),
                        RationingRules.HoardingPanicTick(0.5f, 1f, 0.5f, 1f, P));
        }

        [Test]
        public void Params_CtorClampsInputs()
        {
            // 負・範囲外はクランプ＝既定どおり安全
            var q = new RationingParams(-1f, 2f, 0f, -1f, -1f, -1f, -1f);
            Assert.AreEqual(0f, q.opaqueFairnessCap, 1e-5f);
            Assert.AreEqual(1f, q.fairnessWeight, 1e-5f);
            Assert.AreEqual(0.01f, q.comfortRation, 1e-5f);
            Assert.AreEqual(0f, q.unfairnessMarketBoost, 1e-5f);
            Assert.AreEqual(0f, q.panicSpawnRate, 1e-5f);
            Assert.AreEqual(0f, q.panicGrowthRate, 1e-5f);
            Assert.AreEqual(0f, q.calmRate, 1e-5f);
        }
    }
}
