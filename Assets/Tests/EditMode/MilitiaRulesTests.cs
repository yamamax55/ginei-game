using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>民兵・自警団（市民の自衛武装組織）の純ロジックテスト。既定Paramsで期待値を固定。</summary>
    public class MilitiaRulesTests
    {
        const float Tol = 1e-4f;
        const float PowTol = 1e-3f; // Pow/Sqrt を含む箇所

        /// <summary>動員の早さ＝即応性×人口で頭数が早く揃う。</summary>
        [Test]
        public void MobilizationSpeed_即応性と人口で早く集まる()
        {
            // 即応0.6×(1+人口0.8×重み0.5)=0.6×1.4=0.84
            Assert.AreEqual(0.84f, MilitiaRules.MobilizationSpeed(0.6f, 0.8f), Tol);

            // 即応性0なら人口があっても動かない（積）
            Assert.AreEqual(0f, MilitiaRules.MobilizationSpeed(0f, 1f), Tol);
            // 人口が多いほど早い
            Assert.Greater(MilitiaRules.MobilizationSpeed(0.6f, 1f),
                MilitiaRules.MobilizationSpeed(0.6f, 0f));
        }

        /// <summary>地元防衛の士気＝郷土への脅威×地域の絆で郷土愛が燃える。</summary>
        [Test]
        public void HomeDefenseMorale_脅威と絆で郷土愛が燃える()
        {
            // 基礎0.3＋脅威0.7×絆0.6×重み1.0=0.3+0.42=0.72
            Assert.AreEqual(0.72f, MilitiaRules.HomeDefenseMorale(0.7f, 0.6f), Tol);

            // 脅威も絆もゼロでも基礎士気は残る
            Assert.AreEqual(0.3f, MilitiaRules.HomeDefenseMorale(0f, 1f), Tol);
            // 故郷を侵されるほど死力を尽くす
            Assert.Greater(MilitiaRules.HomeDefenseMorale(1f, 1f),
                MilitiaRules.HomeDefenseMorale(0.3f, 1f));
        }

        /// <summary>訓練不足＝正規訓練の欠如がそのまま質の低さになる。</summary>
        [Test]
        public void TrainingDeficiency_訓練が欠けるほど質が低い()
        {
            // (1-0.25)×倍率0.8=0.75×0.8=0.6
            Assert.AreEqual(0.6f, MilitiaRules.TrainingDeficiency(0.25f), Tol);

            // 完全訓練なら不足なし
            Assert.AreEqual(0f, MilitiaRules.TrainingDeficiency(1f), Tol);
            // 訓練が薄いほど質が低い（単調減少の逆）
            Assert.Greater(MilitiaRules.TrainingDeficiency(0f),
                MilitiaRules.TrainingDeficiency(0.5f));
        }

        /// <summary>生産との両立コスト＝動員率×農繁期で畑が荒れる。</summary>
        [Test]
        public void EconomicTradeoff_農繁期に動員するほど生産が傷む()
        {
            // 動員0.5×(1+農繁0.8×重み0.6)=0.5×1.48=0.74
            Assert.AreEqual(0.74f, MilitiaRules.EconomicTradeoff(0.5f, 0.8f), Tol);

            // 動員ゼロなら畑は無傷
            Assert.AreEqual(0f, MilitiaRules.EconomicTradeoff(0f, 1f), Tol);
            // 同じ動員でも農繁期ほど痛い
            Assert.Greater(MilitiaRules.EconomicTradeoff(0.5f, 1f),
                MilitiaRules.EconomicTradeoff(0.5f, 0f));
        }

        /// <summary>長期戦への不向き＝戦役が長引くほど（民間の義務が重いほど）崩れる。</summary>
        [Test]
        public void ProtractedWarWeakness_長引くほど持久に耐えない()
        {
            // 0.5^1.5=0.353553×(1+義務0.6×重み0.5)=×1.3 → 0.459619
            Assert.AreEqual(0.459619f, MilitiaRules.ProtractedWarWeakness(0.5f, 0.6f), PowTol);

            // 開戦直後（長さ0）なら弱点は出ない
            Assert.AreEqual(0f, MilitiaRules.ProtractedWarWeakness(0f, 1f), Tol);
            // 長引くほど不向きが増す（非線形に加速）
            Assert.Greater(MilitiaRules.ProtractedWarWeakness(0.9f, 0.6f),
                MilitiaRules.ProtractedWarWeakness(0.5f, 0.6f));
        }

        /// <summary>地の利＝地形熟知×地元士気の幾何平均でどちらか欠けると活きない。</summary>
        [Test]
        public void HomeGroundAdvantage_地形熟知と士気の両方が要る()
        {
            // sqrt(地形0.8×士気0.5)=sqrt(0.4)=0.632456
            Assert.AreEqual(0.632456f, MilitiaRules.HomeGroundAdvantage(0.8f, 0.5f), PowTol);

            // 地形を知らなければ地の利なし（積）
            Assert.AreEqual(0f, MilitiaRules.HomeGroundAdvantage(0f, 1f), Tol);
            // 士気がゼロでも地の利なし
            Assert.AreEqual(0f, MilitiaRules.HomeGroundAdvantage(1f, 0f), Tol);
        }

        /// <summary>民兵の戦闘力＝士気×(1-訓練不足)×地の利。素人でも地の利で押し返す。</summary>
        [Test]
        public void CombatEffectiveness_士気と訓練と地の利で決まる()
        {
            // 士気0.72×(1-訓練不足0.6)×(1+地の利0.5×重み0.5)=0.72×0.4×1.25=0.36
            Assert.AreEqual(0.36f, MilitiaRules.CombatEffectiveness(0.72f, 0.6f, 0.5f), Tol);

            // 訓練不足が満杯なら戦闘力ゼロ
            Assert.AreEqual(0f, MilitiaRules.CombatEffectiveness(1f, 1f, 1f), Tol);
            // 地の利が高いほど戦闘力が増す
            Assert.Greater(MilitiaRules.CombatEffectiveness(0.72f, 0.6f, 1f),
                MilitiaRules.CombatEffectiveness(0.72f, 0.6f, 0f));
        }

        /// <summary>本土防衛価値＝動員速度×戦闘力−長期戦弱点。守りに厚いが持久で価値が減る。</summary>
        [Test]
        public void HomelandDefenseValue_守りに厚いが持久で価値が減る()
        {
            // 速度0.84×戦闘力0.36−弱点0.4×重み0.5=0.3024−0.2=0.1024
            Assert.AreEqual(0.1024f, MilitiaRules.HomelandDefenseValue(0.84f, 0.36f, 0.4f), Tol);

            // 長期戦弱点が大きいほど価値が下がる
            Assert.Less(MilitiaRules.HomelandDefenseValue(0.84f, 0.36f, 1f),
                MilitiaRules.HomelandDefenseValue(0.84f, 0.36f, 0f));
            // 負にはならず0でクランプ
            Assert.AreEqual(0f, MilitiaRules.HomelandDefenseValue(0.1f, 0.1f, 1f), Tol);
        }

        /// <summary>
        /// 物語：故郷を侵された農村が即座に総動員し、地形を知り尽くした民兵が侵略者を撃退するが、
        /// 戦が長引けば農地への責務で崩れる＝短期の本土防衛でこそ輝く。
        /// </summary>
        [Test]
        public void Story_郷土を守る民兵は短期防衛で輝き持久で崩れる()
        {
            var p = MilitiaParams.Default;

            // 高い即応×多い人口 → 動員は満杯近く（クランプ）
            float speed = MilitiaRules.MobilizationSpeed(0.8f, 0.9f, p);
            Assert.AreEqual(1f, speed, Tol); // 0.8×1.45=1.16→1

            // 故郷を侵される怒り＋厚い絆 → 士気は満杯（クランプ）
            float morale = MilitiaRules.HomeDefenseMorale(0.9f, 0.8f, p);
            Assert.AreEqual(1f, morale, Tol); // 0.3+0.72=1.02→1

            // ろくな訓練がない寄せ集め
            float deficiency = MilitiaRules.TrainingDeficiency(0.2f, p); // 0.8×0.8=0.64
            Assert.AreEqual(0.64f, deficiency, Tol);

            // だが庭先の地形を知り尽くす
            float hga = MilitiaRules.HomeGroundAdvantage(0.9f, morale, p); // sqrt(0.9)=0.948683
            Assert.AreEqual(0.948683f, hga, PowTol);

            // 素人でも地の利で侮れない戦闘力
            float combat = MilitiaRules.CombatEffectiveness(morale, deficiency, hga, p);
            // 1×(1-0.64)×(1+0.948683×0.5)=0.36×1.474342=0.530763
            Assert.AreEqual(0.530763f, combat, PowTol);

            // 短期戦（長さ0.2）なら弱点は小さい
            float shortWar = MilitiaRules.ProtractedWarWeakness(0.2f, 0.5f, p);
            float shortValue = MilitiaRules.HomelandDefenseValue(speed, combat, shortWar, p);

            // 長期戦（長さ0.95）なら弱点が大きく価値が崩れる
            float longWar = MilitiaRules.ProtractedWarWeakness(0.95f, 0.5f, p);
            float longValue = MilitiaRules.HomelandDefenseValue(speed, combat, longWar, p);

            // 短期防衛でこそ価値が高い＝持久で崩れる
            Assert.Greater(shortValue, longValue);
            Assert.Greater(shortValue, 0.4f); // 短期は確かに守りに厚い
        }
    }
}
