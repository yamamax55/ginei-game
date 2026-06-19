using UnityEngine;

namespace Ginei
{
    /// <summary>将帥の声望（武名・威信）の調整係数。</summary>
    public readonly struct CommandReputationParams
    {
        /// <summary>敗北の重み（一敗が勝利何回ぶんの武名を相殺するか）。</summary>
        public readonly float defeatPenalty;
        /// <summary>戦の規模が武名に効く重み（大会戦の勝利ほど名を上げる）。</summary>
        public readonly float scaleWeight;
        /// <summary>戦歴→声望の正規化（この戦績で声望が満タン1.0に近づく分母）。</summary>
        public readonly float recordNormalizer;
        /// <summary>味方士気の鼓舞上限（声望が高くてもこの幅まで＝過信を防ぐ）。</summary>
        public readonly float inspirationCap;
        /// <summary>部隊の馴染みが鼓舞に効く重み（古参の部下ほど将を信じる）。</summary>
        public readonly float familiarityWeight;
        /// <summary>敵の萎縮のスケール（声望をこの倍に見立てて敵の場慣れと綱引き）。</summary>
        public readonly float intimidationScale;
        /// <summary>直近の敗北が声望を削る率（一敗で大きく失墜＝名声は脆い）。</summary>
        public readonly float defeatDecayRate;
        /// <summary>功高震主の立ち上がり（声望が君主の信頼を超えた差をこの倍で警戒に変える）。</summary>
        public readonly float excessGain;
        /// <summary>嫉妬の立ち上がり（功高震主×同僚の野心をこの倍で嫉妬に変える）。</summary>
        public readonly float jealousyGain;
        /// <summary>階級が実効権威に効く重み（同じ声望でも高位ほど指揮が通る）。</summary>
        public readonly float rankWeight;
        /// <summary>嫉妬が実効権威を削る重み（嫉妬を買うほど指揮系統が軋む）。</summary>
        public readonly float jealousyAuthorityPenalty;

        public CommandReputationParams(float defeatPenalty, float scaleWeight, float recordNormalizer,
                                       float inspirationCap, float familiarityWeight, float intimidationScale,
                                       float defeatDecayRate, float excessGain, float jealousyGain,
                                       float rankWeight, float jealousyAuthorityPenalty)
        {
            this.defeatPenalty = Mathf.Max(0f, defeatPenalty);
            this.scaleWeight = Mathf.Max(0f, scaleWeight);
            this.recordNormalizer = Mathf.Max(0.0001f, recordNormalizer);
            this.inspirationCap = Mathf.Max(0f, inspirationCap);
            this.familiarityWeight = Mathf.Max(0f, familiarityWeight);
            this.intimidationScale = Mathf.Max(0f, intimidationScale);
            this.defeatDecayRate = Mathf.Clamp01(defeatDecayRate);
            this.excessGain = Mathf.Max(0f, excessGain);
            this.jealousyGain = Mathf.Max(0f, jealousyGain);
            this.rankWeight = Mathf.Max(0f, rankWeight);
            this.jealousyAuthorityPenalty = Mathf.Max(0f, jealousyAuthorityPenalty);
        }

        /// <summary>既定＝敗北重み2/規模重み1/正規化20/鼓舞上限0.3/馴染み重み0.5/萎縮スケール1.5/敗北失墜率0.4/震主0.8/嫉妬1/階級重み0.4/嫉妬罰0.6。</summary>
        public static CommandReputationParams Default =>
            new CommandReputationParams(2f, 1f, 20f, 0.3f, 0.5f, 1.5f, 0.4f, 0.8f, 1f, 0.4f, 0.6f);
    }

