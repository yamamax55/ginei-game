using UnityEngine;

namespace Ginei
{
    /// <summary>依怙贔屓・縁故人事の調整係数（情実人事システム）。</summary>
    public readonly struct FavoritismParams
    {
        /// <summary>人材配置損失の最大幅（贔屓×能力差に掛かる）。</summary>
        public readonly float misallocationScale;
        /// <summary>見過ごされた有能者の士気低下の最大幅（贔屓×彼らの実力に掛かる）。</summary>
        public readonly float moraleDropScale;
        /// <summary>有能者離反の最大幅（士気低下×外部機会に掛かる）。</summary>
        public readonly float flightScale;
        /// <summary>茶坊主繁殖の最大幅（贔屓に掛かる）。</summary>
        public readonly float sycophantScale;
        /// <summary>組織腐朽の最大幅（配置損失・離反・茶坊主の累積に掛かる）。</summary>
        public readonly float rotScale;
        /// <summary>実力主義が崩れたと見なす崩壊度の閾値（これを超えると実力主義は失われる）。</summary>
        public readonly float meritocracyThreshold;

        public FavoritismParams(
            float misallocationScale, float moraleDropScale, float flightScale,
            float sycophantScale, float rotScale, float meritocracyThreshold)
        {
            this.misallocationScale = Mathf.Clamp01(misallocationScale);
            this.moraleDropScale = Mathf.Clamp01(moraleDropScale);
            this.flightScale = Mathf.Clamp01(flightScale);
            this.sycophantScale = Mathf.Clamp01(sycophantScale);
            this.rotScale = Mathf.Clamp01(rotScale);
            this.meritocracyThreshold = Mathf.Clamp01(meritocracyThreshold);
        }

        /// <summary>既定＝配置損失1.0/士気低下1.0/離反1.0/茶坊主1.0/腐朽1.0/実力主義閾値0.5。</summary>
        public static FavoritismParams Default => new FavoritismParams(1f, 1f, 1f, 1f, 1f, 0.5f);
    }

