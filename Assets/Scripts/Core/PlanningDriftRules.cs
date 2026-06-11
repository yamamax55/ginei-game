using UnityEngine;

namespace Ginei
{
    /// <summary>計画失敗への岐路（PlannersDilemma の選択肢）。</summary>
    public enum PlannerChoice
    {
        /// <summary>撤回＝介入を巻き戻し市場へ返す（政治的に難しい）。</summary>
        撤回,
        /// <summary>さらなる統制＝失敗を補う追加介入へ傾く（ラチェットが進む）。</summary>
        さらなる統制,
    }

    /// <summary>計画経済ドリフトの純データ（ハイエク型・HAYK-1 #1541）。</summary>
    public struct PlanningDrift
    {
        /// <summary>計画化度（0..1＝中央計画がどれだけ経済を覆っているか）。</summary>
        public float planningLevel;
        /// <summary>強制度（0..1＝命令・統制の強さ）。</summary>
        public float coercion;
        /// <summary>残された個人の自由（0..1＝政治的・経済的自由の残量）。</summary>
        public float freedom;

        public PlanningDrift(float planningLevel, float coercion, float freedom)
        {
            this.planningLevel = Mathf.Clamp01(planningLevel);
            this.coercion = Mathf.Clamp01(coercion);
            this.freedom = Mathf.Clamp01(freedom);
        }
    }

    /// <summary>計画ドリフトの調整係数（ハイエク型・HAYK-1 #1541）。</summary>
    public readonly struct PlanningDriftParams
    {
        /// <summary>介入ラチェットの基礎進行/秒（計画失敗が呼ぶ追加介入の積み上がり）。</summary>
        public readonly float ratchetRate;
        /// <summary>計画化が強制を生む速さ/秒（計画化に比例して命令・統制が増す）。</summary>
        public readonly float coercionRate;
        /// <summary>強制が自由を蝕む速さ/秒（経済統制→政治的自由の抑圧）。</summary>
        public readonly float erosionRate;
        /// <summary>知識代替の効率損失の最大幅（計画化1で価格・分散知識を失いこのぶん効率が落ちる）。</summary>
        public readonly float knowledgeLossMax;
        /// <summary>坂道（臨界超過後）の統制加速倍率（≥1・閾値超えで一気に進む）。</summary>
        public readonly float slopeMultiplier;
        /// <summary>撤回の政治的困難（0..1＝失敗時に撤回が選ばれにくく統制へ傾く強さ）。</summary>
        public readonly float retractDifficulty;

        public PlanningDriftParams(float ratchetRate, float coercionRate, float erosionRate,
            float knowledgeLossMax, float slopeMultiplier, float retractDifficulty)
        {
            this.ratchetRate = Mathf.Max(0f, ratchetRate);
            this.coercionRate = Mathf.Max(0f, coercionRate);
            this.erosionRate = Mathf.Max(0f, erosionRate);
            this.knowledgeLossMax = Mathf.Clamp01(knowledgeLossMax);
            this.slopeMultiplier = Mathf.Max(1f, slopeMultiplier);
            this.retractDifficulty = Mathf.Clamp01(retractDifficulty);
        }

        /// <summary>
        /// 既定＝ラチェット0.03・強制0.04・侵食0.05・知識損失最大0.6・坂道倍率3・撤回困難0.7。
        /// 撤回困難0.7＝<b>失敗の7割は撤回でなく追加統制へ傾く</b>＝介入が介入を呼ぶラチェットを数値に固定。
        /// </summary>
        public static PlanningDriftParams Default
            => new PlanningDriftParams(0.03f, 0.04f, 0.05f, 0.6f, 3f, 0.7f);
    }