    /// <summary>
    /// 将帥の声望（武名・威信）の純ロジック（名将の名前そのものが戦力になる）。戦歴（大会戦の勝利ほど）から
    /// 武名が育ち、声望は味方の士気を鼓舞し（古参の部下ほど将を信じる）、敵を萎縮させ（敵が場慣れしていない
    /// ほど効く）、敗北で大きく失墜する（名声は脆い）。さらに過大な声望は功高震主（君主の警戒）を招き、同僚の
    /// 野心と相俟って嫉妬を生み、実効的な指揮権威を蝕む。声望×階級から嫉妬を引いた実効権威（-1..1）を返す。
    /// すべて決定論・乱数なし・入力 clamp・実効値パターン（基準非破壊）。声望は 0..1 で正規化して扱う。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CommandReputationRules
    {
        /// <summary>名将と呼べる既定の声望閾値（武名 0.6 以上で「名将」）。</summary>
        public const float DefaultRenownThreshold = 0.6f;

        /// <summary>
        /// 戦歴から武名（0..1）＝（勝利−敗北×敗北重み）×（1＋戦の規模×規模重み）を正規化。
        /// 大会戦での勝利ほど名を上げ、敗北はそれ以上に名を落とす。負け越せば声望は 0。
        /// </summary>
        public static float ReputationFromRecord(int victories, int defeats, float battleScale, CommandReputationParams p)
        {
            float wins = Mathf.Max(0, victories);
            float losses = Mathf.Max(0, defeats);
            float scale = Mathf.Clamp01(battleScale);
            float net = wins - losses * p.defeatPenalty;
            float weighted = net * (1f + scale * p.scaleWeight);
            return Mathf.Clamp01(weighted / p.recordNormalizer);
        }

        public static float ReputationFromRecord(int victories, int defeats, float battleScale)
            => ReputationFromRecord(victories, defeats, battleScale, CommandReputationParams.Default);

        /// <summary>
        /// 味方士気の鼓舞（0..inspirationCap）＝声望×（1＋部隊の馴染み×馴染み重み）。名将の旗の下では
        /// 兵が奮い立ち、長く共に戦った古参の部下ほど将を信じて士気が上がる。上限で過信を抑える。
        /// </summary>
        public static float MoraleInspiration(float reputation, float troopFamiliarity, CommandReputationParams p)
        {
            float rep = Mathf.Clamp01(reputation);
            float fam = Mathf.Clamp01(troopFamiliarity);
            float raw = rep * (1f + fam * p.familiarityWeight);
            return Mathf.Clamp(raw, 0f, p.inspirationCap);
        }

        public static float MoraleInspiration(float reputation, float troopFamiliarity)
            => MoraleInspiration(reputation, troopFamiliarity, CommandReputationParams.Default);

        /// <summary>
        /// 敵の萎縮（0..1）＝声望×スケール／（声望×スケール＋敵の場慣れ）。武名が高いほど敵は怯むが、
        /// 修羅場を踏んだ歴戦の敵（experience が高い）には効きにくい。声望 0 なら萎縮 0。
        /// </summary>
        public static float EnemyIntimidation(float reputation, float enemyExperience, CommandReputationParams p)
        {
            float fame = Mathf.Clamp01(reputation) * p.intimidationScale;
            float exp = Mathf.Clamp01(enemyExperience);
            float denom = fame + exp;
            if (denom <= 0.0001f) return 0f;
            return Mathf.Clamp01(fame / denom);
        }

        public static float EnemyIntimidation(float reputation, float enemyExperience)
            => EnemyIntimidation(reputation, enemyExperience, CommandReputationParams.Default);

        /// <summary>
        /// 直近の敗北による声望の失墜（0..1）＝声望×（1−直近敗北×失墜率）。名声は脆く、一敗で大きく削れる。
        /// recentDefeat 0 なら不変、1 なら失墜率ぶん丸ごと削れる。戻り値は更新後の声望。
        /// </summary>
        public static float ReputationDecay(float reputation, float recentDefeat, CommandReputationParams p)
        {
            float rep = Mathf.Clamp01(reputation);
            float hit = Mathf.Clamp01(recentDefeat);
            return Mathf.Clamp01(rep * (1f - hit * p.defeatDecayRate));
        }

        public static float ReputationDecay(float reputation, float recentDefeat)
            => ReputationDecay(reputation, recentDefeat, CommandReputationParams.Default);

        /// <summary>
        /// 功高震主（0..1）＝声望が君主の信頼を上回った差×立ち上がり。声望が信頼を超えるほど君主に
        /// 警戒される。信頼が声望以上なら 0（信を得ていれば名声は脅威でない）。
        /// </summary>
        public static float RenownExcess(float reputation, float sovereignTrust, CommandReputationParams p)
        {
            float rep = Mathf.Clamp01(reputation);
            float trust = Mathf.Clamp01(sovereignTrust);
            return Mathf.Clamp01((rep - trust) * p.excessGain);
        }

        public static float RenownExcess(float reputation, float sovereignTrust)
            => RenownExcess(reputation, sovereignTrust, CommandReputationParams.Default);

        /// <summary>
        /// 嫉妬（0..1）＝功高震主×同僚の野心×立ち上がり。功高震主だけでは波風で済むが、野心ある同僚が
        /// いれば嫉妬・讒言に火がつく。どちらかが 0 なら嫉妬は起きない。
        /// </summary>
        public static float JealousyProvoked(float renownExcess, float peerAmbition, CommandReputationParams p)
        {
            float excess = Mathf.Clamp01(renownExcess);
            float ambition = Mathf.Clamp01(peerAmbition);
            return Mathf.Clamp01(excess * ambition * p.jealousyGain);
        }

        public static float JealousyProvoked(float renownExcess, float peerAmbition)
            => JealousyProvoked(renownExcess, peerAmbition, CommandReputationParams.Default);

        /// <summary>
        /// 実効的な指揮権威（-1..1）＝声望×（1＋階級×階級重み）−嫉妬×嫉妬罰。声望と高位の階級が権威を
        /// 支え、嫉妬がそれを蝕む。嫉妬が深ければマイナス（指揮系統が軋み命令が通らない）にもなりうる。
        /// rankTier は 0..1 の正規化階級（高いほど上位）。
        /// </summary>
        public static float EffectiveAuthority(float reputation, float rankTier, float jealousy, CommandReputationParams p)
        {
            float rep = Mathf.Clamp01(reputation);
            float rank = Mathf.Clamp01(rankTier);
            float jeal = Mathf.Clamp01(jealousy);
            float authority = rep * (1f + rank * p.rankWeight) - jeal * p.jealousyAuthorityPenalty;
            return Mathf.Clamp(authority, -1f, 1f);
        }

        public static float EffectiveAuthority(float reputation, float rankTier, float jealousy)
            => EffectiveAuthority(reputation, rankTier, jealousy, CommandReputationParams.Default);

        /// <summary>名将と呼べるか＝声望が閾値以上。</summary>
        public static bool IsRenownedCommander(float reputation, float threshold)
            => Mathf.Clamp01(reputation) >= Mathf.Clamp01(threshold);

        /// <summary>名将判定（既定閾値委譲版）。</summary>
        public static bool IsRenownedCommander(float reputation)
            => IsRenownedCommander(reputation, DefaultRenownThreshold);
    }
}
