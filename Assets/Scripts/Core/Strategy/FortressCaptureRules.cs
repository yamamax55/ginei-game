using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 回廊要塞の<b>制御状態</b>（#40）。要塞は「基本は占領」＝通常の会戦では所属が変わるだけで、
    /// 施設そのものは残る。破壊は特殊手段でのみ到達しうる別状態として分けて持つ。
    /// </summary>
    public enum FortressControl
    {
        /// <summary>守備が生きている＝回廊を扼している（封鎖中）。</summary>
        守備健在 = 0,
        /// <summary>守備（施設の砲台・中枢・守備艦隊）が沈黙した。<b>施設は健在</b>＝まだ誰のものでもない。</summary>
        守備制圧 = 1,
        /// <summary>攻撃側が制圧線まで到達し所属が移った＝<b>占領</b>。施設は無傷で引き継がれる。</summary>
        占領 = 2,
        /// <summary>施設そのものが失われた＝<b>破壊</b>。特殊手段でしか到達しない（通常戦闘では起きない）。</summary>
        破壊 = 3,
    }

    /// <summary>
    /// 要塞へ与えられたダメージの出所。通常戦闘（艦砲・守備艦隊の掃討）では施設は壊れない。
    /// <see cref="FortressDamageSource.特殊破壊"/> は<b>明示的な特殊アクション専用</b>の入口で、
    /// 現状ゲーム内に到達経路は用意していない（＝要塞は通常手段では破壊されない）。
    /// </summary>
    public enum FortressDamageSource
    {
        /// <summary>艦砲・砲台戦・守備艦隊の掃討など通常の会戦。施設は破壊されない。</summary>
        通常戦闘 = 0,
        /// <summary>明示的な特殊破壊アクション（未提供）。これだけが施設を失わせうる。</summary>
        特殊破壊 = 1,
    }

    /// <summary>
    /// 会戦中の要塞の状況スナップショット（純データ）。施設本体（コア・砲台）と守備艦隊を
    /// <b>別々の数</b>で持つ＝「守備艦隊を全滅させた」と「施設を落とした」を取り違えない。
    /// </summary>
    public readonly struct FortressSiegeState
    {
        /// <summary>施設中枢の残耐久（0以下＝中枢沈黙。施設が消えるという意味ではない）。</summary>
        public readonly int coreStrength;
        /// <summary>稼働中の砲台数（0＝応射なし）。</summary>
        public readonly int activeTurrets;
        /// <summary>要塞側で生存している守備<b>艦隊</b>の数（施設とは別勘定）。</summary>
        public readonly int garrisonFleets;
        /// <summary>攻撃側が制圧線（守備側の出口）へ到達しているか。</summary>
        public readonly bool attackerAtControlLine;
        /// <summary>施設が特殊手段で破壊されているか。</summary>
        public readonly bool facilityDestroyed;

        public FortressSiegeState(int coreStrength, int activeTurrets, int garrisonFleets,
                                  bool attackerAtControlLine, bool facilityDestroyed = false)
        {
            this.coreStrength = coreStrength;
            this.activeTurrets = activeTurrets;
            this.garrisonFleets = garrisonFleets;
            this.attackerAtControlLine = attackerAtControlLine;
            this.facilityDestroyed = facilityDestroyed;
        }
    }

    /// <summary>占領成立の条件をどこまで厳しくするか。</summary>
    public readonly struct FortressCaptureParams
    {
        /// <summary>守備<b>艦隊</b>まで排除しないと占領を認めないか（true＝厳格・既定）。</summary>
        public readonly bool requireGarrisonFleetsCleared;

        public FortressCaptureParams(bool requireGarrisonFleetsCleared)
        {
            this.requireGarrisonFleetsCleared = requireGarrisonFleetsCleared;
        }

        /// <summary>既定＝施設の守備が沈黙し、守備艦隊も残っていないことを求める。</summary>
        public static FortressCaptureParams Default => new FortressCaptureParams(true);
    }

    /// <summary>
    /// 回廊要塞の<b>占領（制圧）</b>と<b>破壊</b>を切り分ける純ロジック（#40）。
    ///
    /// 方針＝<b>基本は占領</b>：通常の会戦で要塞が失われることは無い。守備（施設の中枢・砲台・守備艦隊）を
    /// 沈黙させ、そのうえで攻撃側が制圧線へ到達したときに<b>所属だけ</b>が移る。施設・モデル・名前は残る。
    /// 破壊は明示的な特殊手段（<see cref="FortressDamageSource.特殊破壊"/>）でしか到達しない別状態で、
    /// 通常戦闘のHP0とは完全に切り離す。
    ///
    /// 既存判定は<b>再実装せず委譲</b>する：
    /// - 通行可否＝<see cref="StrategyRules.IsFortressBlocked"/>／<see cref="FortressRules.BlocksPassage"/>
    /// - 戦略側の守備置き直し＝<see cref="FortressBlockadeRules.Regarrison"/>
    /// - 力攻めの自動解決＝<see cref="StrategyRules.AssaultFortress"/>
    /// ここが足すのは「守備を潰しただけ」と「占領した」を分ける条件と、その表示文言だけ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FortressCaptureRules
    {
        /// <summary>通常戦闘で施設が破壊されうるか＝<b>常に false</b>（基本は占領）。</summary>
        public const bool DestructibleByNormalAttack = false;

        /// <summary>施設の守備（中枢＋砲台）が沈黙したか。守備艦隊は別勘定（ここでは見ない）。</summary>
        public static bool IsFacilityGarrisonSilenced(in FortressSiegeState s)
            => s.coreStrength <= 0 && s.activeTurrets <= 0;

        /// <summary>
        /// 「守備が尽きた」か＝施設の守備が沈黙し、（厳格設定なら）守備艦隊も残っていない。
        /// これは<b>占領の前提</b>であって、占領そのものではない（制圧線への到達が別に要る）。
        /// </summary>
        public static bool IsGarrisonSuppressed(in FortressSiegeState s, in FortressCaptureParams p)
        {
            if (!IsFacilityGarrisonSilenced(s)) return false;
            if (p.requireGarrisonFleetsCleared && s.garrisonFleets > 0) return false;
            return true;
        }

        public static bool IsGarrisonSuppressed(in FortressSiegeState s)
            => IsGarrisonSuppressed(s, FortressCaptureParams.Default);

        /// <summary>
        /// 占領が成立するか＝<b>守備が尽きた状態で攻撃側が制圧線へ到達</b>している。
        /// 施設が破壊されている場合は占領する対象が無いので false。
        /// </summary>
        public static bool CanCapture(in FortressSiegeState s, in FortressCaptureParams p)
            => !s.facilityDestroyed && IsGarrisonSuppressed(s, p) && s.attackerAtControlLine;

        public static bool CanCapture(in FortressSiegeState s) => CanCapture(s, FortressCaptureParams.Default);

        /// <summary>現在の制御状態を判定する（破壊＞占領＞守備制圧＞守備健在の順に強い）。</summary>
        public static FortressControl Resolve(in FortressSiegeState s, in FortressCaptureParams p)
        {
            if (s.facilityDestroyed) return FortressControl.破壊;
            if (CanCapture(s, p)) return FortressControl.占領;
            if (IsGarrisonSuppressed(s, p)) return FortressControl.守備制圧;
            return FortressControl.守備健在;
        }

        public static FortressControl Resolve(in FortressSiegeState s) => Resolve(s, FortressCaptureParams.Default);

        /// <summary>この出所のダメージで施設を失わせてよいか＝特殊破壊だけが true。</summary>
        public static bool AllowsDestruction(FortressDamageSource source)
            => source == FortressDamageSource.特殊破壊;

        /// <summary>施設が回廊を扼し続けるか＝守備が健在なあいだだけ。</summary>
        public static bool BlocksPassage(FortressControl control) => control == FortressControl.守備健在;

        /// <summary>所属（所有勢力）が移るか＝占領のときだけ。守備を潰しただけでは移らない。</summary>
        public static bool TransfersOwnership(FortressControl control) => control == FortressControl.占領;

        /// <summary>施設の実体（モデル・名前）が盤面に残るか＝破壊以外は残る。</summary>
        public static bool FacilitySurvives(FortressControl control) => control != FortressControl.破壊;

        /// <summary>
        /// 状態を短い日本語で言い表す（表示の単一窓口）。<b>守備を撃破しただけの状態を
        /// 「陥落」「破壊」と呼ばない</b>ための出し分けをここで固定する。
        /// </summary>
        public static string DescribeControl(FortressControl control, string fortressName, string attackerName = null)
        {
            string n = string.IsNullOrEmpty(fortressName) ? "要塞" : fortressName;
            switch (control)
            {
                case FortressControl.守備制圧:
                    return $"{n} の守備を制圧した（施設は健在・占領には制圧線への到達が必要）";
                case FortressControl.占領:
                    return string.IsNullOrEmpty(attackerName)
                        ? $"{n} を占領した（施設は無傷のまま接収）"
                        : $"{attackerName} 軍が {n} を占領した（施設は無傷のまま接収）";
                case FortressControl.破壊:
                    return $"{n} は特殊手段により破壊された";
                default:
                    return $"{n} は持ちこたえている（回廊の封鎖は続く）";
            }
        }

        /// <summary>
        /// 戦略側の要塞データへ制御状態を書き戻す（統合の単一窓口）。
        /// 占領＝<see cref="FortressBlockadeRules.Regarrison"/> へ委譲して所有移転＋守備残置。
        /// 守備制圧＝所有は動かさず守備0・封鎖解除だけ（「落としたが取っていない」を表す）。
        /// 破壊＝回廊から要塞そのものを外すのは呼び出し側（<see cref="Corridor.fortress"/> の付け外し）に任せ、
        /// ここでは守備0・封鎖解除まで行う（Core は参照を壊さない）。
        /// 変更したら true。
        /// </summary>
        public static bool ApplyToStrategy(Fortress f, FortressControl control, Faction attacker,
                                           float garrison, float shieldIntegrity = 1f)
        {
            if (f == null) return false;
            switch (control)
            {
                case FortressControl.占領:
                    FortressBlockadeRules.Regarrison(f, attacker, Mathf.Max(0f, garrison), shieldIntegrity);
                    return true;
                case FortressControl.守備制圧:
                case FortressControl.破壊:
                    f.garrisonStrength = 0f;
                    f.controlsCorridor = false;
                    return true;
                default:
                    return false;
            }
        }
    }
}
