using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 後の先ドクトリン（GRN-2 #1379・五輪書/武道）の純ロジックテスト。
    /// 先の先・対の先・後の先の三つの機＝主導権の取り方・反撃の窓・後の先のボーナス・
    /// 先の先のリスク・対の先の同期・反攻型AI補正・誘いの効果・後の先の構え判定を既定Paramsで担保。
    /// </summary>
    public class TimingDoctrineRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>主導権の取り方＝先の先は先制（即応度比例）・後の先は待ち（あえて譲る）。</summary>
        [Test]
        public void InitiativeValue_先制と待ちで主導権が分かれる()
        {
            // 先の先＝即応1.0で完全主導(0.6+0.4)、即応0で基礎0.6。
            Assert.AreEqual(1.0f, TimingDoctrineRules.InitiativeValue(TimingDoctrine.先の先, 1.0f), Eps);
            Assert.AreEqual(0.6f, TimingDoctrineRules.InitiativeValue(TimingDoctrine.先の先, 0.0f), Eps);
            // 対の先＝中庸0.5（敵と同時＝半分譲る）。
            Assert.AreEqual(0.5f, TimingDoctrineRules.InitiativeValue(TimingDoctrine.対の先, 0.7f), Eps);
            // 後の先＝待ち（低・主導権をあえて譲る）。即応1.0でも0.3。
            Assert.AreEqual(0.3f, TimingDoctrineRules.InitiativeValue(TimingDoctrine.後の先, 1.0f), Eps);
            // 先の先＞後の先＝先制が主導を握る。
            Assert.Greater(
                TimingDoctrineRules.InitiativeValue(TimingDoctrine.先の先, 1.0f),
                TimingDoctrineRules.InitiativeValue(TimingDoctrine.後の先, 1.0f));
        }

        /// <summary>反撃の窓＝敵が攻めに出てこそ開く（敵に先に動かせる必要がある）。</summary>
        [Test]
        public void CounterWindow_敵が動いてこそ開く()
        {
            // 敵が深く攻め(1.0)＋受けの構え(1.0)＝窓が全開(1.0)。
            Assert.AreEqual(1.0f, TimingDoctrineRules.CounterWindow(1.0f, 1.0f), Eps);
            // 敵が攻めても受けが崩れている(即応0)＝下限0.4まで。
            Assert.AreEqual(0.4f, TimingDoctrineRules.CounterWindow(1.0f, 0.0f), Eps);
            // 敵が動かなければ(commit=0)＝窓は開かない＝受けて勝つには相手に先に動かせる。
            Assert.AreEqual(0.0f, TimingDoctrineRules.CounterWindow(0.0f, 1.0f), Eps);
        }

        /// <summary>後の先のボーナス＝反撃が決まると効果大（受けの構えが底上げ）。</summary>
        [Test]
        public void GoNoSenBonus_受けて勝つと効果が大きい()
        {
            // 窓全開×構え固い＝1.8倍（敵の勢いを利用）。
            Assert.AreEqual(1.8f, TimingDoctrineRules.GoNoSenBonus(1.0f, 1.0f), Eps);
            // 窓全開だが構え無し＝1.4倍（構えの分だけ伸びしろが減る）。
            Assert.AreEqual(1.4f, TimingDoctrineRules.GoNoSenBonus(1.0f, 0.0f), Eps);
            // 窓が開かなければ＝1.0（反撃の機会なし＝基準のまま）。
            Assert.AreEqual(1.0f, TimingDoctrineRules.GoNoSenBonus(0.0f, 1.0f), Eps);
            // 構えが固いほどボーナスが大きい＝受けて勝つ。
            Assert.Greater(
                TimingDoctrineRules.GoNoSenBonus(1.0f, 1.0f),
                TimingDoctrineRules.GoNoSenBonus(1.0f, 0.0f));
            // 実効値≥1.0（基準非破壊）。
            Assert.GreaterOrEqual(TimingDoctrineRules.GoNoSenBonus(0.5f, 0.5f), 1.0f);
        }

        /// <summary>先の先のリスク＝先制は読まれると隙になる（先の先のみ・敵の備えで増す）。</summary>
        [Test]
        public void SenNoSenRisk_先制は読まれると隙になる()
        {
            // 先の先＝敵が完全に備える(1.0)と読まれてリスク最大1.0。
            Assert.AreEqual(1.0f, TimingDoctrineRules.SenNoSenRisk(TimingDoctrine.先の先, 1.0f), Eps);
            // 敵が無警戒(0)でも先走りの下地リスク0.2。
            Assert.AreEqual(0.2f, TimingDoctrineRules.SenNoSenRisk(TimingDoctrine.先の先, 0.0f), Eps);
            // 後の先・対の先は先制していない＝先走りの隙なし(0)。
            Assert.AreEqual(0.0f, TimingDoctrineRules.SenNoSenRisk(TimingDoctrine.後の先, 1.0f), Eps);
            Assert.AreEqual(0.0f, TimingDoctrineRules.SenNoSenRisk(TimingDoctrine.対の先, 1.0f), Eps);
        }

        /// <summary>対の先の同期＝タイミングが合えば相手を制す（ズレで落ちる）。</summary>
        [Test]
        public void TaiNoSenSynchrony_同時に動けば同期最大()
        {
            // タイミング完全一致＝同期1.0。
            Assert.AreEqual(1.0f, TimingDoctrineRules.TaiNoSenSynchrony(0.5f, 0.5f), Eps);
            // 大きくズレる(差1.0)＝同期0（鋭さ1.5でクランプ）。
            Assert.AreEqual(0.0f, TimingDoctrineRules.TaiNoSenSynchrony(1.0f, 0.0f), Eps);
            // 小さなズレ(差0.2)＝1-0.3=0.7。
            Assert.AreEqual(0.7f, TimingDoctrineRules.TaiNoSenSynchrony(0.6f, 0.4f), Eps);
        }

        /// <summary>反攻型AI補正＝後の先は守って反撃・先の先は先制。</summary>
        [Test]
        public void ReactiveAIBias_後の先は反攻型に傾く()
        {
            // 先の先＝先制型（負）。
            Assert.AreEqual(-0.7f, TimingDoctrineRules.ReactiveAIBias(TimingDoctrine.先の先), Eps);
            // 対の先＝中庸(0)。
            Assert.AreEqual(0.0f, TimingDoctrineRules.ReactiveAIBias(TimingDoctrine.対の先), Eps);
            // 後の先＝反攻型（正＝守って反撃するAI補正）。
            Assert.AreEqual(0.7f, TimingDoctrineRules.ReactiveAIBias(TimingDoctrine.後の先), Eps);
        }

        /// <summary>誘いの効果＝後の先は攻めっ気の敵を釣れる（構え×攻撃性）。</summary>
        [Test]
        public void BaitEffectiveness_攻めっ気の敵を誘える()
        {
            // 誘いの構え固い×敵が攻撃的＝釣りが効く(1.0)。
            Assert.AreEqual(1.0f, TimingDoctrineRules.BaitEffectiveness(1.0f, 1.0f), Eps);
            // 攻めっ気のない敵(0)は誘いに乗らない(0)。
            Assert.AreEqual(0.0f, TimingDoctrineRules.BaitEffectiveness(1.0f, 0.0f), Eps);
            // 中庸×中庸＝0.25。
            Assert.AreEqual(0.25f, TimingDoctrineRules.BaitEffectiveness(0.5f, 0.5f), Eps);
        }

        /// <summary>後の先の構え判定＝後の先で窓が閾値以上に開いている時のみ機能。</summary>
        [Test]
        public void IsCounterPosture_後の先で窓が開いていれば機能()
        {
            // 後の先＋窓0.5が閾値0.4以上＝構え機能(true)。
            Assert.IsTrue(TimingDoctrineRules.IsCounterPosture(TimingDoctrine.後の先, 0.5f, 0.4f));
            // 後の先でも窓0.3が閾値未満＝まだ機能しない(false)。
            Assert.IsFalse(TimingDoctrineRules.IsCounterPosture(TimingDoctrine.後の先, 0.3f, 0.4f));
            // 先の先はカウンターの構えではない＝常にfalse。
            Assert.IsFalse(TimingDoctrineRules.IsCounterPosture(TimingDoctrine.先の先, 1.0f, 0.0f));
            // 対の先もカウンターの構えではない＝常にfalse。
            Assert.IsFalse(TimingDoctrineRules.IsCounterPosture(TimingDoctrine.対の先, 1.0f, 0.0f));
        }
    }
}
