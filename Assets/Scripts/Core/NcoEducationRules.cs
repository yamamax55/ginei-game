using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 下士官教育の課程（米軍 NCO PME ラダー＝NCOPDS）。初級＝Basic Leader Course（班長級）／中級＝Advanced Leader Course（分隊軍曹級）／
    /// 上級＝Senior Leader Course（小隊軍曹/先任級）／最先任＝Master Leader/Sergeants Major Academy（最先任上級曹長＝CSM級）。
    /// </summary>
    public enum NcoCourse { 初級, 中級, 上級, 最先任 }

    /// <summary>
    /// 下士官教育の純ロジック（#210 下士官団の育成エンジン・米軍 NCOPDS モデル・test-first・唯一の窓口）。
    /// 新兵教育（<see cref="RecruitTrainingRules"/>）で兵を量産し、士官教育（<see cref="MilitaryAcademyRules"/> #155）が将校を生むのに対し、
    /// ここは**経験でしか育たない部隊の背骨＝下士官団**を作る。核は3つ：
    /// (1) <b>STEP＝教育が昇進の前提</b>（no school, no promotion＝各 PME 課程の修了が次の下士官段位の要件）。
    /// (2) <b>PME ラダーの選抜</b>（上ほど狭き門）。(3) <b>“経験は急造できない”</b>＝損耗の質的打撃と再建の遅さ
    /// （兵は徴募#96 で即補充できるが下士官団の再建は年単位＝ベテラン部隊の壊滅は痛恨）。
    /// 強い下士官団→練度/結束/自律の背骨倍率（#106 係数・#147/#206 の自律）。個体粒度へ降りない＝集約スカラー（PERF #1117）。状態は変えない（read-only）。
    /// </summary>
    public static class NcoEducationRules
    {
        // ===== PME ラダーの選抜（上ほど狭き門・#155 の多段選抜に倣う） =====
        public const float Pass初級 = 0.70f;
        public const float Pass中級 = 0.55f;
        public const float Pass上級 = 0.40f;
        public const float Pass最先任 = 0.25f;

        // ===== 下士官団の質（PME 到達段） =====
        public const float ProgramQualityFloor = 0.40f; // 初級どまりでも最低限

        // ===== 背骨効果（#210：練度・結束・自律） =====
        public const float IdealNcoRatio = 0.15f;        // 理想の下士官比（兵に対する）
        public const float MaxProficiencyBonus = 0.30f;  // 練度（命中/回避）
        public const float MaxCohesionBonus = 0.40f;     // 結束（士気の粘り）

        // ===== 損耗の質・再建（“経験は急造できない”） =====
        public const float ExperienceLossAmplifier = 0.50f; // 質が高いほど失う経験が大きい（ベテランは代替困難）
        public const float MaxExpansionDilution = 0.50f;    // 急拡大で薄まる上限（大衆動員 vs 職業軍）
        public const float BaseRebuildYears = 6f;           // 質を0→1へ育てる基準年数（経験育成）

        // ===== STEP（教育が昇進の前提＝no school, no promotion・米軍 NCOPDS） =====

        /// <summary>その課程の修了で開かれる下士官段位（初級→1 … 最先任→4）。</summary>
        public static int GradeTierFor(NcoCourse course) => (int)course + 1;

        /// <summary>その下士官段位へ昇進するのに必要な課程（段位1→初級 … 4→最先任。範囲外はクランプ）。</summary>
        public static NcoCourse RequiredCourseForTier(int ncoTier)
        {
            int idx = Mathf.Clamp(ncoTier - 1, 0, 3);
            return (NcoCourse)idx;
        }

        /// <summary>
        /// その段位へ昇進できるか＝<b>必要課程を修了していること</b>（STEP）。
        /// 修了した最高課程の段位が目標段位以上なら可。教育を経ない昇進は不可＝下士官の質を担保する。
        /// </summary>
        public static bool PromotionEligible(NcoCourse highestCompleted, int targetTier)
            => targetTier >= 1 && GradeTierFor(highestCompleted) >= targetTier;

        // ===== PME ラダーの選抜 =====

        public static float PassRate(NcoCourse course)
        {
            switch (course)
            {
                case NcoCourse.初級: return Pass初級;
                case NcoCourse.中級: return Pass中級;
                case NcoCourse.上級: return Pass上級;
                case NcoCourse.最先任: return Pass最先任;
                default: return Pass初級;
            }
        }

        /// <summary>その課程の合格数＝受講者×合格率（狭き門・float 誤差ガード付）。</summary>
        public static int QuotaPassing(int sitters, NcoCourse course)
        {
            if (sitters <= 0) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(sitters * PassRate(course) + 1e-4f), 0, sitters);
        }

        /// <summary>輩出数＝適格な熟練兵プールから訓練枠の範囲で受講させ、合格率で篩う。</summary>
        public static int Graduates(int eligiblePool, int capacity, NcoCourse course)
        {
            int sitters = Mathf.Clamp(Mathf.Min(Mathf.Max(0, eligiblePool), Mathf.Max(0, capacity)), 0, int.MaxValue);
            return QuotaPassing(sitters, course);
        }

        // ===== 下士官団の質 =====

        /// <summary>
        /// 下士官団の質＝学校の質×PME 到達段（最先任まで通すほど高く、初級どまりは <see cref="ProgramQualityFloor"/> 係数）。
        /// </summary>
        public static float ProgramQuality(NcoCourse highestProgram, float academyQuality)
        {
            float ladder = (int)highestProgram / 3f; // 0(初級)..1(最先任)
            float factor = ProgramQualityFloor + (1f - ProgramQualityFloor) * ladder;
            return Mathf.Clamp01(Mathf.Clamp01(academyQuality) * factor);
        }

        // ===== 背骨効果（#210） =====

        /// <summary>下士官の厚み 0..1＝下士官比を理想比（<see cref="IdealNcoRatio"/>）に対して正規化。</summary>
        public static float Thickness(float ncoCount, float troopStrength)
        {
            if (ncoCount <= 0f || troopStrength <= 0f) return 0f;
            float ratio = ncoCount / troopStrength;
            return Mathf.Clamp01(ratio / IdealNcoRatio);
        }

        /// <summary>練度倍率（命中/回避）＝1.0＋密度×質（厚みと質の両方が要る）。</summary>
        public static float ProficiencyMultiplier(NcoCorps corps)
            => corps == null ? 1f : 1f + MaxProficiencyBonus * Mathf.Clamp01(corps.density) * Mathf.Clamp01(corps.quality);

        /// <summary>結束倍率（士気の粘り＝崩壊耐性・<see cref="FleetMorale"/> に効く想定）。</summary>
        public static float CohesionMultiplier(NcoCorps corps)
            => corps == null ? 1f : 1f + MaxCohesionBonus * Mathf.Clamp01(corps.density) * Mathf.Clamp01(corps.quality);

        /// <summary>
        /// 自律 0..1（命令なしで動けるか＝任務戦術#147/通信断#206 下の地力）。下士官団が厚く質が高いほど高い。
        /// 兵だけ（下士官枯渇）の部隊は0に近く中央指揮頼みで麻痺する。
        /// </summary>
        public static float AutonomyFactor(NcoCorps corps)
            => corps == null ? 0f : Mathf.Clamp01(corps.density) * Mathf.Clamp01(corps.quality);

        // ===== 損耗の質・再建（“経験は急造できない”・#210 核心） =====

        /// <summary>
        /// 損耗による質的打撃＝下士官団の質の低下量（無差別損耗でも institutional experience を失う）。
        /// <b>質が高い（ベテラン）ほど1損耗あたり失う経験が大きい</b>（<see cref="ExperienceLossAmplifier"/>）＝ベテラン部隊の壊滅は痛恨。
        /// 返り値は質の減少量Δ（0..現在質）。
        /// </summary>
        public static float AttritionExperienceLoss(NcoCorps corps, float casualtyFraction)
        {
            if (corps == null) return 0f;
            float c = Mathf.Clamp01(casualtyFraction);
            float q = Mathf.Clamp01(corps.quality);
            float drop = c * (1f + ExperienceLossAmplifier * q);
            return Mathf.Clamp(drop, 0f, q);
        }

        /// <summary>
        /// 急拡大による希薄化係数 0..1（大衆動員で下士官比が薄まり質が落ちる＝職業軍 vs 徴集大量軍）。
        /// expansionRate 0..1（1=倍化級）で <see cref="MaxExpansionDilution"/> まで効く。実効質＝質×この係数。
        /// </summary>
        public static float DilutionFactor(float expansionRate)
            => 1f - MaxExpansionDilution * Mathf.Clamp01(expansionRate);

        /// <summary>
        /// 下士官団の再建所要（年）＝目標質−現在質に比例（経験育成は年単位＝兵の即補充と違い急造できない）。
        /// 目標が現在以下なら0。
        /// </summary>
        public static float RebuildYears(float currentQuality, float targetQuality)
        {
            float gap = Mathf.Clamp01(targetQuality) - Mathf.Clamp01(currentQuality);
            return gap <= 0f ? 0f : gap * BaseRebuildYears;
        }
    }
}
