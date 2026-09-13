using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 採用仕様 #67 の第4次分：軍団の<b>解除</b>まで含めて、命令の出どころで指揮権判定を分ける方針を固定する。
    /// 「通常入力・公開APIの既定はプレイヤー権限」「AI/QA/内部の自動失効だけが明示的に素通しできる」を担保。
    /// </summary>
    public class CommandOrderSourceTests
    {
        // ===== 既定（公開APIを引数なしで呼んだときの扱い） =====

        /// <summary>★公開APIの既定はプレイヤー＝引数を省いた呼び出しに抜け道を作らない。</summary>
        [Test]
        public void Default_IsPlayer()
        {
            Assert.AreEqual(CommandOrderSource.プレイヤー, CommandOrderSourceRules.Default);
            Assert.IsTrue(CommandOrderSourceRules.RequiresAuthorityCheck(CommandOrderSourceRules.Default),
                          "既定の出どころで権限判定が働かないと、系統外の解除が素通りする");
        }

        // ===== 出どころごとの判定要否 =====

        [Test]
        public void Player_RequiresAuthorityCheck()
        {
            Assert.IsTrue(CommandOrderSourceRules.RequiresAuthorityCheck(CommandOrderSource.プレイヤー));
        }

        /// <summary>AI・内部の自動失効（軍団消滅・総退却）は盤面の都合なのでプレイヤーの権限では止めない。</summary>
        [Test]
        public void Ai_SkipsAuthorityCheck()
        {
            Assert.IsFalse(CommandOrderSourceRules.RequiresAuthorityCheck(CommandOrderSource.AI));
        }

        /// <summary>QA 専用入口（Editor メニュー）は通常入力から呼べないので素通し。</summary>
        [Test]
        public void Qa_SkipsAuthorityCheck()
        {
            Assert.IsFalse(CommandOrderSourceRules.RequiresAuthorityCheck(CommandOrderSource.QA));
        }

        [Test]
        public void Labels_AreDistinct()
        {
            string p = CommandOrderSourceRules.Label(CommandOrderSource.プレイヤー);
            string a = CommandOrderSourceRules.Label(CommandOrderSource.AI);
            string q = CommandOrderSourceRules.Label(CommandOrderSource.QA);
            Assert.AreNotEqual(p, a);
            Assert.AreNotEqual(a, q);
            Assert.AreNotEqual(p, q);
            Assert.IsFalse(string.IsNullOrEmpty(CommandOrderSourceRules.Label((CommandOrderSource)999)),
                           "未知の値でも空文字を返さない");
        }

        // ===== 解除の権限（自軍／他軍団／混在） =====
        // CorpsFormation は MonoBehaviour なので、そこが呼ぶ判定そのもの（BattleCommandAuthorityRules）を
        // 「解除」の文脈で固定する。CorpsFormation.CanOrderFrom は
        //   出どころ判定（本 Rules）→ 指揮系統判定（BattleCommandAuthorityRules）
        // の順で委譲するだけ＝二重実装しない。

        /// <summary>★自軍団の解除は通る。</summary>
        [Test]
        public void Release_OwnCorps_IsAllowed()
        {
            var chain = new BattleChain("A軍団", false);
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "A軍団"));
        }

        /// <summary>★他軍団の解除はプレイヤーからは直接通らない（要請どまり）。</summary>
        [Test]
        public void Release_OtherCorps_IsNotDirect()
        {
            var chain = new BattleChain("A軍団", false);
            Assert.AreNotEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "B軍団"),
                "系統外の解除が直接通ってはいけない");
        }

        /// <summary>★混在選択は「通る分だけ」＝全部通す/全部止めるのどちらでもない。</summary>
        [Test]
        public void Release_MixedSelection_PartiallySucceeds()
        {
            var chain = new BattleChain("A軍団", false);
            string[] selected = { "A軍団", "B軍団", "A軍団", "C軍団" };
            int released = 0, blocked = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                bool ok = BattleCommandAuthorityRules.RightFor(chain, true, false, selected[i])
                          == BattleCommandRight.直接命令;
                if (ok) released++; else blocked++;
            }
            Assert.AreEqual(2, released, "自軍団ぶんは解除される");
            Assert.AreEqual(2, blocked, "他軍団ぶんは解除されない");
        }

        /// <summary>★AI の自動失効（軍団消滅・総退却）は系統に関係なく通る。</summary>
        [Test]
        public void Release_AutoExpiry_PassesRegardlessOfChain()
        {
            // 出どころが AI なら、指揮系統の判定そのものへ降りない。
            Assert.IsFalse(CommandOrderSourceRules.RequiresAuthorityCheck(CommandOrderSource.AI));

            // 参考：同じ状況をプレイヤーが手で叩けば止まる（＝分離できている）。
            var chain = new BattleChain("A軍団", false);
            Assert.IsTrue(CommandOrderSourceRules.RequiresAuthorityCheck(CommandOrderSource.プレイヤー));
            Assert.AreNotEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "B軍団"));
        }

        /// <summary>★敵軍団は誰の出どころであれプレイヤー操作では触れない。</summary>
        [Test]
        public void Release_EnemyCorps_IsBlockedForPlayer()
        {
            var top = new BattleChain("", true);
            Assert.AreEqual(BattleCommandRight.不可,
                BattleCommandAuthorityRules.RightFor(top, false, false, "敵の軍団"));
        }

        /// <summary>★戦役で系統が不明なら、他人の軍団の解除は全権限に読み替えない。</summary>
        [Test]
        public void Release_UnknownChainInCampaign_IsNotFullAuthority()
        {
            BattleChain chain = BattleCommandModeRules.ChainFor(BattleCommandMode.戦役, false, "");
            Assert.IsFalse(chain.commandsAll);
            Assert.AreNotEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "どこかの軍団"));
        }

        /// <summary>自由操作モード（戦役外の演習）は明示的なモードとして全部隊を解除できる。</summary>
        [Test]
        public void Release_FreePlayMode_IsExplicitlyAllowed()
        {
            BattleChain chain = BattleCommandModeRules.ChainFor(BattleCommandMode.自由操作, false, "");
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "他人の軍団"));
        }
    }
}