    /// <summary>
    /// 依怙贔屓・縁故人事の純ロジック（情実による登用が組織に与える害）。
    /// 個人的縁故が客観的評価を押しのけて人を選ぶと、適任者でなく贔屓の者が席に就き（配置損失）、
    /// 報われない有能者は士気を落として外へ去り（離反）、君主の周りには能力でなくおべっかで生き残る
    /// 茶坊主が繁殖する。結果として「実力で報われる」という組織の前提（実力主義）が崩れていく。
    /// <see cref="BattlefieldPromotionRules"/>（武勲による昇進）・<see cref="ImperialFavorRules"/>（君主個人の寵）
    /// とは別系統＝こちらは人事一般に情実が入ることの組織的な害を扱う。
    /// 乱数なし・決定論・全入力クランプ・基準値非破壊（実効値パターン）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FavoritismRules
    {
        /// <summary>
        /// 依怙贔屓の度合い（0..1）＝個人的縁故 personalTies/(縁故＋客観性 objectivity)。
        /// 縁故が客観的評価より重ければ贔屓が深い。両者ゼロなら判断材料が無く贔屓も無い（0）。
        /// </summary>
        public static float FavoritismDegree(float personalTies, float objectivity, FavoritismParams p)
        {
            float ties = Mathf.Clamp01(personalTies);
            float obj = Mathf.Clamp01(objectivity);
            float denom = ties + obj;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(ties / denom);
        }

        public static float FavoritismDegree(float personalTies, float objectivity)
            => FavoritismDegree(personalTies, objectivity, FavoritismParams.Default);

        /// <summary>
        /// 人材配置の損失（0..misallocationScale）＝贔屓度 favoritismDegree×能力差 competenceGap。
        /// 能力差＝登用された者と本来の適任者の腕の開き（0..1、大きいほど不適格）。
        /// 贔屓が深く適任者との差が大きいほど、適材適所が損なわれる。
        /// </summary>
        public static float MisallocationLoss(float favoritismDegree, float competenceGap, FavoritismParams p)
        {
            float deg = Mathf.Clamp01(favoritismDegree);
            float gap = Mathf.Clamp01(competenceGap);
            return Mathf.Clamp01(deg * gap * p.misallocationScale);
        }

        public static float MisallocationLoss(float favoritismDegree, float competenceGap)
            => MisallocationLoss(favoritismDegree, competenceGap, FavoritismParams.Default);

        /// <summary>
        /// 見過ごされた有能者の士気（-moraleDropScale..0、符号付き＝負＝低下）。
        /// 贔屓度×見過ごされた者の実力 meritOfOverlooked（0..1）が深いほど士気は深く沈む。
        /// 実力ある者ほど不当な扱いに気付き、より深く失望する。
        /// </summary>
        public static float OverlookedTalentMorale(float favoritismDegree, float meritOfOverlooked, FavoritismParams p)
        {
            float deg = Mathf.Clamp01(favoritismDegree);
            float merit = Mathf.Clamp01(meritOfOverlooked);
            return Mathf.Clamp(-(deg * merit * p.moraleDropScale), -1f, 1f);
        }

        public static float OverlookedTalentMorale(float favoritismDegree, float meritOfOverlooked)
            => OverlookedTalentMorale(favoritismDegree, meritOfOverlooked, FavoritismParams.Default);

        /// <summary>
        /// 有能者の離反（0..flightScale）＝士気低下の大きさ |overlookedTalentMorale|×外部機会 outsideOpportunity。
        /// 失望が深くても受け皿（他組織・在野）が無ければ去れない。失望×機会が揃って初めて去る。
        /// </summary>
        public static float TalentFlight(float overlookedTalentMorale, float outsideOpportunity, FavoritismParams p)
        {
            float drop = Mathf.Clamp01(Mathf.Abs(overlookedTalentMorale));
            float opp = Mathf.Clamp01(outsideOpportunity);
            return Mathf.Clamp01(drop * opp * p.flightScale);
        }

        public static float TalentFlight(float overlookedTalentMorale, float outsideOpportunity)
            => TalentFlight(overlookedTalentMorale, outsideOpportunity, FavoritismParams.Default);

        /// <summary>
        /// 実力主義の崩壊（0..1）＝贔屓度/(1＋制度的チェック institutionalCheck)。
        /// 試験制度・人事委員会・監査などの制度的歯止め（≥0）が強いほど崩壊は抑えられる。
        /// 歯止めゼロなら贔屓度がそのまま崩壊度になる。
        /// </summary>
        public static float MeritocracyErosion(float favoritismDegree, float institutionalCheck, FavoritismParams p)
        {
            float deg = Mathf.Clamp01(favoritismDegree);
            float check = Mathf.Max(0f, institutionalCheck);
            return Mathf.Clamp01(deg / (1f + check));
        }

        public static float MeritocracyErosion(float favoritismDegree, float institutionalCheck)
            => MeritocracyErosion(favoritismDegree, institutionalCheck, FavoritismParams.Default);

        /// <summary>
        /// 茶坊主の繁殖（0..sycophantScale）＝贔屓度に応じておべっか使いが増える。
        /// 能力でなく追従で取り立てられる組織では、報われる行動（追従）を学んだ者が増殖する。
        /// </summary>
        public static float SycophantProliferation(float favoritismDegree, FavoritismParams p)
        {
            float deg = Mathf.Clamp01(favoritismDegree);
            return Mathf.Clamp01(deg * p.sycophantScale);
        }

        public static float SycophantProliferation(float favoritismDegree)
            => SycophantProliferation(favoritismDegree, FavoritismParams.Default);

        /// <summary>
        /// 組織の腐朽（0..rotScale、総合指標）＝配置損失・有能者離反・茶坊主繁殖の累積。
        /// 各害を独立要因として 1−(1−損失)(1−離反)(1−茶坊主) で合成＝どれか一つでも深ければ腐朽は進み、
        /// 三つが揃えば組織はほぼ機能を失う。
        /// </summary>
        public static float OrganizationalRot(float misallocationLoss, float talentFlight, float sycophantProliferation, FavoritismParams p)
        {
            float loss = Mathf.Clamp01(misallocationLoss);
            float flight = Mathf.Clamp01(talentFlight);
            float syc = Mathf.Clamp01(sycophantProliferation);
            float intact = (1f - loss) * (1f - flight) * (1f - syc);
            return Mathf.Clamp01((1f - intact) * p.rotScale);
        }

        public static float OrganizationalRot(float misallocationLoss, float talentFlight, float sycophantProliferation)
            => OrganizationalRot(misallocationLoss, talentFlight, sycophantProliferation, FavoritismParams.Default);

        /// <summary>
        /// 実力主義が保たれているか＝崩壊度 meritocracyErosion が閾値以下なら true。
        /// 閾値を超えると「実力で報われる」前提が失われたと見なす。
        /// </summary>
        public static bool IsMeritocracyIntact(float meritocracyErosion, float threshold)
        {
            return Mathf.Clamp01(meritocracyErosion) <= Mathf.Clamp01(threshold);
        }

        public static bool IsMeritocracyIntact(float meritocracyErosion)
            => IsMeritocracyIntact(meritocracyErosion, FavoritismParams.Default.meritocracyThreshold);
    }
}
