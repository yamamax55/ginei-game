using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class CorpsBattleFlowRulesTests
    {
        const float Tol = 1e-4f;

        // ── ② 回り込みの発意ゲート ──

        [Test]
        public void 前面非交戦や後衛無しでは回り込まない()
        {
            Assert.IsFalse(CorpsBattleFlowRules.ShouldAttemptEnvelopment(false, 2, 0.9f, 0f));
            Assert.IsFalse(CorpsBattleFlowRules.ShouldAttemptEnvelopment(true, 0, 0.9f, 0f));
        }

        [Test]
        public void 有能な軍団長だけが回り込みを試みる()
        {
            // 能力0.9・roll0.3 → roll≤能力 で発動
            Assert.IsTrue(CorpsBattleFlowRules.ShouldAttemptEnvelopment(true, 1, 0.9f, 0.3f));
            // 能力0.4（しきい値0.55未満）→ 不発
            Assert.IsFalse(CorpsBattleFlowRules.ShouldAttemptEnvelopment(true, 1, 0.4f, 0.0f));
            // 能力0.6だが roll0.8（>能力）→ 取りこぼす
            Assert.IsFalse(CorpsBattleFlowRules.ShouldAttemptEnvelopment(true, 1, 0.6f, 0.8f));
        }

        [Test]
        public void 軍団長能力は統率と情報の正規化()
        {
            Assert.AreEqual(0.5f, CorpsBattleFlowRules.CommanderAbility(50, 50), Tol);   // 100/200
            Assert.AreEqual(0.9f, CorpsBattleFlowRules.CommanderAbility(90, 90), Tol);   // 180/200
            Assert.AreEqual(1.0f, CorpsBattleFlowRules.CommanderAbility(100, 100), Tol); // 200/200
        }

        // ── ② 回り込みの目標座標（幾何） ──

        [Test]
        public void 回り込み目標は敵の側面へ振れる()
        {
            // 敵が真上(0,10)、自分が原点、standoff5・offset8・side+1（右回り＝+X 側）
            Vector2 t = CorpsBattleFlowRules.FlankTarget(Vector2.zero, new Vector2(0f, 10f), 8f, 5f, 1);
            // approach = (0,10) - up*5 = (0,5)、perp(+1) = (-dir.y,dir.x)=(-1,0)*… ※dir=(0,1) → perp=(-1,0)
            // → approach + perp*8 = (0,5)+(-8,0) = (-8,5)
            Assert.AreEqual(-8f, t.x, Tol);
            Assert.AreEqual(5f, t.y, Tol);

            // side−1 なら反対側
            Vector2 t2 = CorpsBattleFlowRules.FlankTarget(Vector2.zero, new Vector2(0f, 10f), 8f, 5f, -1);
            Assert.AreEqual(8f, t2.x, Tol);
            Assert.AreEqual(5f, t2.y, Tol);
        }

        [Test]
        public void 回り込みの向きは既に居る側を選ぶ()
        {
            // 敵正面=+Y、包囲側が敵の右(+X)に居る → rel=(+,*)、cross = fwd.x*rel.y - fwd.y*rel.x = 0 - 1*rel.x <0 → 右(-1)
            int side = CorpsBattleFlowRules.ChooseFlankSide(new Vector2(5f, 0f), Vector2.zero, Vector2.up);
            Assert.AreEqual(-1, side);
            // 左(-X)に居る → +1
            int side2 = CorpsBattleFlowRules.ChooseFlankSide(new Vector2(-5f, 0f), Vector2.zero, Vector2.up);
            Assert.AreEqual(1, side2);
        }

        // ── ② カウンター検出 ──

        [Test]
        public void 横腹を脅かされたら包囲を察知する()
        {
            // 軍団正面=+Y。脅威が真後ろ(0,-10)＝角180°>100° かつ range20内 → true
            Assert.IsTrue(CorpsBattleFlowRules.IsBeingFlanked(Vector2.zero, Vector2.up, new Vector2(0f, -10f), 20f));
            // 脅威が正面(0,10)＝角0° → false
            Assert.IsFalse(CorpsBattleFlowRules.IsBeingFlanked(Vector2.zero, Vector2.up, new Vector2(0f, 10f), 20f));
            // 後方でも遠ければ（range5）false
            Assert.IsFalse(CorpsBattleFlowRules.IsBeingFlanked(Vector2.zero, Vector2.up, new Vector2(0f, -10f), 5f));
        }

        // ── ③ 決戦の投入 ──

        [Test]
        public void 敵脆弱性は露出と疲弊の積()
        {
            // 包囲進捗0.8（露出0.8）・敵兵力比0.4（疲弊0.6）→ 0.8*0.6=0.48
            Assert.AreEqual(0.48f, CorpsBattleFlowRules.EnemyVulnerability(0.8f, 0.4f), Tol);
            // 敵が満兵力（比1.0）なら疲弊0＝脆弱性0
            Assert.AreEqual(0f, CorpsBattleFlowRules.EnemyVulnerability(0.8f, 1.0f), Tol);
        }

        [Test]
        public void 機が熟せば決戦に踏み切る()
        {
            // 整い1.0・補給1.0・士気1.0 → 準備度1.0
            float rdy = CorpsBattleFlowRules.Readiness(1f, 1f, 1f);
            Assert.AreEqual(1f, rdy, Tol);
            // 敵脆弱性0.8（露出1.0・敵比0.2→疲弊0.8）
            float vuln = CorpsBattleFlowRules.EnemyVulnerability(1f, 0.2f);
            Assert.AreEqual(0.8f, vuln, Tol);
            float window = CorpsBattleFlowRules.WindowOpening(rdy, vuln); // 1.0*0.8=0.8
            Assert.AreEqual(0.8f, window, Tol);
            Assert.IsTrue(CorpsBattleFlowRules.ShouldCommitDecisive(window)); // 0.8≥0.6
        }

        [Test]
        public void 準備不足や敵が堅ければ決戦に踏み切らない()
        {
            // 高水準ヘルパ：整い0.3・士気0.3（準備低い）→ 窓が閾値未満
            bool commit = CorpsBattleFlowRules.ShouldCommitDecisive(0.3f, 1f, 0.3f, 0.9f, 0.1f, out float w);
            Assert.IsFalse(commit);
            Assert.Less(w, 0.6f);
        }
    }
}
