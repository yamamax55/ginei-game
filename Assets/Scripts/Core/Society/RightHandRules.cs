using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 半身（理想のナンバーツー）の純ロジック（#半身・銀河英雄伝説＝ジークフリード・キルヒアイス）。覇王 <see cref="KaiserRules"/>（ラインハルト）の対。
    /// 万能にして完璧、周囲を惹きつける包容力と、主君を支え切る最強の守護者を再現する：
    /// ①<b>万能</b>（攻防速知すべて欠点なし＝どの役割でも適応度100%＝役割不一致ペナルティ無し）、
    /// ②<b>流血なき勝利の懐柔</b>（対話・心理戦で敵の戦意を挫く＝カストロプ動乱の速攻鎮圧）、
    /// ③<b>端正な陣形</b>（奇策を打たず隙なし＝自軍の被ダメ軽減）、
    /// ④<b>無私の献身（身代わり）</b>（味方の致命傷を肩代わり・命と引き換えに味方を超強化）、
    /// ⑤<b>主君の良心</b>（健在の間は相棒の暴走を止める＝覇王の `IsBerserk` を抑える）、
    /// ⑥<b>アンネローゼの誓い</b>（精神攻撃・魅了100%無効）、⑦<b>真のカリスマ</b>（交渉100%成功）、
    /// ⑧<b>絆の相関</b>（覇王/アタッカーと組むとその能力を倍化。ただし半身が先に戦脱すると相棒が暴走する＝`KaiserRules.IsBerserk`）。
    /// 数式は係数を返すだけで既存窓口（`PersonRules`役割一致/`MoraleShockRules`#2176/`KaiserRules`#覇王/外交#189）へ橋渡しする。
    /// 実効値パターン・決定論・test-first。
    /// </summary>
    public static class RightHandRules
    {
        /// <summary>流血なき懐柔で敵の攻撃（戦意）を挫く割合。</summary>
        public const float PacificationDebuff = 0.25f;
        /// <summary>端正な陣形による被ダメ倍率（隙なし）。</summary>
        public const float FormationDamageTaken = 0.85f;
        /// <summary>身代わりの献身で味方を超強化する倍率（命と引き換え）。</summary>
        public const float MartyrAllyBuff = 2.0f;
        /// <summary>絆の相関＝覇王/アタッカーと組んだときの倍化倍率。</summary>
        public const float PartnerSynergy = 2.0f;

        /// <summary>
        /// 役割適応の倍率。半身は万能で役割不一致でも100%（1.0）。並は不一致で `PersonParams.mismatchPenalty` 相当（0.5）。
        /// </summary>
        public static float RoleAdaptationFactor(bool isRightHand, bool roleMatches)
        {
            if (isRightHand) return 1f;        // 万能＝どの役割でも欠点なし
            return roleMatches ? 1f : 0.5f;
        }

        /// <summary>流血なき懐柔＝敵の攻撃（戦意）を挫く割合（並は0）。`MoraleShockRules`#2176 等へ。</summary>
        public static float PacificationAttackDebuff(bool isRightHand)
            => isRightHand ? PacificationDebuff : 0f;

        /// <summary>端正な陣形による被ダメ倍率（並は1.0）。</summary>
        public static float FormationDamageTakenFactor(bool isRightHand)
            => isRightHand ? FormationDamageTaken : 1f;

        /// <summary>味方の致命傷を肩代わりできるか（無私の献身・身代わり）。</summary>
        public static bool CanShieldAlly(bool isRightHand) => isRightHand;

        /// <summary>身代わりに際し味方へ与える超強化倍率（命と引き換え・非発動は1.0）。</summary>
        public static float MartyrAllyBuffFactor(bool sacrificing)
            => sacrificing ? MartyrAllyBuff : 1f;

        /// <summary>相棒の暴走を止めるか（主君の良心＝健在の間は覇王の `IsBerserk` を抑える）。</summary>
        public static bool PreventsBerserk(bool isRightHand, bool alive)
            => isRightHand && alive;

        /// <summary>精神攻撃・魅了に完全耐性か（アンネローゼの誓い＝行動原理が不動）。</summary>
        public static bool ImmuneToMentalAttack(bool isRightHand) => isRightHand;

        /// <summary>交渉を必ず成功させるか（真のカリスマ＝誰からも慕われる）。</summary>
        public static bool GuaranteesNegotiation(bool isRightHand) => isRightHand;

        /// <summary>絆の相関＝覇王/アタッカーと組んだときの相棒能力の倍化倍率（同時出撃時のみ・並は1.0）。</summary>
        public static float PartnerSynergyFactor(bool isRightHand, bool pairedWithPartner)
            => (isRightHand && pairedWithPartner) ? PartnerSynergy : 1f;
    }
}
