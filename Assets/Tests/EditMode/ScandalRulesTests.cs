using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 醜聞を固定する：露見＝基礎×不行跡×（報道∨政敵の嗅ぎ回り）、判定は roll の決定論、
    /// 失脚ダメージは偽善プレミアムで倍打撃（致死性は罪でなく落差）、もみ消しは成功すれば無傷・
    /// 失敗すれば倍返し（罪＋隠蔽罪）、続発で世論が麻痺（醜聞慣れ）、選挙前に撃つのが最も効く（政治兵器化）。
    /// </summary>
    public class ScandalRulesTests
    {
        private static readonly ScandalParams P = ScandalParams.Default;
        // 露見0.8/ダメージ幅0.5/偽善プレミアム1.0/もみ消し0.9/倍返し2.0/醜聞慣れ0.5/選挙前倍率1.0

        [Test]
        public void ExposureChance_PressAndEnemiesSniffIndependently()
        {
            // 報道も政敵もない＝露見しない（もみ消す必要すらない）
            Assert.AreEqual(0f, ScandalRules.ExposureChance(1f, 0f, 0f, P), 1e-5f);
            // 自由報道だけで満額嗅ぎつける：0.8×1×1
            Assert.AreEqual(0.8f, ScandalRules.ExposureChance(1f, 1f, 0f, P), 1e-5f);
            // 中庸：嗅ぎ回り=1−0.5×0.5=0.75 → 0.8×0.5×0.75=0.3
            Assert.AreEqual(0.3f, ScandalRules.ExposureChance(0.5f, 0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void Exposed_DeterministicRoll()
        {
            Assert.IsTrue(ScandalRules.Exposed(0.3f, 0.29f));   // 確率未満＝露見
            Assert.IsFalse(ScandalRules.Exposed(0.3f, 0.3f));   // 確率以上＝もちこたえる
            Assert.IsFalse(ScandalRules.Exposed(0f, 0f));       // 露見0なら絶対ばれない
        }

        [Test]
        public void ReputationDamage_HypocrisyPremiumDoubles()
        {
            // 野人の汚職＝0.5、清廉を売る者の同じ汚職＝倍打撃の1.0
            Assert.AreEqual(0.5f, ScandalRules.ReputationDamage(1f, 0f, P), 1e-5f);
            Assert.AreEqual(1.0f, ScandalRules.ReputationDamage(1f, 1f, P), 1e-5f);
            // 致死性は罪の重さでなく落差：軽い罪の偽善者(0.4×0.5×2=0.4)＞重い罪の野人(0.6×0.5×1=0.3)
            Assert.Greater(ScandalRules.ReputationDamage(0.4f, 1f, P),
                           ScandalRules.ReputationDamage(0.6f, 0f, P));
        }

        [Test]
        public void CoverupGamble_SmallSinsBuyableBigSinsNot()
        {
            Assert.AreEqual(0.9f, ScandalRules.CoverupGamble(0f, 1f, P), 1e-5f);   // 微罪×潤沢＝ほぼ消せる
            Assert.AreEqual(0f, ScandalRules.CoverupGamble(1f, 1f, P), 1e-5f);     // 大罪は隠しきれない
            Assert.AreEqual(0f, ScandalRules.CoverupGamble(0.5f, 0f, P), 1e-5f);   // 無一文では消せない
            Assert.AreEqual(0.45f, ScandalRules.CoverupGamble(0.5f, 1f, P), 1e-5f); // 0.9×1×0.5
        }

        [Test]
        public void CoverupBackfire_DoublesTheOriginalDamage()
        {
            // 罪＋隠蔽罪＝素の失脚ダメージ(0.5×0.5=0.25)の倍返し
            Assert.AreEqual(0.5f, ScandalRules.CoverupBackfire(0.5f, P), 1e-5f);
            Assert.AreEqual(2f * ScandalRules.ReputationDamage(0.5f, 0f, P),
                            ScandalRules.CoverupBackfire(0.5f, P), 1e-5f);
            // 必ず元の醜聞より重い（上限1にクランプ）
            Assert.Greater(ScandalRules.CoverupBackfire(0.5f, P), ScandalRules.ReputationDamage(0.5f, 0f, P));
            Assert.AreEqual(1f, ScandalRules.CoverupBackfire(1f, P), 1e-5f);
        }

        [Test]
        public void ScandalFatigue_PublicNumbsWithRepetition()
        {
            Assert.AreEqual(1f, ScandalRules.ScandalFatigue(0, P), 1e-5f);     // 初物は満額効く
            Assert.AreEqual(0.5f, ScandalRules.ScandalFatigue(2, P), 1e-5f);   // 1/(1+0.5×2)
            Assert.AreEqual(0.25f, ScandalRules.ScandalFatigue(6, P), 1e-5f);  // 続発で麻痺
            Assert.AreEqual(1f, ScandalRules.ScandalFatigue(-3, P), 1e-5f);    // 負数は0件扱い
        }

        [Test]
        public void TimingWeaponValue_ElectionEveDoubles()
        {
            Assert.AreEqual(0.4f, ScandalRules.TimingWeaponValue(0.4f, 0f, P), 1e-5f);  // 平時＝素のダメージ
            Assert.AreEqual(0.8f, ScandalRules.TimingWeaponValue(0.4f, 1f, P), 1e-5f);  // 選挙前夜＝2倍の兵器価値
        }
    }
}
