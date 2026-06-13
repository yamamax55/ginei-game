using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// CrisisCycleRules（KNDB-1 #1610・ミンスキー型金融循環）の純ロジック検証。
    /// 既定 Params の具体値で期待値を固定し、相判定・相遷移の循環・レバレッジの蓄積と剥落・
    /// 脆弱性・反転トリガー・ミンスキーモーメントを担保する。
    /// </summary>
    public class CrisisCycleRulesTests
    {
        /// <summary>循環位置が4相へ正しく写る（閾値 0.25/0.6/0.8）。</summary>
        [Test]
        public void PhaseOf_循環位置を4相へ写す()
        {
            Assert.AreEqual(MinskyPhase.変位, CrisisCycleRules.PhaseOf(0.1f));
            Assert.AreEqual(MinskyPhase.熱狂, CrisisCycleRules.PhaseOf(0.3f));
            Assert.AreEqual(MinskyPhase.恐慌, CrisisCycleRules.PhaseOf(0.65f));
            Assert.AreEqual(MinskyPhase.収縮, CrisisCycleRules.PhaseOf(0.9f));
            // 境界は次の相に属する
            Assert.AreEqual(MinskyPhase.熱狂, CrisisCycleRules.PhaseOf(0.25f));
        }

        /// <summary>相遷移は変位→熱狂→恐慌→収縮→変位と循環する。</summary>
        [Test]
        public void NextPhase_相が循環する()
        {
            Assert.AreEqual(MinskyPhase.熱狂, CrisisCycleRules.NextPhase(MinskyPhase.変位));
            Assert.AreEqual(MinskyPhase.恐慌, CrisisCycleRules.NextPhase(MinskyPhase.熱狂));
            Assert.AreEqual(MinskyPhase.収縮, CrisisCycleRules.NextPhase(MinskyPhase.恐慌));
            Assert.AreEqual(MinskyPhase.変位, CrisisCycleRules.NextPhase(MinskyPhase.収縮));
        }

        /// <summary>熱狂でレバレッジが積み上がり、恐慌・収縮で剥落する（変位は横ばい）。</summary>
        [Test]
        public void LeverageBuildup_熱狂で蓄積し恐慌収縮で剥落()
        {
            // 熱狂＝+0.3*1
            Assert.AreEqual(0.8f, CrisisCycleRules.LeverageBuildup(MinskyPhase.熱狂, 0.5f, 1f), 1e-4f);
            // 恐慌＝-0.5*1
            Assert.AreEqual(0.3f, CrisisCycleRules.LeverageBuildup(MinskyPhase.恐慌, 0.8f, 1f), 1e-4f);
            // 収縮＝-0.5*1（下限0でクランプ）
            Assert.AreEqual(0f, CrisisCycleRules.LeverageBuildup(MinskyPhase.収縮, 0.3f, 1f), 1e-4f);
            // 変位＝横ばい
            Assert.AreEqual(0.5f, CrisisCycleRules.LeverageBuildup(MinskyPhase.変位, 0.5f, 1f), 1e-4f);
        }

        /// <summary>脆弱性は熱狂のピークで最も高い（同レバレッジでも相で差が出る）。</summary>
        [Test]
        public void FragilityIndex_熱狂で最も脆い()
        {
            // 熱狂＝1.0*0.7*1.0=0.7
            float boom = CrisisCycleRules.FragilityIndex(1f, MinskyPhase.熱狂);
            Assert.AreEqual(0.7f, boom, 1e-4f);
            // 恐慌＝0.7*0.8=0.56
            float panic = CrisisCycleRules.FragilityIndex(1f, MinskyPhase.恐慌);
            Assert.AreEqual(0.56f, panic, 1e-4f);
            // 収縮＝0.7*0.3=0.21
            float contraction = CrisisCycleRules.FragilityIndex(1f, MinskyPhase.収縮);
            Assert.AreEqual(0.21f, contraction, 1e-4f);
            Assert.Greater(boom, panic);
            Assert.Greater(panic, contraction);
        }

        /// <summary>反転は臨界脆弱性（0.7以上）でのみ起き、脆弱性×ショックを roll が下回ると発火する。</summary>
        [Test]
        public void ReversalTrigger_臨界脆弱性でショックが反転を引く()
        {
            // 脆弱性0.7（臨界）×ショック0.8=0.56。roll0.5<0.56で反転
            Assert.IsTrue(CrisisCycleRules.ReversalTrigger(0.7f, 0.8f, 0.5f));
            // roll0.6>0.56で反転せず
            Assert.IsFalse(CrisisCycleRules.ReversalTrigger(0.7f, 0.8f, 0.6f));
            // 脆弱性0.5（臨界未満）は大きなショックでも反転しない＝積み上がりが要る
            Assert.IsFalse(CrisisCycleRules.ReversalTrigger(0.5f, 1f, 0f));
        }

        /// <summary>ミンスキー・モーメント＝脆弱性が反転閾値0.7に達した瞬間。</summary>
        [Test]
        public void IsMinskyMoment_臨界到達で真()
        {
            Assert.IsTrue(CrisisCycleRules.IsMinskyMoment(0.7f));
            Assert.IsTrue(CrisisCycleRules.IsMinskyMoment(0.85f));
            Assert.IsFalse(CrisisCycleRules.IsMinskyMoment(0.69f));
        }

        /// <summary>循環位置は熱狂で速く・収縮で淀んで進み、1.0を越えると巻き戻って循環する。</summary>
        [Test]
        public void PhaseProgressTick_熱狂は速く収縮は淀み一周で巻き戻る()
        {
            // 熱狂（pos0.3）＝0.5*1*1.5*0.2=0.15進む→0.45
            float boom = CrisisCycleRules.PhaseProgressTick(0.3f, 1f, 0.2f);
            Assert.AreEqual(0.45f, boom, 1e-4f);
            // 収縮（pos0.9）＝0.5*1*0.5*0.2=0.05進む→0.95
            float contraction = CrisisCycleRules.PhaseProgressTick(0.9f, 1f, 0.2f);
            Assert.AreEqual(0.95f, contraction, 1e-4f);
            Assert.Greater(boom - 0.3f, contraction - 0.9f); // 熱狂のほうが進みが速い
            // 1.0超で巻き戻る：収縮（pos0.98）＝+0.05→1.03→-1.0=0.03
            float wrap = CrisisCycleRules.PhaseProgressTick(0.98f, 1f, 0.2f);
            Assert.AreEqual(0.03f, wrap, 1e-4f);
        }

        /// <summary>債務返済比率でヘッジ→投機→ポンツィを弁別（境界 0.4/0.8）。</summary>
        [Test]
        public void HedgeSpeculativePonzi_3段階を弁別()
        {
            Assert.AreEqual(0, CrisisCycleRules.HedgeSpeculativePonzi(0.2f));  // ヘッジ
            Assert.AreEqual(1, CrisisCycleRules.HedgeSpeculativePonzi(0.4f));  // 投機（境界）
            Assert.AreEqual(1, CrisisCycleRules.HedgeSpeculativePonzi(0.7f));  // 投機
            Assert.AreEqual(2, CrisisCycleRules.HedgeSpeculativePonzi(0.8f));  // ポンツィ（境界）
            Assert.AreEqual(2, CrisisCycleRules.HedgeSpeculativePonzi(0.95f)); // ポンツィ
        }
    }
}
