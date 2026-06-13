namespace Ginei
{
    /// <summary>
    /// 勢力の政治を1年ぶん回す純ロジック（政党システムの配線オーケストレータ・GOV-6 #159）。
    /// 既存の純ロジック（<see cref="PartySystemRules"/>＝二大政党への収束/分断、<see cref="ElectionScheduleRules"/>＝衆参の選挙日程）を
    /// <b>勢力の年次 Tick として束ねる</b>（数値は委譲し二重実装しない）。<see cref="FactionState.politics"/> を更新し、起きた出来事を返す。
    /// GalaxyView の年次 Tick から呼ぶ想定（民主政治のみ＝<see cref="ElectoralSystemRules.IsElectoral"/>）。決定論・test-first。
    /// </summary>
    public static class PoliticsTickRules
    {
        /// <summary>政治年次 Tick の調整係数。</summary>
        public readonly struct PoliticsParams
        {
            /// <summary>二大政党化の進む速さ/年（<see cref="PartySystemRules.TickConsolidation"/> の rate）。</summary>
            public readonly float consolidationRate;
            /// <summary>分断危機とみなす分極化のしきい値。</summary>
            public readonly float crisisThreshold;

            public PoliticsParams(float consolidationRate, float crisisThreshold)
            {
                this.consolidationRate = consolidationRate;
                this.crisisThreshold = crisisThreshold;
            }

            /// <summary>既定＝ゆっくり収束（0.15/年）・分断危機は <see cref="PartySystemRules.DefaultCrisisThreshold"/>。</summary>
            public static PoliticsParams Default => new PoliticsParams(0.15f, PartySystemRules.DefaultCrisisThreshold);
        }

        /// <summary>1年の政治 Tick で起きた出来事。</summary>
        public struct PoliticsTickResult
        {
            public bool lowerHouseElection;   // 下院（衆議院相当）の選挙が実施された
            public bool upperHouseElection;   // 上院（参議院相当）の選挙が実施された
            public bool dividedCrisis;        // 分断危機の状態
            public bool dividedCrisisOnset;   // この年に分断危機へ突入した（立ち上がり＝通知に使う）
            public float effectiveParties;    // 有効政党数
            public float maturity;            // 民主主義の成熟度
        }

        /// <summary>二院の選挙日程が無ければ設立する（衆参を foundedYear で立ち上げ）。</summary>
        public static void EnsureChambers(PoliticsState pol, int foundedYear)
        {
            if (pol == null) return;
            if (pol.lowerHouse == null) pol.lowerHouse = ElectionScheduleRules.Found(LegislativeChamber.下院, foundedYear);
            if (pol.upperHouse == null) pol.upperHouse = ElectionScheduleRules.Found(LegislativeChamber.上院, foundedYear);
        }

        /// <summary>
        /// 勢力の政治を1年進める：成熟度に応じて政党制を二大政党へ収束させ（<see cref="PartySystemRules.TickConsolidation"/>）、
        /// 衆参の選挙日程を進め（<see cref="ElectionScheduleRules.TickYear"/>）、分断危機を判定する。<see cref="FactionState.politics"/> を生成/更新。
        /// </summary>
        public static PoliticsTickResult TickYear(FactionState s, int currentYear, PoliticsParams prm)
        {
            var r = default(PoliticsTickResult);
            if (s == null) return r;

            PoliticsState pol = s.politics;
            if (pol == null) { pol = new PoliticsState(); s.politics = pol; }
            EnsureChambers(pol, currentYear);

            float maturity = PartySystemRules.MaturityFrom(s);
            r.maturity = maturity;

            // 政党制の収束（成熟度が上がるほど二大政党へ＝デュヴェルジェ）。
            PartySystemRules.TickConsolidation(pol.parties, maturity, prm.consolidationRate);
            r.effectiveParties = PartySystemRules.EffectiveNumberOfParties(pol.parties);

            // 選挙日程（衆＝任期4年/解散・参＝6年で半数改選）。
            r.lowerHouseElection = ElectionScheduleRules.TickYear(pol.lowerHouse, currentYear);
            r.upperHouseElection = ElectionScheduleRules.TickYear(pol.upperHouse, currentYear);

            // 分断危機（二大政党で成熟するほど高い）。立ち上がり（false→true）を検出して通知に使う。
            r.dividedCrisis = PartySystemRules.IsDividedCrisis(maturity, r.effectiveParties, prm.crisisThreshold);
            r.dividedCrisisOnset = r.dividedCrisis && !pol.dividedCrisisActive;
            pol.dividedCrisisActive = r.dividedCrisis;

            return r;
        }

        public static PoliticsTickResult TickYear(FactionState s, int currentYear)
            => TickYear(s, currentYear, PoliticsParams.Default);
    }
}
