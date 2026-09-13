namespace Ginei
{
    /// <summary>会戦でその部隊に何ができるか。</summary>
    public enum BattleCommandRight
    {
        /// <summary>自分の指揮系統の内側＝直接命令できる（操作感を保つ）。</summary>
        直接命令,
        /// <summary>指揮系統の外＝直接は通さず、支援を要請する。</summary>
        要請,
        /// <summary>そもそも動かせない（敵・味方でない・行動不能）。</summary>
        不可,
    }

    /// <summary>
    /// いま操作している人物の<b>指揮系統</b>（会戦側で判定するのに要るぶんだけ）。
    /// 会戦の部隊は人物IDを持たないことがあるので、軍団名（<see cref="FleetStrength.corpsName"/> 相当）と
    /// 「自分の乗艦か」を主なキーにする。
    /// </summary>
    public readonly struct BattleChain
    {
        /// <summary>自分が指揮する軍団の名前（空＝軍団を率いていない）。</summary>
        public readonly string corpsName;

        /// <summary>
        /// 全軍の指揮権を持つか（総司令官）。<b>既定は true</b>＝主人公を特定できない会戦では
        /// 従来どおり全部を直接操作できる（後方互換。権限で遊べなくしない）。
        /// </summary>
        public readonly bool commandsAll;

        public BattleChain(string corpsName, bool commandsAll)
        {
            this.corpsName = corpsName ?? "";
            this.commandsAll = commandsAll;
        }

        /// <summary>従来どおり全部を直接操作できる系統（主人公が特定できないとき）。</summary>
        public static BattleChain Everything => new BattleChain("", true);
    }

    /// <summary>
    /// 会戦の<b>指揮系統</b>による命令の可否（GitHub #67）。
    ///
    /// <b>方針</b>：会戦の操作感は保つ＝<b>自分の指揮系統の内側は毎回の稟議なしで直接操作</b>。
    /// 系統の外へは直接命令を通さず、<b>支援要請</b>にする（同じ勢力でも他人の隷下は勝手に動かせない）。
    ///
    /// 判定は「対象が自分の乗艦か」「対象の軍団が自分の軍団か」「自分が全軍の指揮権を持つか」だけで決める
    /// ＝会戦側に新しい人事データを持ち込まない。敵・行動不能は <see cref="BattleCommandRight.不可"/>。
    ///
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BattleCommandAuthorityRules
    {
        /// <summary>
        /// その部隊に対する権限。
        /// <paramref name="isOwnSide"/>＝自勢力（敵は問答無用で不可）。
        /// <paramref name="isActorsOwnShip"/>＝操作者自身が乗っている部隊。
        /// <paramref name="targetCorpsName"/>＝対象の軍団名（空＝軍団に属さない独立部隊）。
        /// </summary>
        public static BattleCommandRight RightFor(in BattleChain actor, bool isOwnSide,
                                                  bool isActorsOwnShip, string targetCorpsName)
        {
            if (!isOwnSide) return BattleCommandRight.不可;
            if (actor.commandsAll) return BattleCommandRight.直接命令;   // 総司令官＝全部が自系統
            if (isActorsOwnShip) return BattleCommandRight.直接命令;     // 自分の乗艦

            // 自分の軍団の隷下だけが直接命令の範囲。
            if (!string.IsNullOrEmpty(actor.corpsName)
                && string.Equals(actor.corpsName, targetCorpsName ?? "")) return BattleCommandRight.直接命令;

            return BattleCommandRight.要請;   // 同じ勢力でも他系統＝要請
        }

        /// <summary>直接命令できるか（<see cref="BattleCommandRight.直接命令"/> のときだけ true）。</summary>
        public static bool CanCommand(in BattleChain actor, bool isOwnSide, bool isActorsOwnShip,
                                      string targetCorpsName)
            => RightFor(actor, isOwnSide, isActorsOwnShip, targetCorpsName) == BattleCommandRight.直接命令;

        /// <summary>
        /// 選択のうち何隊が直接動かせて、何隊が要請になるかの説明（<b>文章で</b>出す＝色だけに頼らない）。
        /// 全部が直接命令なら空文字（余計な注意書きを出さない）。
        /// </summary>
        public static string SelectionNote(int directCount, int requestCount, int blockedCount)
        {
            if (requestCount <= 0 && blockedCount <= 0) return "";

            var sb = new System.Text.StringBuilder();
            if (directCount > 0) sb.Append($"直接命令 {directCount} 隊");
            if (requestCount > 0)
            {
                if (sb.Length > 0) sb.Append("／");
                sb.Append($"指揮系統外 {requestCount} 隊は支援要請");
            }
            if (blockedCount > 0)
            {
                if (sb.Length > 0) sb.Append("／");
                sb.Append($"命令できない {blockedCount} 隊");
            }
            return sb.ToString();
        }

        /// <summary>その部隊に命令できない理由（直接命令できるときは空文字）。</summary>
        public static string ReasonText(BattleCommandRight right, string targetName)
        {
            string who = string.IsNullOrEmpty(targetName) ? "その部隊" : targetName;
            switch (right)
            {
                case BattleCommandRight.直接命令: return "";
                case BattleCommandRight.要請: return $"{who} は指揮系統外です（支援要請になります）";
                default: return $"{who} には命令できません";
            }
        }

        /// <summary>
        /// 要請したときの1行（実際に相手が応じるかは相手しだい＝<b>命令ではない</b>と分かる言い方にする）。
        /// </summary>
        public static string RequestSentText(string targetName, string orderName)
        {
            string who = string.IsNullOrEmpty(targetName) ? "指揮系統外の部隊" : targetName;
            string what = string.IsNullOrEmpty(orderName) ? "支援" : orderName;
            return $"{who} へ{what}を要請しました（応じるかは相手の判断）";
        }
    }
}
