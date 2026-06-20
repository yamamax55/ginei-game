namespace Ginei
{
    /// <summary>岐路（<see cref="CareerFork"/>）の実行を委ねる先のマクロ窓口（TKO-7・#2484）。実体の数値・状態遷移は各窓口へ委譲する。</summary>
    public enum ForkExecutor
    {
        なし,        // 忠勤＝継続（実行不要）
        退役,        // 下野＝軍を辞す（退役 #530）
        亡命受入,    // 亡命＝敵勢力へ（DiplomacyRules 受入＋BattleAllegianceRules 持ち出し）
        独立樹立,    // 独立＝旗揚げ（CivilWarRules 新勢力生成）
        政界入り,    // 政界転身＝政治家提督（PoliticianRules/政党 #159）
    }

    /// <summary>
    /// 岐路の<b>実行の振り分け</b>の純ロジック（TKO-7・#2484・唯一の窓口）。選んだ <see cref="CareerFork"/> を、実体を担う
    /// 既存マクロ窓口（退役#530／`DiplomacyRules`+`BattleAllegianceRules`／`CivilWarRules`／`PoliticianRules`+政党#159）へ
    /// 振り分け（<see cref="ExecutorFor"/>）、その<b>前提が満たされているか</b>を判定する（<see cref="CanExecute"/>）。
    /// 数値・状態遷移は各窓口へ委譲＝本ルールは振り分けと前提のみ（並行系を作らない＝additive）。<see cref="CareerForkRules"/>
    /// が「開かれているか」を、本ルールが「実行できるか・どこへ」を担う。決定論・test-first。Game 層が薄いアダプタで配線する。
    /// </summary>
    public static class CareerForkExecutionRules
    {
        /// <summary>岐路の実行を委ねる先のマクロ窓口。</summary>
        public static ForkExecutor ExecutorFor(CareerFork f)
        {
            switch (f)
            {
                case CareerFork.下野:     return ForkExecutor.退役;
                case CareerFork.亡命:     return ForkExecutor.亡命受入;
                case CareerFork.独立:     return ForkExecutor.独立樹立;
                case CareerFork.政界転身: return ForkExecutor.政界入り;
                default:                  return ForkExecutor.なし; // 忠勤
            }
        }

        /// <summary>
        /// その岐路を実際に実行できるか＝前提の充足。忠勤/下野は常に可（留まる/辞すは常に選べる）。
        /// 亡命＝迎えてくれる敵勢力（<paramref name="hasEnemyHaven"/>）が要る／独立＝旗揚げの拠点・stature
        /// （<paramref name="hasIndependenceBase"/>）が要る／政界転身＝政界の受け皿（政党/文民役職の空き＝<paramref name="hasPoliticalOpening"/>）が要る。
        /// 充足の有無は Game 層が既存マクロ（外交/内戦/政党）から供給する。
        /// </summary>
        public static bool CanExecute(CareerFork f, bool hasEnemyHaven, bool hasIndependenceBase, bool hasPoliticalOpening)
        {
            switch (f)
            {
                case CareerFork.忠勤: return true;
                case CareerFork.下野: return true;
                case CareerFork.亡命: return hasEnemyHaven;
                case CareerFork.独立: return hasIndependenceBase;
                case CareerFork.政界転身: return hasPoliticalOpening;
                default: return false;
            }
        }
    }
}