    /// <summary>
    /// 計画経済ドリフトの純ロジック（ハイエク型・HAYK-1 #1541・『隷属への道』参考）。中央計画は一度始めると、
    /// <b>計画の失敗を補うためのさらなる介入を呼び、累積したラチェット（戻りにくい一方通行）が個人の自由を蝕んで
    /// 権威主義へ滑り落ちる</b>＝経済統制から始まり政治的自由の抑圧へ至る坂道（隷属への道）。計画化が進むほど強制
    /// （命令・統制）が増し、強制が自由を侵食し、計画が価格・分散知識を置き換えそこねて効率を失う（ハイエクの知識
    /// 問題）。計画化が臨界を超えると統制が一気に加速し、計画の失敗は「撤回」か「さらなる統制」かの岐路を生むが、
    /// 撤回は政治的に難しく統制へ傾く＝ラチェットが進む。
    /// <see cref="SocialProtectionRules"/>（市場圧力への保護ラチェット＝ポランニーの二重運動）とは別＝あちらは
    /// 市場の害から社会を守る保護の累積、こちらは<b>計画経済の累積介入ラチェットが権威主義を呼ぶドリフト</b>。
    /// 同EPIC HAYK の <see cref="SpontaneousOrderRules"/>（自生的秩序＝設計されず育つ市場・慣習の脆弱性）／
    /// AuthoritarianSelectionRules（逆淘汰＝計画機構で最悪の者が上に立つ）とも分担し、ここは介入ラチェット・
    /// 強制の増大・自由の侵食・権威主義圧力・知識代替の損失・坂道・隷属への道判定に専念する。
    /// すべて plain な float で受け渡す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PlanningDriftRules
    {
        /// <summary>権威主義圧力における計画化の重み（残りは強制の重み）。</summary>
        public const float AuthoritarianPlanningWeight = 0.5f;

        /// <summary>
        /// 介入ラチェット（dt後の planningLevel 0..1）＝計画の失敗がさらなる介入を呼ぶ累積。
        /// 増分＝ラチェット率×計画失敗×(1−撤回困難の逆＝統制傾斜)×dt。失敗を補う介入が積み上がり<b>戻りにくい</b>
        /// （計画失敗0なら増えない・失敗が大きいほど介入が積む）。撤回困難が高いほど失敗が確実に介入へ転化する。
        /// </summary>
        public static float InterventionRatchet(float planningLevel, float planFailure, float dt, PlanningDriftParams p)
        {
            float pl = Mathf.Clamp01(planningLevel);
            float fail = Mathf.Clamp01(planFailure);
            float step = Mathf.Max(0f, dt);

            // 撤回困難が高いほど失敗が「さらなる統制」へ転化＝ラチェットが効く。
            float ratchetForce = fail * p.retractDifficulty;
            float increment = p.ratchetRate * ratchetForce * step;
            return Mathf.Clamp01(pl + increment);
        }

        public static float InterventionRatchet(float planningLevel, float planFailure, float dt)
            => InterventionRatchet(planningLevel, planFailure, dt, PlanningDriftParams.Default);

        /// <summary>
        /// 強制の増大（dt後の coercion 0..1）＝計画化が進むほど命令・統制が増す（計画を貫くには強制が要る）。
        /// 増分＝強制率×計画化度×dt。計画化0なら強制は増えない＝<b>経済統制が政治的強制を呼ぶ</b>。
        /// </summary>
        public static float CoercionTick(float coercion, float planningLevel, float dt, PlanningDriftParams p)
        {
            float c = Mathf.Clamp01(coercion);
            float pl = Mathf.Clamp01(planningLevel);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(c + p.coercionRate * pl * step);
        }

        public static float CoercionTick(float coercion, float planningLevel, float dt)
            => CoercionTick(coercion, planningLevel, dt, PlanningDriftParams.Default);

        /// <summary>
        /// 自由の侵食（dt後の freedom 0..1）＝強制が個人の自由を蝕む（経済統制→政治的自由の抑圧）。
        /// 減少＝侵食率×強制度×dt。強制0なら自由は減らない＝<b>強制が高まるほど自由が削られる</b>（隷属への道の核）。
        /// </summary>
        public static float FreedomErosion(float freedom, float coercion, float dt, PlanningDriftParams p)
        {
            float fr = Mathf.Clamp01(freedom);
            float c = Mathf.Clamp01(coercion);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(fr - p.erosionRate * c * step);
        }

        public static float FreedomErosion(float freedom, float coercion, float dt)
            => FreedomErosion(freedom, coercion, dt, PlanningDriftParams.Default);

        /// <summary>
        /// 権威主義圧力（0..1）＝計画化と強制が権威主義を生む。計画化（重み0.5）と強制（重み0.5）の加重和。
        /// 両方が高いほど<b>権威主義へ滑り落ちる圧力が強い</b>（計画と強制が結びついて専制を呼ぶ）。
        /// 呼び出し側が <see cref="IsRoadToSerfdom"/> の判定や政体ドリフトへ渡す。
        /// </summary>
        public static float AuthoritarianPressure(float planningLevel, float coercion)
        {
            float pl = Mathf.Clamp01(planningLevel);
            float c = Mathf.Clamp01(coercion);
            return Mathf.Clamp01(AuthoritarianPlanningWeight * pl + (1f - AuthoritarianPlanningWeight) * c);
        }

        /// <summary>
        /// 知識代替の効率損失（0..1）＝計画が価格・分散知識を置き換えそこねる効率損失（ハイエクの知識問題）。
        /// 計画化度に比例して knowledgeLossMax まで損失が増す＝<b>計画化が進むほど現場の分散知識が失われ効率が落ちる</b>
        /// （上からの計画は無数の分散した知識を再現できない）。呼び出し側が産出倍率を (1−損失) で削る。
        /// </summary>
        public static float KnowledgeSubstitution(float planningLevel, PlanningDriftParams p)
        {
            float pl = Mathf.Clamp01(planningLevel);
            return Mathf.Clamp01(pl * p.knowledgeLossMax);
        }

        public static float KnowledgeSubstitution(float planningLevel)
            => KnowledgeSubstitution(planningLevel, PlanningDriftParams.Default);

        /// <summary>
        /// 坂道の統制加速倍率（≥1）＝計画化が臨界（threshold）を超えると一気に統制が加速する坂道。
        /// 閾値以下は1倍（普通の進行）、超えると超過分を 0..1 に正規化して 1〜slopeMultiplier へ線形補間
        /// ＝<b>臨界を越えた計画化は加速度的に統制を呼ぶ</b>（坂を転がり始めると止まらない）。
        /// 呼び出し側が <see cref="CoercionTick"/>・<see cref="InterventionRatchet"/> の進行へ掛ける。
        /// </summary>
        public static float SlipperySlope(float planningLevel, float threshold, PlanningDriftParams p)
        {
            float pl = Mathf.Clamp01(planningLevel);
            float th = Mathf.Clamp01(threshold);
            if (pl <= th) return 1f;

            float span = Mathf.Max(0.0001f, 1f - th);
            float over = Mathf.Clamp01((pl - th) / span);
            return Mathf.Lerp(1f, p.slopeMultiplier, over);
        }

        public static float SlipperySlope(float planningLevel, float threshold)
            => SlipperySlope(planningLevel, threshold, PlanningDriftParams.Default);

        /// <summary>
        /// 計画者のジレンマ＝計画の失敗に「撤回」か「さらなる統制」かの岐路（options は提示される選択肢）。
        /// 撤回（巻き戻し）は政治的に難しく、計画失敗が大きく撤回困難が高いほど<b>さらなる統制へ傾く</b>
        /// （失敗の責任を認めての撤退より、失敗を補う追加介入が選ばれる＝ラチェットの心理）。
        /// 統制傾斜＝計画失敗×撤回困難 が 0.5 を超えれば「さらなる統制」、超えなければ「撤回」。
        /// options に <see cref="PlannerChoice.撤回"/> が無ければ統制しか選べない。
        /// </summary>
        public static PlannerChoice PlannersDilemma(float planFailure, PlannerChoice[] options, PlanningDriftParams p)
        {
            float fail = Mathf.Clamp01(planFailure);

            bool canRetract = false;
            if (options != null)
            {
                for (int i = 0; i < options.Length; i++)
                {
                    if (options[i] == PlannerChoice.撤回) { canRetract = true; break; }
                }
            }
            if (!canRetract) return PlannerChoice.さらなる統制;

            // 失敗が大きく撤回が難しいほど統制へ傾く。
            float controlBias = fail * p.retractDifficulty;
            return controlBias > 0.5f ? PlannerChoice.さらなる統制 : PlannerChoice.撤回;
        }

        public static PlannerChoice PlannersDilemma(float planFailure, PlannerChoice[] options)
            => PlannersDilemma(planFailure, options, PlanningDriftParams.Default);

        /// <summary>
        /// 隷属への道（権威主義化）に入ったか（true＝坂を下り始めた）。権威主義圧力が threshold を超え、
        /// かつ残された自由が (1−threshold) を下回ると成立＝<b>計画の累積介入が強制を生み自由を蝕み権威主義へ
        /// 滑り落ちた</b>（経済統制から政治的自由の抑圧へ至る坂道に入った）。圧力が低いか自由が十分なら未到達。
        /// </summary>
        public static bool IsRoadToSerfdom(float authoritarianPressure, float freedom, float threshold)
        {
            float pressure = Mathf.Clamp01(authoritarianPressure);
            float fr = Mathf.Clamp01(freedom);
            float th = Mathf.Clamp01(threshold);
            return pressure > th && fr < 1f - th;
        }
    }
}
