using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>状況認識（戦場の全体把握とOODA・三層）の純ロジックのテスト。</summary>
    public class SituationalAwarenessRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>収集（第一層）＝センサー網×偵察資産（reconWeight=0.4）で情報を集める。</summary>
        [Test]
        public void Perception_センサーと偵察で情報を集める()
        {
            // センサー0.6/偵察0.9＝0.6×0.6 + 0.9×0.4 = 0.36 + 0.36 = 0.72。
            Assert.AreEqual(0.72f, SituationalAwarenessRules.Perception(0.6f, 0.9f), Eps);
            // どちらも満点なら収集も満点。
            Assert.AreEqual(1f, SituationalAwarenessRules.Perception(1f, 1f), Eps);
            // センサー皆無でも偵察で reconWeight ぶんは埋まる（偵察1.0＝0 + 0.4 = 0.4）。
            Assert.AreEqual(0.4f, SituationalAwarenessRules.Perception(0f, 1f), Eps);
            // 過大入力はクランプ（センサー2/偵察2でも1.0止まり）。
            Assert.AreEqual(1f, SituationalAwarenessRules.Perception(2f, 2f), Eps);
        }

        /// <summary>理解（第二層）＝収集×分析力の積。どちらも要る。</summary>
        [Test]
        public void Comprehension_収集と分析の積で意味づける()
        {
            // 収集0.72/分析0.8＝0.576。
            Assert.AreEqual(0.576f, SituationalAwarenessRules.Comprehension(0.72f, 0.8f), Eps);
            // 分析力ゼロなら情報を集めても理解は0（解釈できねば意味づかない）。
            Assert.AreEqual(0f, SituationalAwarenessRules.Comprehension(1f, 0f), Eps);
        }

        /// <summary>投射（第三層）＝理解×伝達率0.8×予測モデルで将来を読む。</summary>
        [Test]
        public void Projection_理解と予測で将来を投射する()
        {
            // 理解0.5/予測0.8＝(0.5×0.8)×0.8 = 0.4×0.8 = 0.32。
            Assert.AreEqual(0.32f, SituationalAwarenessRules.Projection(0.5f, 0.8f), Eps);
            // 理解ゼロなら予測の足場が無く投射も0。
            Assert.AreEqual(0f, SituationalAwarenessRules.Projection(0f, 1f), Eps);
            // 理解満点/予測満点でも伝達率0.8で頭打ち＝0.8。
            Assert.AreEqual(0.8f, SituationalAwarenessRules.Projection(1f, 1f), Eps);
        }

        /// <summary>戦場の霧によるギャップ＝敵の隠蔽×(1-収集)。</summary>
        [Test]
        public void FogOfWarGap_隠蔽と収集不足で穴があく()
        {
            // 隠蔽0.8/収集0.7＝0.8×0.3 = 0.24。
            Assert.AreEqual(0.24f, SituationalAwarenessRules.FogOfWarGap(0.8f, 0.7f), Eps);
            // 収集が満ちれば霧は晴れる（隠蔽1.0でも収集1.0なら0）。
            Assert.AreEqual(0f, SituationalAwarenessRules.FogOfWarGap(1f, 1f), Eps);
            // 隠蔽が無ければギャップも無い。
            Assert.AreEqual(0f, SituationalAwarenessRules.FogOfWarGap(0f, 0f), Eps);
        }

        /// <summary>OODAの速さ＝理解/(1+決定遅延×latencyPenalty1.0)。</summary>
        [Test]
        public void OodaSpeed_理解と決定遅延で回る速さ()
        {
            // 理解0.6/遅延1.0＝0.6/(1+1) = 0.3。
            Assert.AreEqual(0.3f, SituationalAwarenessRules.OodaSpeed(0.6f, 1f), Eps);
            // 遅延ゼロなら理解そのままの速さ。
            Assert.AreEqual(0.6f, SituationalAwarenessRules.OodaSpeed(0.6f, 0f), Eps);
            // 遅延3で大きく鈍る（0.8/(1+3) = 0.2）。
            Assert.AreEqual(0.2f, SituationalAwarenessRules.OodaSpeed(0.8f, 3f), Eps);
        }

        /// <summary>敵の意図の読み＝投射×行動パターンの積。</summary>
        [Test]
        public void EnemyIntentReading_投射と行動パターンで意図を読む()
        {
            // 投射0.5/パターン0.6＝0.3。
            Assert.AreEqual(0.3f, SituationalAwarenessRules.EnemyIntentReading(0.5f, 0.6f), Eps);
            // 行動がパターン化していなければ意図は読めない。
            Assert.AreEqual(0f, SituationalAwarenessRules.EnemyIntentReading(1f, 0f), Eps);
        }

        /// <summary>認識優位＝OODA×意図読み−霧×fogWeight0.5（先手は正・後手は負・-1..1）。</summary>
        [Test]
        public void AwarenessAdvantage_先手は正後手は負()
        {
            // OODA0.8/意図0.5/霧0.4＝0.8×0.5 - 0.4×0.5 = 0.4 - 0.2 = 0.2（先手）。
            Assert.AreEqual(0.2f, SituationalAwarenessRules.AwarenessAdvantage(0.8f, 0.5f, 0.4f), Eps);
            // 速さも意図読みも無く霧が深ければ後手（0 - 1.0×0.5 = -0.5）。
            Assert.AreEqual(-0.5f, SituationalAwarenessRules.AwarenessAdvantage(0f, 0f, 1f), Eps);
            // 速く回し意図を読み切り霧が無ければ満額の先手（1×1 - 0 = 1）。
            Assert.AreEqual(1f, SituationalAwarenessRules.AwarenessAdvantage(1f, 1f, 0f), Eps);
        }

        /// <summary>誤認のリスク＝霧×認知バイアス×biasWeight0.6。</summary>
        [Test]
        public void MisperceptionRisk_霧とバイアスの積が誤認()
        {
            // 霧0.5/バイアス0.8＝0.5×0.8×0.6 = 0.24。
            Assert.AreEqual(0.24f, SituationalAwarenessRules.MisperceptionRisk(0.5f, 0.8f), Eps);
            // 霧が無ければ誤認も無い（穴が無いので推測の余地が無い）。
            Assert.AreEqual(0f, SituationalAwarenessRules.MisperceptionRisk(0f, 1f), Eps);
            // バイアスが無ければ霧があっても誤認は最小（冷静に未知と扱う）。
            Assert.AreEqual(0f, SituationalAwarenessRules.MisperceptionRisk(1f, 0f), Eps);
        }

        /// <summary>
        /// 物語＝霧の濃い夜戦。優れた提督は偵察を出し（収集↑）分析で状況を理解し将来を投射、
        /// OODAを速く回して敵意図を読み、霧を晴らして先手を取り誤認も避ける。対する凡将は逆になる。
        /// </summary>
        [Test]
        public void Story_霧の夜戦で名将が先手を取り凡将が後手に回る()
        {
            // ── 名将：偵察を厚く出し分析と予測に長け判断が速い ──
            float acePerc = SituationalAwarenessRules.Perception(0.7f, 0.9f);       // 0.7×0.6+0.9×0.4=0.78
            float aceComp = SituationalAwarenessRules.Comprehension(acePerc, 0.9f); // 0.78×0.9=0.702
            float aceProj = SituationalAwarenessRules.Projection(aceComp, 0.8f);    // (0.702×0.8)×0.8
            float aceFog = SituationalAwarenessRules.FogOfWarGap(0.8f, acePerc);    // 0.8×(1-0.78)
            float aceOoda = SituationalAwarenessRules.OodaSpeed(aceComp, 0.3f);     // 速い判断
            float aceIntent = SituationalAwarenessRules.EnemyIntentReading(aceProj, 0.7f);
            float aceAdv = SituationalAwarenessRules.AwarenessAdvantage(aceOoda, aceIntent, aceFog);
            float aceMis = SituationalAwarenessRules.MisperceptionRisk(aceFog, 0.3f);

            // ── 凡将：偵察を出さず分析も予測も鈍く判断が遅い ──
            float dullPerc = SituationalAwarenessRules.Perception(0.4f, 0.1f);
            float dullComp = SituationalAwarenessRules.Comprehension(dullPerc, 0.4f);
            float dullProj = SituationalAwarenessRules.Projection(dullComp, 0.3f);
            float dullFog = SituationalAwarenessRules.FogOfWarGap(0.8f, dullPerc);
            float dullOoda = SituationalAwarenessRules.OodaSpeed(dullComp, 2.5f);
            float dullIntent = SituationalAwarenessRules.EnemyIntentReading(dullProj, 0.7f);
            float dullAdv = SituationalAwarenessRules.AwarenessAdvantage(dullOoda, dullIntent, dullFog);
            float dullMis = SituationalAwarenessRules.MisperceptionRisk(dullFog, 0.7f);

            // 名将は霧が薄く認識優位（先手）を握る。凡将は霧が濃く後手に回る。
            Assert.Less(aceFog, dullFog, "名将は偵察で霧を晴らす");
            Assert.Greater(aceAdv, dullAdv, "名将が先手を取る");
            Assert.Greater(aceAdv, 0f, "名将は認識優位（先手）");
            Assert.Less(dullAdv, 0f, "凡将は後手（認識劣位）");
            // 名将は誤認も少ない（霧が薄くバイアスも抑えている）。
            Assert.Less(aceMis, dullMis, "名将は誤認も避ける");
        }
    }
}
