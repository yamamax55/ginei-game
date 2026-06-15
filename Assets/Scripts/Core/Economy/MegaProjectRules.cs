using UnityEngine;

namespace Ginei
{
    /// <summary>国家的大事業の種別（PIL-1 #1090）。完成効果は <see cref="MegaProjectRules.CompletionEffect"/> で種別ごとに決まる。</summary>
    public enum MegaProjectKind
    {
        要塞,           // 戦略要衝の固定防御（戦略価値）
        大シップヤード, // 建艦力の恒久増強（#884 造船へ係数として配線予定）
        遷都,           // 首都移転（統合・正統性）
        記念碑          // 王朝の威信（正統性・希望 #852）
    }

    /// <summary>大事業の建設段階（PIL-1 #1090）。進捗 0..1 を <see cref="MegaProjectRules.StageOf"/> で写像する。</summary>
    public enum ProjectStage
    {
        基礎, // 着工〜土台
        構造, // 構造体の建ち上げ
        完成  // 供用開始＝完成効果が発生
    }

    /// <summary>
    /// 世代を跨ぐ国家的大事業（PIL-1 #1090）。種別×段階建設×数十年スケールの純データ。
    /// 決断は「着工／中断・再開／頓挫」のみ＝建築マイクロ禁止。解決は <see cref="MegaProjectRules"/>（static）が唯一の窓口。
    /// 暦への接続（CalendarDispatcher）・財政（FiscalRules）・継承（SuccessionRules）は配線時＝ここは純ロジック。
    /// </summary>
    public class MegaProject
    {
        public int id;
        public Faction faction;
        public MegaProjectKind kind = MegaProjectKind.記念碑;
        public string projectName = "";

        /// <summary>発起人（人物id・LIFE #151 想定）。死亡時は <see cref="MegaProjectRules.SuccessionOnFounderDeath"/> で頓挫判定。</summary>
        public int founderId;

        public float progress;   // 進捗 0..1（1で完成）
        public bool suspended;   // 中断中（資金難の決断＝支出も進捗も止まる）
        public bool abandoned;   // 頓挫（不可逆。発起人死亡×低制度化など）

        public MegaProject() { }

        public MegaProject(int id, Faction faction, MegaProjectKind kind, string projectName = "", int founderId = 0)
        {
            this.id = id;
            this.faction = faction;
            this.kind = kind;
            this.projectName = projectName ?? "";
            this.founderId = founderId;
        }

        public bool IsComplete => progress >= 1f;
        /// <summary>進捗中か（中断・頓挫・完成のいずれでもない）。</summary>
        public bool IsActive => !suspended && !abandoned && !IsComplete;
        public ProjectStage Stage => MegaProjectRules.StageOf(progress);
    }

    /// <summary>大事業の調整係数（PIL-1 #1090）。既定値は <see cref="Default"/> に集約（マジックナンバー禁止）。</summary>
    public readonly struct MegaProjectParams
    {
        /// <summary>満額資金（funding=1）で完成までにかかる基準時間（戦略秒・種別倍率1.0のとき）。</summary>
        public readonly float baseBuildSeconds;
        /// <summary>継続支出の基準（資金/戦略秒・種別倍率1.0のとき）。財政ドレインの単価。</summary>
        public readonly float upkeepPerSecond;
        /// <summary>段階「構造」へ入る進捗の閾値（これ未満は「基礎」）。</summary>
        public readonly float structureThreshold;
        /// <summary>発起人死亡時の頓挫リスク係数。頓挫確率＝(1-制度化)×これ（#812 の式の事業適用）。</summary>
        public readonly float orphanRiskFactor;
        /// <summary>頓挫・放棄時に投下資本のうち回収できる比率（残りが埋没費用）。</summary>
        public readonly float sunkCostRecovery;

        public MegaProjectParams(float baseBuildSeconds, float upkeepPerSecond, float structureThreshold, float orphanRiskFactor, float sunkCostRecovery)
        {
            this.baseBuildSeconds = Mathf.Max(1f, baseBuildSeconds);
            this.upkeepPerSecond = Mathf.Max(0f, upkeepPerSecond);
            this.structureThreshold = Mathf.Clamp01(structureThreshold);
            this.orphanRiskFactor = Mathf.Clamp01(orphanRiskFactor);
            this.sunkCostRecovery = Mathf.Clamp01(sunkCostRecovery);
        }

        public static MegaProjectParams Default => new MegaProjectParams(
            baseBuildSeconds: 3600f,
            upkeepPerSecond: 1f,
            structureThreshold: 0.35f,
            orphanRiskFactor: 0.8f,
            sunkCostRecovery: 0.2f);
    }

