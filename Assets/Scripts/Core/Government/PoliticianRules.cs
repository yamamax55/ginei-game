using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 政治家システムの純ロジック（政治家システム基盤・#159 GOV-6 / 目安箱 政治家箱 #1296）。
    /// 政治家（<see cref="PoliticianProfile"/>）が<b>どう権力を得るか（票・選挙）</b>と<b>どう決裁するか（民意と票）</b>を一元化する唯一の窓口。
    /// 政治家は軍才/文才でなく<b>民意と票で生き死にする</b>（game-introduction：国王は大局と正統性で／政治家は民意と票で／貴族は領地の利益で判断）。
    /// </summary>
    /// <remarks>
    /// <b>選挙の解決は <see cref="LeadershipElectionRules"/> に委譲</b>する（<see cref="ToCandidate"/> で橋渡し＝二重実装しない）。
    /// 党/派閥は <see cref="Party"/>/<see cref="PartyRules"/>、役職資格は <see cref="OfficeRules"/>、職分は <see cref="PersonVocationRules"/> が窓口。
    /// すべて実効値パターン（基準フィールド非破壊）・決定論。test-first（盤面/UI 配線は後段＝後方互換）。
    /// </remarks>
    public static class PoliticianRules
    {
        /// <summary>能力値の上限（<see cref="Person"/> と同枠）。</summary>
        public const float MaxStat = 100f;

        /// <summary>人気が薄れていく中庸値（昨日の英雄も忘れられる）。</summary>
        public const float NeutralPopularity = 0.5f;

        /// <summary>決裁基準の比重＝民意（公共の支持）。<see cref="VoteWeight"/> と合わせて 1.0。</summary>
        public const float PublicOpinionWeight = 0.5f;
        /// <summary>決裁基準の比重＝票（自分の票につながる利得）。</summary>
        public const float VoteWeight = 0.5f;

        /// <summary>スキャンダルが実効人気を削る最大割合（累積1.0で半減）。</summary>
        public const float ScandalPopularityCut = 0.5f;
        /// <summary>清廉さがスキャンダルを軽減する最大割合（清廉100で被害半減）。</summary>
        public const float IntegrityScandalResist = 0.5f;

        /// <summary>地盤に効く案件の票利得（自票に直結）。</summary>
        public const float HomeRegionVoteGain = 1f;
        /// <summary>全国区/全国案件の票利得（特定の票田なし＝民意で取る）。</summary>
        public const float NeutralVoteGain = 0.5f;
        /// <summary>他人の地盤の案件の票利得（自票に薄い）。</summary>
        public const float RivalRegionVoteGain = 0.1f;

        /// <summary>党首として現実的とみなす総合選挙力の既定しきい値。</summary>
        public const float DefaultLeaderThreshold = 0.5f;

        /// <summary>能力値（0..100）を 0..1 へ正規化。</summary>
        static float Norm(int stat) => Mathf.Clamp01(stat / MaxStat);

        /// <summary>その人物が政治家か（職分ゲート＝<see cref="PersonVocationRules"/> が唯一の窓口・null 安全）。</summary>
        public static bool IsPolitician(Person p)
            => PersonVocationRules.VocationOf(p) == PersonVocation.政治家;

        /// <summary>実効人気＝民意をスキャンダルで割り引いた値（0..1・基準 <see cref="PoliticianProfile.popularity"/> 非破壊）。</summary>
        public static float EffectivePopularity(PoliticianProfile pol)
        {
            if (pol == null) return 0f;
            return Mathf.Clamp01(pol.popularity * (1f - ScandalPopularityCut * Mathf.Clamp01(pol.scandalLevel)));
        }

        /// <summary>党員票への訴求（0..1）＝実効人気が主、弁舌で増幅（広い支持基盤）。</summary>
        public static float MemberVoteAppeal(PoliticianProfile pol)
        {
            if (pol == null) return 0f;
            return Mathf.Clamp01(EffectivePopularity(pol) * (0.7f + 0.3f * Norm(pol.oratory)));
        }

        /// <summary>議員票への訴求（0..1）＝党内基盤が主、清廉さで談合の信頼を上乗せ（GOV-7 派閥票）。</summary>
        public static float LegislatorVoteAppeal(PoliticianProfile pol)
        {
            if (pol == null) return 0f;
            return Mathf.Clamp01(Mathf.Clamp01(pol.partyStanding) * (0.8f + 0.2f * Norm(pol.integrity)));
        }

        /// <summary>総合選挙力（0..1）＝党員票/議員票を同等に合成（スキャンダル反映済み）。</summary>
        public static float ElectoralStrength(PoliticianProfile pol)
            => 0.5f * MemberVoteAppeal(pol) + 0.5f * LegislatorVoteAppeal(pol);

        /// <summary>
        /// <see cref="LeadershipElectionRules.Candidate"/> へ橋渡し（総裁選 GOV-7 #165 の解決はそちらへ委譲＝二重実装しない）。
        /// 党員票＝<see cref="MemberVoteAppeal"/>、議員票＝<see cref="LegislatorVoteAppeal"/>。
        /// </summary>
        public static LeadershipElectionRules.Candidate ToCandidate(PoliticianProfile pol)
            => new LeadershipElectionRules.Candidate(
                pol != null ? pol.personId : -1,
                MemberVoteAppeal(pol),
                LegislatorVoteAppeal(pol));

        /// <summary>地盤の有権者は票が固い＝投票率/支持の割増（地域選挙の得票に乗る・<see cref="ElectionRules"/> #選挙基盤）。</summary>
        public const float HomeTurnoutBonus = 1.3f;

        /// <summary>
        /// 一選挙区（惑星/星系）の得票見込み（民意が票になる・選挙システム基盤）。
        /// ＝有権者数 × 党員票への訴求（<see cref="MemberVoteAppeal"/>＝人気×弁舌・スキャンダル反映）×（自分の地盤なら <see cref="HomeTurnoutBonus"/>）。
        /// 領域三層の <see cref="VoteTally"/> を組む素材で、集計/集約は <see cref="ElectionRules"/> が担う（票の解決は委譲）。
        /// </summary>
        public static float RegionVotes(PoliticianProfile pol, float electorate, string regionKey)
        {
            if (pol == null || electorate <= 0f) return 0f;
            bool home = !string.IsNullOrEmpty(pol.homeRegionKey) && pol.homeRegionKey == regionKey;
            return Mathf.Max(0f, electorate * MemberVoteAppeal(pol) * (home ? HomeTurnoutBonus : 1f));
        }

        /// <summary>党首/首班候補として現実的か（総合選挙力がしきい値以上）。</summary>
        public static bool IsViableLeader(PoliticianProfile pol, float threshold)
            => ElectoralStrength(pol) >= threshold;

        public static bool IsViableLeader(PoliticianProfile pol)
            => IsViableLeader(pol, DefaultLeaderThreshold);

        /// <summary>
        /// 案件が政治家の票につながる度合い（0..1）。自分の地盤（<see cref="PoliticianProfile.homeRegionKey"/>）に効くほど高い。
        /// 全国区/全国案件は <see cref="NeutralVoteGain"/>（特定の票田を持たず民意で取る）、他人の地盤は <see cref="RivalRegionVoteGain"/>。
        /// </summary>
        public static float VoteGainForRegion(PoliticianProfile pol, string petitionRegionKey)
        {
            if (pol == null || string.IsNullOrEmpty(pol.homeRegionKey)) return NeutralVoteGain; // 全国区＝民意で取る
            if (string.IsNullOrEmpty(petitionRegionKey)) return NeutralVoteGain;                // 全国案件
            return pol.homeRegionKey == petitionRegionKey ? HomeRegionVoteGain : RivalRegionVoteGain;
        }

        /// <summary>
        /// 政治家箱の決裁傾向（0..1・高いほど通す）＝<b>民意と票</b>の加重和（#1296 game-introduction）。
        /// 国王が問う「大局・正統性」を見ない（引数に取らない）のが政治家の本質＝<b>人気のある案件と自票につながる案件を通す</b>。
        /// <paramref name="publicSupport"/>＝その案件への大衆の支持（民意 0..1）、<paramref name="voteGain"/>＝自票への利得（票 0..1）。
        /// </summary>
        public static float PetitionApproval(PoliticianProfile pol, float publicSupport, float voteGain)
        {
            float 民意 = Mathf.Clamp01(publicSupport);
            float 票 = Mathf.Clamp01(voteGain);
            return Mathf.Clamp01(PublicOpinionWeight * 民意 + VoteWeight * 票);
        }

        /// <summary>地方スコープ版＝票利得を <see cref="VoteGainForRegion"/> から導いて決裁傾向を返す。</summary>
        public static float PetitionApproval(PoliticianProfile pol, float publicSupport, string petitionRegionKey)
            => PetitionApproval(pol, publicSupport, VoteGainForRegion(pol, petitionRegionKey));

        /// <summary>
        /// スキャンダルを浴びせる（<paramref name="severity"/> 0..1）。清廉な政治家ほど被害が小さい（<see cref="IntegrityScandalResist"/>）。
        /// <see cref="PoliticianProfile.scandalLevel"/> を加算（実効人気を削る＝<see cref="EffectivePopularity"/>）。
        /// </summary>
        public static void ApplyScandal(PoliticianProfile pol, float severity)
        {
            if (pol == null || severity <= 0f) return;
            float resist = IntegrityScandalResist * Norm(pol.integrity);
            pol.scandalLevel = Mathf.Clamp01(pol.scandalLevel + Mathf.Clamp01(severity) * (1f - resist));
        }

        /// <summary>功績/失政で人気を動かす（<paramref name="magnitude"/> は負で失点・基準 popularity を直接更新）。</summary>
        public static void ApplyAchievement(PoliticianProfile pol, float magnitude)
        {
            if (pol == null) return;
            pol.popularity = Mathf.Clamp01(pol.popularity + magnitude);
        }

        /// <summary>人気の時間減衰の調整係数（年境界 Tick 用）。</summary>
        public readonly struct PoliticianParams
        {
            /// <summary>人気が中庸（<see cref="NeutralPopularity"/>）へ薄れる速度/年。</summary>
            public readonly float popularityDrift;
            /// <summary>スキャンダルが忘れられる速度/年。</summary>
            public readonly float scandalDecay;

            public PoliticianParams(float popularityDrift, float scandalDecay)
            {
                this.popularityDrift = Mathf.Max(0f, popularityDrift);
                this.scandalDecay = Mathf.Max(0f, scandalDecay);
            }

            /// <summary>既定＝人気はゆっくり薄れ（0.05/年）、スキャンダルは早めに忘れられる（0.2/年）。</summary>
            public static PoliticianParams Default => new PoliticianParams(0.05f, 0.2f);
        }

        /// <summary>年境界の進行＝人気は中庸へドリフト（風化）、スキャンダルは時間で薄れる。</summary>
        public static void TickYear(PoliticianProfile pol, PoliticianParams prm)
        {
            if (pol == null) return;
            pol.popularity = Mathf.MoveTowards(pol.popularity, NeutralPopularity, prm.popularityDrift);
            pol.scandalLevel = Mathf.MoveTowards(pol.scandalLevel, 0f, prm.scandalDecay);
        }

        public static void TickYear(PoliticianProfile pol) => TickYear(pol, PoliticianParams.Default);
    }
}
