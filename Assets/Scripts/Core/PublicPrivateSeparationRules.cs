using UnityEngine;

namespace Ginei
{
    /// <summary>公私の分離（国庫 vs 元首私財）の調整係数（#1035）。</summary>
    public readonly struct PublicPrivateSeparationParams
    {
        /// <summary>制度化0でも残る最低限の分離度（家産国家でも形ばかりの区別はある）。</summary>
        public readonly float baseSeparation;
        /// <summary>制度化が分離度を引き上げる強さ（制度化→近代国家化の傾き）。</summary>
        public readonly float institutionWeight;
        /// <summary>私物化リスクの係数（分離の弱さ×君主の貪欲に掛かる＝汚職の余地）。</summary>
        public readonly float privatizationScale;
        /// <summary>分離が継承の安定に寄与する強さ（公的制度ほど相続争いが減る）。</summary>
        public readonly float successionWeight;
        /// <summary>分離による正統性の最大寄与（法の支配の土台）。</summary>
        public readonly float legitimacyWeight;
        /// <summary>既得層の抵抗の係数（特権×分離が奪う私腹に掛かる）。</summary>
        public readonly float eliteResistanceScale;

        public PublicPrivateSeparationParams(float baseSeparation, float institutionWeight,
            float privatizationScale, float successionWeight,
            float legitimacyWeight, float eliteResistanceScale)
        {
            this.baseSeparation = Mathf.Clamp01(baseSeparation);
            this.institutionWeight = Mathf.Clamp01(institutionWeight);
            this.privatizationScale = Mathf.Max(0f, privatizationScale);
            this.successionWeight = Mathf.Clamp01(successionWeight);
            this.legitimacyWeight = Mathf.Clamp01(legitimacyWeight);
            this.eliteResistanceScale = Mathf.Max(0f, eliteResistanceScale);
        }

        /// <summary>既定＝基礎分離0.1・制度化重み0.9・私物化係数1.0・継承重み0.7・正統性重み0.6・既得抵抗係数1.0。</summary>
        public static PublicPrivateSeparationParams Default =>
            new PublicPrivateSeparationParams(0.1f, 0.9f, 1f, 0.7f, 0.6f, 1f);
    }

    /// <summary>
    /// 公私の分離＝国庫 vs 元首私財の純ロジック（#1035）。前近代の家産国家では君主の私財と国庫が
    /// 混同される＝国家は君主の私物。制度化が進むほど公私が分離し（家産国家→近代国家）、私物化（汚職）
    /// の余地が減り、国庫の漏出が止まり、継承は相続争いでなく公的制度として円滑に運ぶ。
    /// <see cref="RegimeRules"/>（腐敗＝corruption の進行・制度疲労）へ接続＝私物化リスクは腐敗の余地を測る。
    /// <see cref="FiscalRules"/>（国庫＝歳入歳出・国債の収支）とは別系統＝こちらは資金の<b>帰属</b>（公金か
    /// 君主の私財か）を扱う。<see cref="SuccessionWarRules"/>（継承戦争の勃発・解決）とは別＝こちらは
    /// 「国家が君主の私物なら継承は相続争い／公的制度なら円滑」という分離が継承に与える安定度のみを返す。
    /// 倍率・割合は係数として使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PublicPrivateSeparationRules
    {
        /// <summary>
        /// 公私分離の度合い（0..1）＝基礎分離＋制度化×制度化重み。
        /// 制度化が進むほど国庫と私財が分かれる（家産国家＝低分離→近代国家＝高分離）。
        /// </summary>
        public static float SeparationLevel(float institutionalization, PublicPrivateSeparationParams p)
        {
            float inst = Mathf.Clamp01(institutionalization);
            return Mathf.Clamp01(p.baseSeparation + inst * p.institutionWeight);
        }

        public static float SeparationLevel(float institutionalization)
            => SeparationLevel(institutionalization, PublicPrivateSeparationParams.Default);

        /// <summary>
        /// 私物化リスク（0..1）＝（1−分離度）×君主の貪欲×係数。分離が弱く君主が貪欲なほど
        /// 国庫が私財に流れる＝汚職の余地（<see cref="RegimeRules"/> の corruption へ接続する係数）。
        /// </summary>
        public static float PrivatizationRisk(float separationLevel, float rulerGreed, PublicPrivateSeparationParams p)
        {
            float sep = Mathf.Clamp01(separationLevel);
            float greed = Mathf.Clamp01(rulerGreed);
            return Mathf.Clamp01((1f - sep) * greed * p.privatizationScale);
        }

        public static float PrivatizationRisk(float separationLevel, float rulerGreed)
            => PrivatizationRisk(separationLevel, rulerGreed, PublicPrivateSeparationParams.Default);

        /// <summary>
        /// 国庫の漏出額＝公金×私物化リスク。公金が私的に消える量（家産国家ほど大きく漏れる）。
        /// 近代国家（高分離）では漏出はほぼ止まる。
        /// </summary>
        public static float TreasuryLeakage(float publicFunds, float separationLevel, float rulerGreed,
            PublicPrivateSeparationParams p)
        {
            return Mathf.Max(0f, publicFunds) * PrivatizationRisk(separationLevel, rulerGreed, p);
        }

        public static float TreasuryLeakage(float publicFunds, float separationLevel, float rulerGreed)
            => TreasuryLeakage(publicFunds, separationLevel, rulerGreed, PublicPrivateSeparationParams.Default);

        /// <summary>
        /// 分離による正統性（0..1）＝分離度×正統性重み。公私を分ける統治は信頼される（法の支配の土台）。
        /// </summary>
        public static float LegitimacyFromSeparation(float separationLevel, PublicPrivateSeparationParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(separationLevel) * p.legitimacyWeight);
        }

        public static float LegitimacyFromSeparation(float separationLevel)
            => LegitimacyFromSeparation(separationLevel, PublicPrivateSeparationParams.Default);

        /// <summary>
        /// 継承の安定性（0..1）＝（1−継承重み）＋分離度×継承重み。国家が君主の私物（低分離）なら
        /// 継承は相続争い（不安定）、公的制度（高分離）なら円滑＝制度化が継承戦争を防ぐ。
        /// </summary>
        public static float SuccessionStability(float separationLevel, PublicPrivateSeparationParams p)
        {
            float sep = Mathf.Clamp01(separationLevel);
            return Mathf.Clamp01((1f - p.successionWeight) + sep * p.successionWeight);
        }

        public static float SuccessionStability(float separationLevel)
            => SuccessionStability(separationLevel, PublicPrivateSeparationParams.Default);

        /// <summary>
        /// 既得層の抵抗（0..1）＝分離度×特権×係数。公私分離は私腹を肥やす特権を奪う＝門閥が抵抗する。
        /// 分離を進めるほど（高分離）、特権が大きいほど抵抗は強い。
        /// </summary>
        public static float ReformResistanceFromElites(float separationLevel, float elitePrivilege,
            PublicPrivateSeparationParams p)
        {
            float sep = Mathf.Clamp01(separationLevel);
            float priv = Mathf.Clamp01(elitePrivilege);
            return Mathf.Clamp01(sep * priv * p.eliteResistanceScale);
        }

        public static float ReformResistanceFromElites(float separationLevel, float elitePrivilege)
            => ReformResistanceFromElites(separationLevel, elitePrivilege, PublicPrivateSeparationParams.Default);
    }
}
