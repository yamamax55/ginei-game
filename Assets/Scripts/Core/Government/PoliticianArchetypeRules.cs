using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 政治家の類型（archetype・政治家システム基盤 #159 GOV-6 の拡張）。
    /// <see cref="PoliticianProfile"/> の素養（人気/弁舌/党内基盤/清廉さ）から<b>導出</b>される行動類型＝
    /// その政治家が「どう票を取り・どう連立に振る舞うか」の人物像。生データに新フィールドを足さず、
    /// 既存の素養から決定論で算出する（実効値パターン・基準フィールド非破壊）。
    /// </summary>
    /// <remarks>
    /// ウェーバーの倫理類型（<see cref="PoliticalEthicsType"/>）・召命/生業（<see cref="VocationOrientation"/>）は
    /// 直交する別軸＝あれは「政治への向き合い方」、こちらは「票の取り方・連立への振る舞い」を素養から読む。
    /// </remarks>
    public enum PoliticianArchetype
    {
        /// <summary>ポピュリスト＝大衆人気と弁舌で民意を直接動かす（党内基盤は弱い・大衆動員型）。</summary>
        ポピュリスト,
        /// <summary>技術官僚＝清廉で実務的だが大衆訴求は弱い（テクノクラート・調整に長ける）。</summary>
        技術官僚,
        /// <summary>改革者＝清廉さと人気を併せ持ち既得の党内基盤に頼らない（しがらみが薄い）。</summary>
        改革者,
        /// <summary>守旧派＝厚い党内基盤で議員票を束ねるが清廉さ/人気は乏しい（派閥のボス・オールドガード）。</summary>
        守旧派,
        /// <summary>折衷＝どの素養も突出せず、平均的に立ち回る（バランス型）。</summary>
        折衷,
    }

    /// <summary>
    /// 政治家類型の純ロジック（政治家システム基盤 #159 GOV-6 の拡張）。
    /// <see cref="PoliticianProfile"/> の素養から類型（<see cref="PoliticianArchetype"/>）を分類し、
    /// その類型から<b>選挙訴求の補正</b>と<b>連立への協調性</b>を導く唯一の窓口。
    /// 素養そのもの（実効人気/党員票/議員票/選挙力）の算出は <see cref="PoliticianRules"/> へ委譲する（二重実装しない）。
    /// </summary>
    /// <remarks>
    /// すべて pure/static/決定論/null 安全。調整値は const/<see cref="PoliticianArchetypeParams"/> に集約。
    /// 盤面/UI 配線は後段（<c>GalaxyView</c> の政治年次 Tick＝<c>RunPoliticsTick</c> で類型を読み訴求/連立に乗せる）。
    /// </remarks>
    public static class PoliticianArchetypeRules
    {
        /// <summary>素養を「高い」と弁別する 0..1 軸の既定しきい値。</summary>
        public const float HighThreshold = 0.6f;
        /// <summary>素養を「低い」とみなす 0..1 軸の既定しきい値。</summary>
        public const float LowThreshold = 0.4f;

        /// <summary>能力値（0..100）を 0..1 へ正規化。</summary>
        static float Norm(int stat) => Mathf.Clamp01(stat / PoliticianRules.MaxStat);

        /// <summary>大衆訴求の強さ（0..1）＝党員票への訴求（人気×弁舌・スキャンダル反映）。<see cref="PoliticianRules"/> へ委譲。</summary>
        public static float MassAppeal(PoliticianProfile pol)
            => PoliticianRules.MemberVoteAppeal(pol);

        /// <summary>党内掌握の強さ（0..1）＝議員票への訴求（党内基盤×清廉さ）。<see cref="PoliticianRules"/> へ委譲。</summary>
        public static float InsiderGrip(PoliticianProfile pol)
            => PoliticianRules.LegislatorVoteAppeal(pol);

        /// <summary>清廉さ（0..1・<see cref="PoliticianProfile.integrity"/> の正規化）。</summary>
        public static float CleanScore(PoliticianProfile pol)
            => pol == null ? 0f : Norm(pol.integrity);

        /// <summary>
        /// 素養から政治家の類型を分類する（決定論・null は <see cref="PoliticianArchetype.折衷"/>）。
        /// 大衆訴求（<see cref="MassAppeal"/>）・党内掌握（<see cref="InsiderGrip"/>）・清廉さ（<see cref="CleanScore"/>）の高低で弁別：
        /// 改革者（清廉×大衆高×党内低）／技術官僚（清廉×大衆低）／ポピュリスト（大衆高×党内低）／守旧派（党内高×大衆低×不浄）／他＝折衷。
        /// </summary>
        public static PoliticianArchetype Classify(PoliticianProfile pol, PoliticianArchetypeParams prm)
        {
            if (pol == null) return PoliticianArchetype.折衷;

            float mass = MassAppeal(pol);
            float insider = InsiderGrip(pol);
            float clean = CleanScore(pol);

            bool massHigh = mass >= prm.highThreshold;
            bool massLow = mass <= prm.lowThreshold;
            bool insiderHigh = insider >= prm.highThreshold;
            bool insiderLow = insider <= prm.lowThreshold;
            bool cleanHigh = clean >= prm.highThreshold;

            // 改革者：清廉で人気もあるが党内基盤に頼らない（しがらみが薄い）。
            if (cleanHigh && massHigh && insiderLow) return PoliticianArchetype.改革者;
            // 技術官僚：清廉・実務的だが大衆訴求は弱い。
            if (cleanHigh && massLow) return PoliticianArchetype.技術官僚;
            // ポピュリスト：大衆人気が突出し党内基盤は弱い（清廉さは問わない）。
            if (massHigh && insiderLow) return PoliticianArchetype.ポピュリスト;
            // 守旧派：厚い党内基盤で議員票を束ねるが清廉さ/人気に乏しい。
            if (insiderHigh && massLow && !cleanHigh) return PoliticianArchetype.守旧派;
            return PoliticianArchetype.折衷;
        }

        /// <summary>既定パラメータで分類。</summary>
        public static PoliticianArchetype Classify(PoliticianProfile pol)
            => Classify(pol, PoliticianArchetypeParams.Default);

        /// <summary>
        /// その類型が大衆票（党員票）に対して持つ訴求補正（倍率・1.0 が中立）。
        /// ポピュリスト＝大衆票に強い、守旧派＝大衆票に弱い、他＝中立寄り。
        /// <see cref="PoliticianRules.MemberVoteAppeal"/> に掛けて使う（基準値非破壊）。
        /// </summary>
        public static float MassVoteModifier(PoliticianArchetype a, PoliticianArchetypeParams prm)
        {
            switch (a)
            {
                case PoliticianArchetype.ポピュリスト: return 1f + prm.appealSwing;
                case PoliticianArchetype.改革者: return 1f + 0.5f * prm.appealSwing;
                case PoliticianArchetype.守旧派: return 1f - prm.appealSwing;
                case PoliticianArchetype.技術官僚: return 1f - 0.5f * prm.appealSwing;
                default: return 1f;
            }
        }

        public static float MassVoteModifier(PoliticianArchetype a)
            => MassVoteModifier(a, PoliticianArchetypeParams.Default);

        /// <summary>
        /// その類型が党内票（議員票）に対して持つ訴求補正（倍率・1.0 が中立）。
        /// 守旧派＝議員票に強い、ポピュリスト＝議員票に弱い（党内に基盤を持たない）。
        /// <see cref="PoliticianRules.LegislatorVoteAppeal"/> に掛けて使う（基準値非破壊）。
        /// </summary>
        public static float InsiderVoteModifier(PoliticianArchetype a, PoliticianArchetypeParams prm)
        {
            switch (a)
            {
                case PoliticianArchetype.守旧派: return 1f + prm.appealSwing;
                case PoliticianArchetype.技術官僚: return 1f + 0.5f * prm.appealSwing;
                case PoliticianArchetype.ポピュリスト: return 1f - prm.appealSwing;
                case PoliticianArchetype.改革者: return 1f - 0.5f * prm.appealSwing;
                default: return 1f;
            }
        }

        public static float InsiderVoteModifier(PoliticianArchetype a)
            => InsiderVoteModifier(a, PoliticianArchetypeParams.Default);

        /// <summary>
        /// 連立交渉での協調性（0..1・高いほど他党と組みやすい）。
        /// 技術官僚＝調整役で最も協調的、守旧派＝党益優先でやや協調、改革者/折衷＝中庸、ポピュリスト＝独善で非協調。
        /// 連立形成の解決（思想距離/結束）は <see cref="CoalitionRules"/> が窓口＝これは「組みやすさ」の人物バイアスを与えるだけ。
        /// </summary>
        public static float CoalitionCooperativeness(PoliticianArchetype a, PoliticianArchetypeParams prm)
        {
            float baseCoop;
            switch (a)
            {
                case PoliticianArchetype.技術官僚: baseCoop = prm.cooperBase + prm.cooperSwing; break;
                case PoliticianArchetype.守旧派: baseCoop = prm.cooperBase + 0.5f * prm.cooperSwing; break;
                case PoliticianArchetype.改革者: baseCoop = prm.cooperBase; break;
                case PoliticianArchetype.折衷: baseCoop = prm.cooperBase; break;
                case PoliticianArchetype.ポピュリスト: baseCoop = prm.cooperBase - prm.cooperSwing; break;
                default: baseCoop = prm.cooperBase; break;
            }
            return Mathf.Clamp01(baseCoop);
        }

        public static float CoalitionCooperativeness(PoliticianArchetype a)
            => CoalitionCooperativeness(a, PoliticianArchetypeParams.Default);

        /// <summary>政治家から直接、連立協調性を求める便宜版（分類してから補正を返す）。</summary>
        public static float CoalitionCooperativeness(PoliticianProfile pol, PoliticianArchetypeParams prm)
            => CoalitionCooperativeness(Classify(pol, prm), prm);

        /// <summary>
        /// 類型補正を反映した実効選挙力（0..1）＝党員票×大衆補正と議員票×党内補正を同等合成。
        /// <see cref="PoliticianRules.ElectoralStrength"/>（無補正）の類型反映版＝素養算出は委譲し補正だけ乗せる（基準値非破壊）。
        /// </summary>
        public static float ArchetypeElectoralStrength(PoliticianProfile pol, PoliticianArchetypeParams prm)
        {
            if (pol == null) return 0f;
            var a = Classify(pol, prm);
            float member = Mathf.Clamp01(PoliticianRules.MemberVoteAppeal(pol) * MassVoteModifier(a, prm));
            float legis = Mathf.Clamp01(PoliticianRules.LegislatorVoteAppeal(pol) * InsiderVoteModifier(a, prm));
            return Mathf.Clamp01(0.5f * member + 0.5f * legis);
        }

        public static float ArchetypeElectoralStrength(PoliticianProfile pol)
            => ArchetypeElectoralStrength(pol, PoliticianArchetypeParams.Default);

        /// <summary>類型分類と訴求/連立補正の調整係数。</summary>
        public readonly struct PoliticianArchetypeParams
        {
            /// <summary>素養を「高い」とみなすしきい値（0..1）。</summary>
            public readonly float highThreshold;
            /// <summary>素養を「低い」とみなすしきい値（0..1）。</summary>
            public readonly float lowThreshold;
            /// <summary>類型による票訴求の振れ幅（倍率の片振り＝ポピュリストの大衆票に +この値）。</summary>
            public readonly float appealSwing;
            /// <summary>連立協調性の基準値（折衷/改革者の既定）。</summary>
            public readonly float cooperBase;
            /// <summary>連立協調性の類型による振れ幅。</summary>
            public readonly float cooperSwing;

            public PoliticianArchetypeParams(float highThreshold, float lowThreshold,
                                             float appealSwing, float cooperBase, float cooperSwing)
            {
                // 低≤高 を保証（逆転入力でも壊れない）。
                float hi = Mathf.Clamp01(highThreshold);
                float lo = Mathf.Clamp01(lowThreshold);
                this.highThreshold = Mathf.Max(hi, lo);
                this.lowThreshold = Mathf.Min(hi, lo);
                this.appealSwing = Mathf.Clamp(appealSwing, 0f, 1f);
                this.cooperBase = Mathf.Clamp01(cooperBase);
                this.cooperSwing = Mathf.Clamp(cooperSwing, 0f, 1f);
            }

            /// <summary>既定＝高0.6/低0.4・訴求振れ0.3・協調基準0.5・協調振れ0.3。</summary>
            public static PoliticianArchetypeParams Default
                => new PoliticianArchetypeParams(HighThreshold, LowThreshold, 0.3f, 0.5f, 0.3f);
        }
    }
}
