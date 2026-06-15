using UnityEngine;

namespace Ginei
{
    /// <summary>帝国主義の還流（ブーメラン効果）の調整係数。辺境の暴力が本国へ跳ね返る速さ・移植・常態化の重みを束ねる。</summary>
    public readonly struct ImperialBlowbackParams
    {
        /// <summary>本国が近い（連絡が密な）とき還流がどれだけ速くなるかの最大倍率（近距離での加速上限）。</summary>
        public readonly float proximityAcceleration;
        /// <summary>還流が国内急進化を進める速さ（per dt）。</summary>
        public readonly float radicalizationRate;
        /// <summary>制度的記憶が支配手法の移植をどれだけ確実にするか（0..1。記憶が濃いほど制度に焼き付く）。</summary>
        public readonly float memoryWeight;
        /// <summary>急進化が暴力の常態化を進める速さ（per dt・辺境限定だった暴力が本国に根付く）。</summary>
        public readonly float normalizationRate;
        /// <summary>還流カスケード（本国の急進化）の臨界閾値。</summary>
        public readonly float cascadeThreshold;

        public ImperialBlowbackParams(float proximityAcceleration, float radicalizationRate, float memoryWeight,
                                      float normalizationRate, float cascadeThreshold)
        {
            this.proximityAcceleration = Mathf.Max(1f, proximityAcceleration);
            this.radicalizationRate = Mathf.Max(0f, radicalizationRate);
            this.memoryWeight = Mathf.Clamp01(memoryWeight);
            this.normalizationRate = Mathf.Max(0f, normalizationRate);
            this.cascadeThreshold = Mathf.Clamp01(cascadeThreshold);
        }

        /// <summary>既定＝近接加速1.5・急進化速度0.2・記憶重み0.6・常態化速度0.1・カスケード閾値0.7。</summary>
        public static ImperialBlowbackParams Default => new ImperialBlowbackParams(1.5f, 0.2f, 0.6f, 0.1f, 0.7f);
    }

