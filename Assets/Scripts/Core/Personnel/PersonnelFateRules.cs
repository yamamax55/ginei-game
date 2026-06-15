using UnityEngine;

namespace Ginei
{
    /// <summary>艦隊壊滅時の在席軍人の運命（LIFE-4 #154 拡張）。基本は<b>未配属</b>（生還して部隊を失うだけ）、稀に
    /// 捕虜/戦死/行方不明。捕虜のその後は <see cref="CaptivityRules"/>（登用/処断/解放→在野）。</summary>
    public enum PersonFate { 未配属, 捕虜, 戦死, 行方不明 }

    /// <summary>
    /// 艦隊壊滅 → 在席軍人（司令/副提督/参謀）の処遇を決める純ロジック（唯一の窓口・死亡#152/捕虜#154 と接続）。
    /// 基本は生還して<b>未配属</b>（席を失うだけ）。稀に<b>捕虜</b>（敵勢力が拘留＝後で登用/処断/解放）・<b>戦死</b>
    /// （死亡プール＝<see cref="LifecycleRules.Kill"/>）・<b>行方不明</b>（消息不明＝稀にイベント復帰・後段）。
    /// 乱数は呼び出し側が roll(0..1) を渡す＝決定論的にテストできる（<see cref="CaptivityRules"/> と同流儀）。test-first。
    /// </summary>
    public static class PersonnelFateRules
    {
        /// <summary>帯境界比較の浮動小数許容（実 Unity と dotnet スタブの 1ULP 差を吸収）。</summary>
        private const float BoundaryEpsilon = 1e-4f;

        /// <summary>運命確率（捕虜/戦死/行方不明・残りが未配属）。合計&lt;=1 を想定（超過分は行方不明へ寄る）。</summary>
        public readonly struct FateOdds
        {
            public readonly float captured;  // 捕虜
            public readonly float killed;    // 戦死
            public readonly float missing;   // 行方不明

            public FateOdds(float captured, float killed, float missing)
            {
                this.captured = Mathf.Clamp01(captured);
                this.killed = Mathf.Clamp01(killed);
                this.missing = Mathf.Clamp01(missing);
            }

            /// <summary>既定＝捕虜0.12 / 戦死0.10 / 行方不明0.08 → 未配属0.70（基本は生還・喪失事象は稀）。</summary>
            public static FateOdds Default => new FateOdds(0.12f, 0.10f, 0.08f);

            /// <summary>未配属の確率＝1−（捕虜+戦死+行方不明）（下限0）。</summary>
            public float Unassigned => Mathf.Max(0f, 1f - captured - killed - missing);
        }

        /// <summary>
        /// roll(0..1) を [未配属 | 捕虜 | 戦死 | 行方不明] の帯へ割り当てて運命を決める（決定論）。未配属を先頭の最も広い帯に置き、
        /// 稀な事象は後ろの細い帯。合計&gt;1 の異常入力は末尾の行方不明が受ける。
        /// </summary>
        public static PersonFate ResolveFate(float roll, FateOdds odds)
        {
            float r = Mathf.Clamp01(roll);
            float u = odds.Unassigned;
            // 「ちょうど境界は次の帯」を浮動小数で確実にする。帯境界の累積和は実 Unity と dotnet
            // スタブで 1ULP ずれうる（例: 0.6+0.2+0.1 vs 0.9f）ので、各閾値から小さな許容を引いて
            // 境界ちょうどの roll が下の帯へ取りこぼされないようにする。
            if (r < u - BoundaryEpsilon) return PersonFate.未配属;
            if (r < u + odds.captured - BoundaryEpsilon) return PersonFate.捕虜;
            if (r < u + odds.captured + odds.killed - BoundaryEpsilon) return PersonFate.戦死;
            return PersonFate.行方不明;
        }

        /// <summary>
        /// 状況から運命確率を導く：包囲（ZOC #81）で苛烈化し捕虜/戦死/行方不明が上がる。指揮・士気が高いほど
        /// 逃げ切って未配属に寄る（<see cref="CaptivityRules.CaptureChance"/> と同じ escapeSkill）。係数 #106 想定。
        /// </summary>
        public static FateOdds OddsFromContext(bool encircled, float commandFactor, float moraleFactor)
        {
            float escape = Mathf.Clamp01(Mathf.Clamp01(commandFactor) * 0.5f + Mathf.Clamp01(moraleFactor) * 0.3f);
            float severity = (encircled ? 1.5f : 1.0f) * (1f - escape); // 包囲＝苛烈／指揮・士気で生還（未配属↑）
            var d = FateOdds.Default;
            return new FateOdds(d.captured * severity, d.killed * severity, d.missing * severity);
        }

        /// <summary>
        /// 運命を Person へ適用する（決定論的に状態遷移）。未配属＝状態不変（艦隊からの離脱＝席外しは呼び出し側
        /// <see cref="FleetRoster.Unassign"/> 等）／捕虜＝<see cref="CaptivityRules.Capture"/>／戦死＝
        /// <see cref="LifecycleRules.Kill"/>（死亡プール入り）／行方不明＝行方不明状態（復帰の余地・後段）。
        /// 適用できたら true（故人・既に拘留中などで不能なら false）。
        /// </summary>
        public static bool Apply(Person person, PersonFate fate, Faction victor, int year)
        {
            if (person == null || person.IsDeceased) return false;
            switch (fate)
            {
                case PersonFate.未配属: return true; // 生還＝未配属（席は呼び出し側で外す）
                case PersonFate.捕虜: return CaptivityRules.Capture(person, victor, year);
                case PersonFate.戦死: return LifecycleRules.Kill(person, year);
                case PersonFate.行方不明:
                    if (person.captiveStatus != CaptiveStatus.自由) return false;
                    person.captiveStatus = CaptiveStatus.行方不明;
                    return true;
                default: return false;
            }
        }

        /// <summary>roll で運命を決めて適用する便利窓口（決めた運命を返す）。</summary>
        public static PersonFate ResolveAndApply(Person person, float roll, FateOdds odds, Faction victor, int year)
        {
            PersonFate fate = ResolveFate(roll, odds);
            Apply(person, fate, victor, year);
            return fate;
        }

        /// <summary>行方不明から復帰する（行方不明→自由）。MIA 復帰イベント（後段・#116）の窓口。行方不明でなければ false。</summary>
        public static bool ReturnFromMissing(Person person)
        {
            if (person == null || person.captiveStatus != CaptiveStatus.行方不明) return false;
            person.captiveStatus = CaptiveStatus.自由;
            return true;
        }
    }
}
