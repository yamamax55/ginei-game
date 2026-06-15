using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 人物 archetype（#軍神/#日本一の兵/#猛将/#覇王/#半身/#魔術師…）の<b>会戦修正子を合成する単一窓口</b>。
    /// 各 archetype の純ロジック（PeerlessWarriorRules / FierceGeneralRules / MagicianRules / KaiserRules / RightHandRules …）が返す
    /// 係数のうち、<b>常時・または「残存兵力比」だけで決まる戦闘修正</b>を `ModifierStack`(#106) で積んで与効果/被ダメ/士気の倍率を返す。
    /// <b>archetype フラグが立っていない提督は全て 1.0 を返す＝既存シナリオは完全に挙動不変（後方互換）</b>。
    /// 状況依存の強い効果（包囲#2178/各個撃破/暴走/宿敵/兵站/登用/忠誠/成長…）は各専用システムや戦略Tick側で配線し、
    /// ここでは「戦場の与ダメ・被ダメ・士気」に効く分だけを束ねる（二重実装しない）。実効値パターン・test-first。
    /// </summary>
    public static class AdmiralArchetypeModifiers
    {
        /// <summary>
        /// 与効果倍率（常時の武勇＋残存兵力比に応じた逆転/決死）。ownHpRatio=残存兵力/最大(0..1)。
        /// </summary>
        public static float AttackFactor(AdmiralData admiral, float ownHpRatio)
        {
            if (admiral == null) return 1f;
            float hp = Mathf.Clamp01(ownHpRatio);
            var s = ModifierStack.Start();
            s.Mul(PeerlessWarriorRules.ValorFactor(admiral.isPeerlessWarrior));          // とにかく強い（日本一の兵）
            s.Mul(PeerlessWarriorRules.DeathChargeFactor(admiral.isPeerlessWarrior, hp)); // 決死の突撃（窮地ほど苛烈）
            s.Mul(FierceGeneralRules.ChargeAttackFactor(admiral.isFierceGeneral));        // 猪突猛進
            s.Mul(MagicianRules.ComebackFactor(admiral.isMagician, 1f - hp));             // 奇跡のヤン（劣勢ほど逆転）
            return s.Value;
        }

        /// <summary>被ダメ倍率（猪突の隙で増・端正な陣形で減）。</summary>
        public static float DamageTakenFactor(AdmiralData admiral)
        {
            if (admiral == null) return 1f;
            var s = ModifierStack.Start();
            s.Mul(FierceGeneralRules.RecklessDamageTakenFactor(admiral.isFierceGeneral)); // 猪突の隙＝被ダメ増
            s.Mul(RightHandRules.FormationDamageTakenFactor(admiral.isRightHand));         // 端正な陣形＝被ダメ減
            return s.Value;
        }

        /// <summary>士気倍率（黄金の獅子のカリスマで限界突破）。</summary>
        public static float MoraleFactor(AdmiralData admiral)
        {
            if (admiral == null) return 1f;
            return KaiserRules.CharismaMoraleFactor(admiral.isKaiser); // 黄金の獅子
        }
    }
}