    /// <summary>
    /// 帝国主義の還流＝ブーメラン効果の純ロジック（TOTL-3 #1522・アーレント『全体主義の起原』参考）。植民地・辺境で
    /// 試された暴力・無法・人種主義の統治手法が、説明責任の不在のもとで野蛮さを増し、やがて本国へ跳ね返って
    /// 国内政治を急進化・野蛮化させる＝「辺境で試された支配の技法が母国を蝕むブーメラン」。本国が近い（連絡が密な）
    /// ほど還流は速く、制度的記憶が濃いほど手法が本国の制度へ移植され、暴力が本国でも常態化していく。
    /// 辺境の物理的・文化的な遠さ（自立志向）は <see cref="FrontierRules"/>、版図の過拡張による負担は
    /// <see cref="OverextensionRules"/> が担い、辺境での暴力行為そのものの汚点・宣伝価値は <see cref="AtrocityRules"/>
    /// が扱う。ここはその「辺境の暴力が本国へ還流して国内を急進化させるフィードバック」に特化する＝分担を分ける
    /// （全体主義の生成そのものは <see cref="TotalitarianRules"/> が担う＝こちらはその一前提＝暴力技法の本国還流）。
    /// 全入力クランプ・乱数なし決定論・基準値非破壊（係数を返すのみ）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ImperialBlowbackRules
    {
        // --- 調整値（マジックナンバー禁止＝const に集約） ---
        public const float MaxBrutality = 1f;          // 辺境の野蛮さの上限
        public const float MaxRadicalism = 1f;         // 国内急進化の上限
        public const float MaxNormalization = 1f;      // 暴力常態化の上限
        public const float ErosionFloor = 0f;          // 文明的規範の侵食の下限

        /// <summary>
        /// 辺境の野蛮さ(0..1)＝植民地での暴力 colonialViolence × 説明責任の不在 accountabilityVoid。
        /// 誰も咎めない（説明責任が無い）場で暴力は歯止めを失う＝辺境では支配の技法が際限なく粗暴になる。
        /// </summary>
        /// <param name="colonialViolence">辺境での暴力の強度(0..1)。</param>
        /// <param name="accountabilityVoid">説明責任の不在(0..1)。1=誰も咎めない・0=厳しく監視される。</param>
        public static float FrontierBrutality(float colonialViolence, float accountabilityVoid)
        {
            float v = Mathf.Clamp01(colonialViolence);
            float voidFrac = Mathf.Clamp01(accountabilityVoid);
            return Mathf.Clamp(v * voidFrac, 0f, MaxBrutality);
        }

        /// <summary>
        /// ブーメラン効果(0..1)＝辺境の野蛮さが本国へ跳ね返る量。本国が近い（連絡が密な）ほど還流が速い＝
        /// homeDistance(0..1) が小さいほど近接加速 <see cref="ImperialBlowbackParams.proximityAcceleration"/> が効く
        /// （近接1で最大加速・遠距離で等倍）。辺境の支配技法が母国へ逆流する経路の太さ。
        /// </summary>
        /// <param name="frontierBrutality">辺境の野蛮さ(0..1)。</param>
        /// <param name="homeDistance">辺境と本国の距離(0..1)。0=隣接・密接・1=遠く連絡が薄い。</param>
        public static float BoomerangEffect(float frontierBrutality, float homeDistance, ImperialBlowbackParams p)
        {
            float b = Mathf.Clamp(frontierBrutality, 0f, MaxBrutality);
            float dist = Mathf.Clamp01(homeDistance);
            // 近接(dist=0)で proximityAcceleration 倍・遠距離(dist=1)で等倍
            float accel = Mathf.Lerp(p.proximityAcceleration, 1f, dist);
            return Mathf.Clamp01(b * accel);
        }

        public static float BoomerangEffect(float frontierBrutality, float homeDistance)
            => BoomerangEffect(frontierBrutality, homeDistance, ImperialBlowbackParams.Default);

        /// <summary>
        /// 国内急進化の1tick後の値(0..1)＝還流したブーメラン効果が国内政治を急進化させる。現在の急進化に
        /// boomerangEffect×radicalizationRate×dt を加える＝辺境の暴力技法が本国の政治を粗暴化していく。
        /// </summary>
        public static float HomeRadicalizationTick(float homeRadicalism, float boomerangEffect, float dt,
                                                   ImperialBlowbackParams p)
        {
            float r = Mathf.Clamp(homeRadicalism, 0f, MaxRadicalism);
            float boom = Mathf.Clamp01(boomerangEffect);
            float delta = boom * p.radicalizationRate * Mathf.Max(0f, dt);
            return Mathf.Clamp(r + delta, 0f, MaxRadicalism);
        }

        public static float HomeRadicalizationTick(float homeRadicalism, float boomerangEffect, float dt)
            => HomeRadicalizationTick(homeRadicalism, boomerangEffect, dt, ImperialBlowbackParams.Default);

        /// <summary>
        /// 支配手法の移植度(0..1)＝辺境で確立した無法・人種主義の手法が本国の制度に焼き付く度合い。
        /// 辺境の野蛮さ×（制度的記憶を <see cref="ImperialBlowbackParams.memoryWeight"/> で効かせた重み）。
        /// 記憶が濃いほど手法は制度として残り、一過性で済まない＝技法が母国の統治に移植される。
        /// </summary>
        /// <param name="institutionalMemory">制度的記憶の濃さ(0..1)。1=手法が記録・継承される・0=忘れられる。</param>
        public static float MethodTransfer(float frontierBrutality, float institutionalMemory, ImperialBlowbackParams p)
        {
            float b = Mathf.Clamp(frontierBrutality, 0f, MaxBrutality);
            float mem = Mathf.Clamp01(institutionalMemory);
            // 記憶が薄くても基礎ぶんは移植され、濃いほど memoryWeight ぶん上積みされる
            float weight = (1f - p.memoryWeight) + p.memoryWeight * mem;
            return Mathf.Clamp01(b * weight);
        }

        public static float MethodTransfer(float frontierBrutality, float institutionalMemory)
            => MethodTransfer(frontierBrutality, institutionalMemory, ImperialBlowbackParams.Default);

        /// <summary>
        /// 暴力の常態化の1tick後の値(0..1)＝かつて辺境限定だった暴力が本国でも当たり前になる（野蛮化）。
        /// 現在の常態化に homeRadicalism×normalizationRate×dt を加える＝急進化が進むほど暴力が日常に根付く。
        /// </summary>
        public static float NormalizationOfViolence(float homeRadicalism, float normalizationCurrent, float dt,
                                                    ImperialBlowbackParams p)
        {
            float r = Mathf.Clamp(homeRadicalism, 0f, MaxRadicalism);
            float n = Mathf.Clamp(normalizationCurrent, 0f, MaxNormalization);
            float delta = r * p.normalizationRate * Mathf.Max(0f, dt);
            return Mathf.Clamp(n + delta, 0f, MaxNormalization);
        }

        public static float NormalizationOfViolence(float homeRadicalism, float normalizationCurrent, float dt)
            => NormalizationOfViolence(homeRadicalism, normalizationCurrent, dt, ImperialBlowbackParams.Default);

        /// <summary>
        /// 帰還兵の伝播(0..1)＝辺境で暴力に慣れた執行者が帰還し本国に粗暴さを持ち込む。
        /// 帰還する執行者の割合 returningEnforcers × 彼らが辺境で身につけた野蛮さ brutalityLearned。
        /// 暴力に慣れた人間そのものが還流の媒体になる＝ブーメランの人的経路。
        /// </summary>
        /// <param name="returningEnforcers">辺境から帰還する執行者の割合(0..1)。</param>
        /// <param name="brutalityLearned">彼らが辺境で身につけた野蛮さ(0..1)。</param>
        public static float VeteranContagion(float returningEnforcers, float brutalityLearned)
        {
            float ret = Mathf.Clamp01(returningEnforcers);
            float learned = Mathf.Clamp01(brutalityLearned);
            return Mathf.Clamp01(ret * learned);
        }

        /// <summary>
        /// 文明的規範の侵食(0..1)＝本国の文明的規範が辺境の論理（暴力の常態化）にどれだけ侵されたか。
        /// 暴力の常態化が進むほど本国の規範が辺境の論理に置き換わる＝1−（規範の残存）。
        /// </summary>
        public static float CivilizationalErosion(float normalizationOfViolence)
        {
            float n = Mathf.Clamp(normalizationOfViolence, 0f, MaxNormalization);
            return Mathf.Clamp01(Mathf.Max(ErosionFloor, n));
        }

        /// <summary>
        /// 還流カスケード判定＝国内急進化が臨界 threshold を超え、本国が辺境の暴力に染まった（後戻りしにくい）か。
        /// 超過＝ブーメランが本国の政治を急進化させきった臨界点。
        /// </summary>
        public static bool IsBlowbackCascade(float homeRadicalism, float threshold)
        {
            float r = Mathf.Clamp(homeRadicalism, 0f, MaxRadicalism);
            return r >= Mathf.Clamp01(threshold);
        }

        public static bool IsBlowbackCascade(float homeRadicalism)
            => IsBlowbackCascade(homeRadicalism, ImperialBlowbackParams.Default.cascadeThreshold);
    }
}