    /// <summary>
    /// 世代を跨ぐ国家的大事業の純ロジック（PIL-1 #1090・唯一の窓口・test-first）。
    /// 種別（要塞/大シップヤード/遷都/記念碑）×段階建設（基礎→構造→完成）×数十年スケール。
    /// 財政ドレイン（<see cref="UpkeepDrain"/>＝資金難で中断/再開の決断）・完成効果（<see cref="CompletionEffect"/>＝種別別）・
    /// 事業の継承（<see cref="SuccessionOnFounderDeath"/>＝制度化が低いと発起人と共に頓挫＝Organization #812 の式の事業適用・roll決定論）。
    /// 暦Tick（CalendarDispatcher）・財政（FiscalRules）・人物死亡（SuccessionRules/LifecycleRules）への接続は配線時＝ここは純ロジック（Core層・Game型不参照）。
    /// 建築マイクロ禁止＝決断は着工/中断/方針のみで、進捗は資金比例の時間積分。
    /// </summary>
    public static class MegaProjectRules
    {
        // ===== 種別の規模倍率（建設時間・継続支出に共通で掛かる。const で調整可） =====
        public const float ScaleFortress = 1.5f;  // 要塞＝重防御で高コスト
        public const float ScaleShipyard = 1.2f;  // 大シップヤード
        public const float ScaleCapital = 2f;     // 遷都＝最大規模
        public const float ScaleMonument = 1f;    // 記念碑＝基準

        // ===== 種別の完成効果値（意味は種別ごと。配線先で係数 #106 として使う） =====
        public const float EffectFortress = 0.3f;   // 要塞＝戦略価値（防衛ボーナス）
        public const float EffectShipyard = 0.5f;   // 大シップヤード＝建艦力増（buildPower +50% 想定・#884）
        public const float EffectCapital = 0.2f;    // 遷都＝統合・正統性
        public const float EffectMonument = 0.15f;  // 記念碑＝正統性・希望（#852）

        /// <summary>種別の規模倍率（建設時間・継続支出に掛かる）。</summary>
        public static float KindScale(MegaProjectKind kind)
        {
            switch (kind)
            {
                case MegaProjectKind.要塞: return ScaleFortress;
                case MegaProjectKind.大シップヤード: return ScaleShipyard;
                case MegaProjectKind.遷都: return ScaleCapital;
                default: return ScaleMonument;
            }
        }

        /// <summary>進捗(0..1)→建設段階。structureThreshold 未満＝基礎／1未満＝構造／1以上＝完成。</summary>
        public static ProjectStage StageOf(float progress, MegaProjectParams p)
        {
            float v = Mathf.Clamp01(progress);
            if (v >= 1f) return ProjectStage.完成;
            if (v >= p.structureThreshold) return ProjectStage.構造;
            return ProjectStage.基礎;
        }

        public static ProjectStage StageOf(float progress) => StageOf(progress, MegaProjectParams.Default);

        /// <summary>
        /// 建設を時間積分で進める（建築マイクロ禁止＝資金配分 funding 0..1 だけが決断）。
        /// 進捗増分＝funding×dt÷(基準時間×種別倍率)。中断/頓挫/完成済み・funding=0 は進まない。
        /// 加えた進捗を返す（progress は 1 にクランプ）。
        /// </summary>
        public static float ProgressTick(MegaProject project, float funding, float dt, MegaProjectParams p)
        {
            if (project == null || !project.IsActive) return 0f;
            float f = Mathf.Clamp01(funding);
            float t = Mathf.Max(0f, dt);
            if (f <= 0f || t <= 0f) return 0f;

            float delta = f * t / (p.baseBuildSeconds * KindScale(project.kind));
            float before = project.progress;
            project.progress = Mathf.Clamp01(project.progress + delta);
            return project.progress - before;
        }

