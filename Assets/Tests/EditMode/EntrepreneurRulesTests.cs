using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 企業家類型と起業活動（#1584・SCHU-2・シュンペーター）の純ロジック検証。
    /// 企業家精神の合成・企業家/管理者の弁別・イノベーション産出・運営の逆相関・起業の成否・起業環境を担保。
    /// </summary>
    public class EntrepreneurRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>企業家精神＝リスク0.4×先見0.35×現状打破0.25の加重和（既定Params）。</summary>
        [Test]
        public void EntrepreneurialDrive_既定の加重和()
        {
            // 0.8*0.4 + 0.6*0.35 + 1.0*0.25 = 0.32 + 0.21 + 0.25 = 0.78
            float drive = EntrepreneurRules.EntrepreneurialDrive(0.8f, 0.6f, 1.0f);
            Assert.AreEqual(0.78f, drive, Eps);
        }

        /// <summary>類型弁別＝企業家精神が既定閾値0.5以上で企業家、未満で管理者。</summary>
        [Test]
        public void TypeOf_閾値で企業家と管理者を弁別()
        {
            Assert.AreEqual(EntrepreneurType.企業家, EntrepreneurRules.TypeOf(0.5f));  // ちょうど閾値＝企業家
            Assert.AreEqual(EntrepreneurType.企業家, EntrepreneurRules.TypeOf(0.78f));
            Assert.AreEqual(EntrepreneurType.管理者, EntrepreneurRules.TypeOf(0.3f));
        }

        /// <summary>イノベーション産出＝企業家精神×機会の積（どちらか欠ければ実らない）。</summary>
        [Test]
        public void InnovationOutput_企業家精神と機会の積()
        {
            Assert.AreEqual(0.48f, EntrepreneurRules.InnovationOutput(0.8f, 0.6f), Eps);
            Assert.AreEqual(0f, EntrepreneurRules.InnovationOutput(0.9f, 0f), Eps); // 機会なし＝産出なし
            Assert.AreEqual(0f, EntrepreneurRules.InnovationOutput(0f, 0.9f), Eps); // 担い手なし＝産出なし
        }

        /// <summary>運営安定性は企業家精神の逆相関＝破壊者は運営に向かない。</summary>
        [Test]
        public void ManagerialEfficiency_企業家精神と逆相関()
        {
            // 高driveの企業家は運営が不安定・低driveの管理者は安定
            Assert.AreEqual(0.22f, EntrepreneurRules.ManagerialEfficiency(0.78f), Eps);
            Assert.AreEqual(0.9f, EntrepreneurRules.ManagerialEfficiency(0.1f), Eps);
            Assert.Greater(EntrepreneurRules.ManagerialEfficiency(0.1f),
                EntrepreneurRules.ManagerialEfficiency(0.9f));
        }

        /// <summary>起業の成否＝適合(drive×資本×市場)/必要適合0.5を確率にしてrollと比較（決定論）。</summary>
        [Test]
        public void StartupSuccess_適合がrollを上回れば成功()
        {
            // fit = 0.8*0.7*0.6 = 0.336 → chance = 0.336/0.5 = 0.672
            Assert.IsTrue(EntrepreneurRules.StartupSuccess(0.8f, 0.7f, 0.6f, 0.5f));   // roll<chance＝成功
            Assert.IsFalse(EntrepreneurRules.StartupSuccess(0.8f, 0.7f, 0.6f, 0.7f));  // roll≧chance＝失敗
            // 資本ゼロは適合0＝必ず失敗
            Assert.IsFalse(EntrepreneurRules.StartupSuccess(0.9f, 0f, 0.9f, 0.0f));
        }

        /// <summary>創造的破壊はイノベーション産出をそのまま破壊圧へ（0..1クランプ）。</summary>
        [Test]
        public void CreativeDisruption_産出を破壊圧へ()
        {
            Assert.AreEqual(0.48f, EntrepreneurRules.CreativeDisruption(0.48f), Eps);
            Assert.AreEqual(1f, EntrepreneurRules.CreativeDisruption(1.5f), Eps); // 上限クランプ
        }

        /// <summary>破滅確率＝リスク×(1−資本)を上限0.8で抑える（過大リスク×薄資本ほど高い）。</summary>
        [Test]
        public void RiskOfRuin_過大リスクと薄い資本で上昇()
        {
            // 0.9*(1-0.2) = 0.72 ≤ 上限0.8
            Assert.AreEqual(0.72f, EntrepreneurRules.RiskOfRuin(0.9f, 0.2f), Eps);
            // 1.0*(1-0) = 1.0 → 上限0.8でクランプ
            Assert.AreEqual(0.8f, EntrepreneurRules.RiskOfRuin(1f, 0f), Eps);
            // 厚い資本は破滅を抑える
            Assert.AreEqual(0f, EntrepreneurRules.RiskOfRuin(0.9f, 1f), Eps);
        }

        /// <summary>起業環境＝支援−規制で1を中心に増減（支援厚で1超・規制重で1未満）。</summary>
        [Test]
        public void ClimateMultiplier_支援と規制で増減()
        {
            Assert.AreEqual(1.3f, EntrepreneurRules.ClimateMultiplier(0.5f, 0.2f), Eps); // 支援優位＝促進
            Assert.AreEqual(0.6f, EntrepreneurRules.ClimateMultiplier(0.2f, 0.6f), Eps); // 規制優位＝抑制
            Assert.AreEqual(1f, EntrepreneurRules.ClimateMultiplier(0.4f, 0.4f), Eps);   // 拮抗＝中立
        }
    }
}
