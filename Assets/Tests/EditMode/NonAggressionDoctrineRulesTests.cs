using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 非攻ドクトリン純ロジック（MOZI-2 #1560）のテスト。既定Params具体値で期待値を固定。
    /// 外交信用・信用の蓄積・攻撃放棄・防衛の正統性・同盟誘引・違反の信用崩壊・評判抑止を担保。
    /// </summary>
    public class NonAggressionDoctrineRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>外交信用＝宣言×0.4＋実績×0.6。守ってきた国(実績)ほど信じられる。</summary>
        [Test]
        public void DiplomaticCredibility_宣言と実績の加重和()
        {
            // 0.5*0.4 + 1.0*0.6 = 0.2 + 0.6 = 0.8
            Assert.AreEqual(0.8f, NonAggressionDoctrineRules.DiplomaticCredibility(0.5f, 1f), Eps);
            // 実績の方が重い＝同値なら実績側が信用を多く生む
            float onlyCommit = NonAggressionDoctrineRules.DiplomaticCredibility(1f, 0f); // 0.4
            float onlyRecord = NonAggressionDoctrineRules.DiplomaticCredibility(0f, 1f); // 0.6
            Assert.Greater(onlyRecord, onlyCommit);
        }

        /// <summary>外交資本は時間で積み上がる（信用×0.1×dt を上積み）。</summary>
        [Test]
        public void TrustCapital_時間で蓄積する()
        {
            // 0.2 + 0.8*0.1*5 = 0.2 + 0.4 = 0.6
            Assert.AreEqual(0.6f, NonAggressionDoctrineRules.TrustCapital(0.2f, 0.8f, 5f), Eps);
            // dt<=0 は据え置き
            Assert.AreEqual(0.2f, NonAggressionDoctrineRules.TrustCapital(0.2f, 0.8f, 0f), Eps);
            // 上限1.0でクランプ
            Assert.AreEqual(1f, NonAggressionDoctrineRules.TrustCapital(0.9f, 1f, 100f), Eps);
        }

        /// <summary>先制攻撃の放棄＝宣言度そのもの＝信用と引き換えに攻める手を捨てる機会費用。</summary>
        [Test]
        public void OffenseForfeit_宣言度ぶん先制を放棄する()
        {
            Assert.AreEqual(0.7f, NonAggressionDoctrineRules.OffenseForfeit(0.7f), Eps);
            Assert.AreEqual(1f, NonAggressionDoctrineRules.OffenseForfeit(1.5f), Eps); // クランプ
        }

        /// <summary>防衛戦の正統性は平時0・被攻撃時は基礎1.0に非攻ぶん上積み＝大義名分が立つ。</summary>
        [Test]
        public void DefensiveLegitimacy_被攻撃時のみ高まる()
        {
            // 平時は先に手を出さない＝0
            Assert.AreEqual(0f, NonAggressionDoctrineRules.DefensiveLegitimacy(1f, false), Eps);
            // 攻められたら 1+commitment*0.3 を頭打ち＝常に1.0
            Assert.AreEqual(1f, NonAggressionDoctrineRules.DefensiveLegitimacy(1f, true), Eps);
            Assert.AreEqual(1f, NonAggressionDoctrineRules.DefensiveLegitimacy(0f, true), Eps);
        }

        /// <summary>同盟誘引＝外交資本×0.8＝守ってくれる隣人ほど組みたい。</summary>
        [Test]
        public void AllianceAttraction_信用ある非攻国は同盟を引き寄せる()
        {
            // 0.5*0.8 = 0.4
            Assert.AreEqual(0.4f, NonAggressionDoctrineRules.AllianceAttraction(0.5f), Eps);
            // 信用が高いほど誘引も高い（単調）
            Assert.Greater(NonAggressionDoctrineRules.AllianceAttraction(0.9f),
                           NonAggressionDoctrineRules.AllianceAttraction(0.3f));
        }

        /// <summary>非攻を破ると信用が一気に崩壊する（違反崩壊2.0で僅かな攻撃でも吹き飛ぶ）＝非対称。</summary>
        [Test]
        public void CommitmentBreach_違反は信用を一瞬で崩す()
        {
            // 0.8 - 0.3*2.0 = 0.8 - 0.6 = 0.2
            Assert.AreEqual(0.2f, NonAggressionDoctrineRules.CommitmentBreach(0.8f, 0.3f), Eps);
            // 半分(0.5)の侵略で 0.8 が 0.8-1.0=負→0 に崩壊＝積年の資本が一撃で消える
            Assert.AreEqual(0f, NonAggressionDoctrineRules.CommitmentBreach(0.8f, 0.5f), Eps);
        }

        /// <summary>蓄積より崩壊が遥かに速い非対称＝積み上げた資本が一度の違反で吹き飛ぶ。</summary>
        [Test]
        public void TrustCapital_蓄積と違反崩壊は非対称()
        {
            // 信用1.0で1tick積んでも +0.1 しか増えない
            float after10 = 0f;
            for (int i = 0; i < 10; i++) after10 = NonAggressionDoctrineRules.TrustCapital(after10, 1f, 1f);
            Assert.AreEqual(1f, after10, Eps); // 10tickでようやく満杯
            // それを侵略0.5で一撃→ほぼゼロ（1.0 - 0.5*2.0 = 0）
            Assert.AreEqual(0f, NonAggressionDoctrineRules.CommitmentBreach(after10, 0.5f), Eps);
        }

        /// <summary>評判抑止＝外交資本×0.6＋防衛力×0.4＝攻めない国だが守りは固い。</summary>
        [Test]
        public void DeterrenceViaReputation_信用と防衛力で攻撃の旨味を削る()
        {
            // 1.0*0.6 + 1.0*0.4 = 1.0
            Assert.AreEqual(1f, NonAggressionDoctrineRules.DeterrenceViaReputation(1f, 1f), Eps);
            // 信用0.5 防衛0.5 = 0.3+0.2 = 0.5
            Assert.AreEqual(0.5f, NonAggressionDoctrineRules.DeterrenceViaReputation(0.5f, 0.5f), Eps);
            // 信用の重みが大きい＝評判が抑止の主柱
            Assert.Greater(NonAggressionDoctrineRules.DeterrenceViaReputation(1f, 0f),
                           NonAggressionDoctrineRules.DeterrenceViaReputation(0f, 1f));
        }

        /// <summary>信頼できる非攻国の判定＝外交信用が閾値以上。</summary>
        [Test]
        public void IsCrediblyPacifist_閾値判定()
        {
            Assert.IsTrue(NonAggressionDoctrineRules.IsCrediblyPacifist(0.8f, 0.7f));
            Assert.IsFalse(NonAggressionDoctrineRules.IsCrediblyPacifist(0.6f, 0.7f));
            Assert.IsTrue(NonAggressionDoctrineRules.IsCrediblyPacifist(0.7f, 0.7f)); // 境界含む
        }
    }
}
