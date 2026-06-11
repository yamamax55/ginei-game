using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 功臣処遇ジレンマ（#1422・項羽と劉邦）のテスト。功臣の脅威・厚遇の忠誠・転封の不満・
    /// 粛清の安定化・処遇の帰結・他功臣の恐怖・信の喪失・最適処遇・粛清の逆効果判定を担保する。
    /// 既定 Params（脅威1.0/厚遇忠誠0.5/独立リスク0.6/転封不満0.5/粛清恐怖0.7/逆効果閾値0.6）。
    /// </summary>
    public class MeritRetentionRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>功臣の脅威＝軍功×地方基盤。両方そろう功臣が最も危険。</summary>
        [Test]
        public void VassalThreat_軍功と地方基盤の積()
        {
            Assert.AreEqual(0.4f, MeritRetentionRules.VassalThreat(0.8f, 0.5f), Eps);
            // 軍功が高くても地方基盤ゼロなら脅威なし。
            Assert.AreEqual(0f, MeritRetentionRules.VassalThreat(1f, 0f), Eps);
        }

        /// <summary>厚遇は当面の忠誠を買うが、独立勢力化リスクが残る。転封・粛清では忠誠を買えない。</summary>
        [Test]
        public void RewardLoyalty_厚遇のみ忠誠と独立リスク()
        {
            float loyalty = MeritRetentionRules.RewardLoyalty(MeritDisposition.厚遇, 0.8f, out float risk);
            Assert.AreEqual(0.4f, loyalty, Eps);
            Assert.AreEqual(0.48f, risk, Eps); // 恩を売っても力は削れない＝独立化の火種が残る

            float none = MeritRetentionRules.RewardLoyalty(MeritDisposition.転封, 0.8f, out float r2);
            Assert.AreEqual(0f, none, Eps);
            Assert.AreEqual(0f, r2, Eps);
        }

        /// <summary>転封で力を削ぐと不満が募る（鳥尽きて弓蔵る）。厚遇では不満ゼロ。</summary>
        [Test]
        public void ReassignmentDiscontent_転封のみ不満()
        {
            Assert.AreEqual(0.3f, MeritRetentionRules.ReassignmentDiscontent(MeritDisposition.転封, 0.6f), Eps);
            Assert.AreEqual(0f, MeritRetentionRules.ReassignmentDiscontent(MeritDisposition.厚遇, 0.6f), Eps);
        }

        /// <summary>粛清は脅威を消すが、同規模の恐怖が差し引かれる＝純安定は目減りする。</summary>
        [Test]
        public void PurgeStabilization_脅威除去から恐怖を引く()
        {
            // 0.8 − 0.8×0.7 = 0.24
            Assert.AreEqual(0.24f, MeritRetentionRules.PurgeStabilization(MeritDisposition.粛清, 0.8f), Eps);
            Assert.AreEqual(0f, MeritRetentionRules.PurgeStabilization(MeritDisposition.厚遇, 0.8f), Eps);
        }

        /// <summary>処遇の帰結＝厚遇は脅威を温存し帰結を割り引く・粛清は恐怖で長期安定を蝕む。</summary>
        [Test]
        public void DispositionOutcome_処遇ごとの安定化帰結()
        {
            // 厚遇：0.4 − 0.4×0.6 = 0.16
            Assert.AreEqual(0.16f, MeritRetentionRules.DispositionOutcome(MeritDisposition.厚遇, 0.4f, 0.5f), Eps);
            // 粛清：fear=0.8×0.7=0.56, absorb=0.75 → 0.8 − 0.56×1.25 = 0.1
            Assert.AreEqual(0.1f, MeritRetentionRules.DispositionOutcome(MeritDisposition.粛清, 0.8f, 0.5f), Eps);
        }

        /// <summary>他功臣の恐怖＝粛清の苛烈さ×功臣間の連帯。連帯が高いほど恐れを共有し離反へ傾く。</summary>
        [Test]
        public void OtherVassalsFear_苛烈さと連帯の積()
        {
            // 0.8×0.9×1.7 = 1.224 → クランプ1.0（韓信の粛清が彭越・英布へ波及）
            Assert.AreEqual(1f, MeritRetentionRules.OtherVassalsFear(0.8f, 0.9f), Eps);
            // 0.5×0.5×1.7 = 0.425
            Assert.AreEqual(0.425f, MeritRetentionRules.OtherVassalsFear(0.5f, 0.5f), Eps);
            // 連帯ゼロの孤立した功臣は恐れを連鎖させない。
            Assert.AreEqual(0f, MeritRetentionRules.OtherVassalsFear(1f, 0f), Eps);
        }

        /// <summary>忠実な功臣まで粛清すると信を失う（信なくば立たず）＝非線形に膨らむ。</summary>
        [Test]
        public void TrustErosionFromBetrayal_忠臣の粛清で加速()
        {
            // 0.5 + 0.25 = 0.75
            Assert.AreEqual(0.75f, MeritRetentionRules.TrustErosionFromBetrayal(0.5f), Eps);
            // 0 → 0、完全な裏切り 1 → クランプ1.0
            Assert.AreEqual(0f, MeritRetentionRules.TrustErosionFromBetrayal(0f), Eps);
            Assert.AreEqual(1f, MeritRetentionRules.TrustErosionFromBetrayal(1f), Eps);
        }

        /// <summary>最適処遇＝忠誠/低脅威は厚遇、極脅威×強政権×低忠誠のみ粛清、間は転封。</summary>
        [Test]
        public void OptimalDisposition_脅威と政権と忠誠で推奨()
        {
            // 脅威が低い → 厚遇
            Assert.AreEqual(MeritDisposition.厚遇, MeritRetentionRules.OptimalDisposition(0.3f, 0.5f, 0.5f));
            // 忠誠が高い → 脅威があっても厚遇（信のある功臣は活かす）
            Assert.AreEqual(MeritDisposition.厚遇, MeritRetentionRules.OptimalDisposition(0.9f, 0.5f, 0.7f));
            // 極脅威×強政権×低忠誠 → 粛清
            Assert.AreEqual(MeritDisposition.粛清, MeritRetentionRules.OptimalDisposition(0.8f, 0.8f, 0.2f));
            // 脅威はあるが政権が万全でない → 転封で穏便に
            Assert.AreEqual(MeritDisposition.転封, MeritRetentionRules.OptimalDisposition(0.6f, 0.5f, 0.4f));
        }

        /// <summary>粛清の逆効果判定＝残る功臣の恐怖が閾値を超えたら先制反乱（彭越・英布）。</summary>
        [Test]
        public void IsPurgeBacklash_恐怖が閾値超えで反乱()
        {
            Assert.IsTrue(MeritRetentionRules.IsPurgeBacklash(1f));      // 恐怖最大 → 反乱
            Assert.IsFalse(MeritRetentionRules.IsPurgeBacklash(0.425f)); // 閾値0.6未満 → 反乱せず
        }
    }
}