        public static float ProgressTick(MegaProject project, float funding, float dt)
            => ProgressTick(project, funding, dt, MegaProjectParams.Default);

        /// <summary>
        /// 継続支出（資金/戦略秒）＝財政ドレイン。建設中のみ発生し、中断/頓挫/完成済みは 0。
        /// 配線時は FiscalRules の歳出へ加算する想定＝「資金難なら中断する」決断の根拠。
        /// </summary>
        public static float UpkeepDrain(MegaProject project, MegaProjectParams p)
        {
            if (project == null || !project.IsActive) return 0f;
            return p.upkeepPerSecond * KindScale(project.kind);
        }

        public static float UpkeepDrain(MegaProject project) => UpkeepDrain(project, MegaProjectParams.Default);

        /// <summary>中断（資金難の決断）。支出も進捗も止まる。頓挫・完成済みには効かない。</summary>
        public static void Suspend(MegaProject project)
        {
            if (project == null || project.abandoned || project.IsComplete) return;
            project.suspended = true;
        }

        /// <summary>再開。頓挫は不可逆＝再開できない。</summary>
        public static void Resume(MegaProject project)
        {
            if (project == null || project.abandoned) return;
            project.suspended = false;
        }

        /// <summary>
        /// 発起人死亡時の継承判定（Organization #812 の式の事業適用・roll 決定論）。
        /// 頓挫確率＝(1-制度化)×orphanRiskFactor。roll∈[0,1) がそれ未満なら頓挫（abandoned=true・不可逆）。
        /// 制度化が高い事業は発起人を超えて続く＝カリスマの日常化。頓挫したか（true=頓挫）を返す。
        /// </summary>
        public static bool SuccessionOnFounderDeath(MegaProject project, float institutionalization, float roll, MegaProjectParams p)
        {
            if (project == null) return false;
            if (project.abandoned || project.IsComplete) return project.abandoned;

            float inst = Mathf.Clamp01(institutionalization);
            float abandonChance = (1f - inst) * p.orphanRiskFactor;
            bool abandons = Mathf.Clamp01(roll) < abandonChance;
            if (abandons) project.abandoned = true;
            return abandons;
        }

        public static bool SuccessionOnFounderDeath(MegaProject project, float institutionalization, float roll)
            => SuccessionOnFounderDeath(project, institutionalization, roll, MegaProjectParams.Default);

        /// <summary>
        /// 頓挫・放棄時の埋没費用（投下資本のうち失う比率 0..1）＝進捗×(1-回収率)。
        /// 進んだ事業ほど捨てる痛みが大きい＝「続けるか畳むか」の決断材料。
        /// </summary>
        public static float AbandonmentSunkCost(float progress, MegaProjectParams p)
            => Mathf.Clamp01(progress) * (1f - p.sunkCostRecovery);

        public static float AbandonmentSunkCost(float progress)
            => AbandonmentSunkCost(progress, MegaProjectParams.Default);

        /// <summary>
        /// 完成効果の大きさ（種別ごと・意味は種別で異なる）。要塞=戦略価値／大シップヤード=建艦力増（#884 の
        /// productionFactor へ乗算想定）／遷都=統合・正統性／記念碑=正統性・希望（#852）。未完成は配線側で 0 扱い。
        /// </summary>
        public static float CompletionEffect(MegaProjectKind kind)
        {
            switch (kind)
            {
                case MegaProjectKind.要塞: return EffectFortress;
                case MegaProjectKind.大シップヤード: return EffectShipyard;
                case MegaProjectKind.遷都: return EffectCapital;
                default: return EffectMonument;
            }
        }
    }
}
