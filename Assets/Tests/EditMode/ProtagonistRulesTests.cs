using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 主人公（アンカー提督・GON-6）判定の唯一の窓口 ProtagonistRules の仕様を固定する（test-first）。
    /// 主人公は「動かない光源」＝常にプレイヤー操作（AI非制御）。フラグ未設定なら従来どおり（後方互換）。
    /// </summary>
    public class ProtagonistRulesTests
    {
        private AdmiralData Make(bool protagonist)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.isProtagonist = protagonist;
            return a;
        }

        // ───────── IsProtagonist（null安全） ─────────

        [Test]
        public void IsProtagonist_Null_IsFalse()
        {
            Assert.IsFalse(ProtagonistRules.IsProtagonist(null));
        }

        [Test]
        public void IsProtagonist_DefaultAdmiral_IsFalse()
        {
            // 既定で false＝後方互換（既存アセットは主人公扱いされない）。
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            Assert.IsFalse(a.isProtagonist);
            Assert.IsFalse(ProtagonistRules.IsProtagonist(a));
        }

        [Test]
        public void IsProtagonist_Flagged_IsTrue()
        {
            Assert.IsTrue(ProtagonistRules.IsProtagonist(Make(true)));
        }

        // ───────── ShouldEnableAI ─────────

        [Test]
        public void ShouldEnableAI_NormalEnemy_IsTrue()
        {
            // 主人公でない・プレイヤー操作でない → 従来どおり AI 有効。
            Assert.IsTrue(ProtagonistRules.ShouldEnableAI(Make(false), isPlayerControlled: false));
        }

        [Test]
        public void ShouldEnableAI_NormalPlayer_IsFalse()
        {
            // 主人公でない・プレイヤー操作 → AI 無効（従来どおり）。
            Assert.IsFalse(ProtagonistRules.ShouldEnableAI(Make(false), isPlayerControlled: true));
        }

        [Test]
        public void ShouldEnableAI_Protagonist_AlwaysFalse()
        {
            // 主人公は陣営に関わらず常に AI 無効＝プレイヤーが握る。
            Assert.IsFalse(ProtagonistRules.ShouldEnableAI(Make(true), isPlayerControlled: false));
            Assert.IsFalse(ProtagonistRules.ShouldEnableAI(Make(true), isPlayerControlled: true));
        }

        [Test]
        public void ShouldEnableAI_NullAdmiral_FollowsPlayerControl()
        {
            // admiralData 無しの艦＝従来どおり（主人公上書き無し）。後方互換。
            Assert.IsTrue(ProtagonistRules.ShouldEnableAI(null, isPlayerControlled: false));
            Assert.IsFalse(ProtagonistRules.ShouldEnableAI(null, isPlayerControlled: true));
        }
    }
}
