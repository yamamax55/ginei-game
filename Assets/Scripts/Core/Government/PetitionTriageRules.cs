using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 稟議トリアージの調整係数（稟議の優先順位・MEYASU #1296）。決裁者が決裁待ちの<b>背後の山（backlog）</b>を
    /// 抱えたとき、どれから捌くかの重み付けと滞留（aging）の減衰を司る。締切・滞留時間は game-秒。
    /// </summary>
    public readonly struct PetitionTriageParams
    {
        /// <summary>出自による緊急度の基礎重み（諮問＝上が箱へ問い返した＝相対的に急ぐ）。</summary>
        public readonly float inquiryUrgency;
        /// <summary>箱の格による重み（国王箱＝最上位の決裁＝重い）。</summary>
        public readonly float kingBoxWeight;
        /// <summary>箱の格による重み（政治家箱）。</summary>
        public readonly float politicianBoxWeight;
        /// <summary>箱の格による重み（地方箱）。</summary>
        public readonly float regionBoxWeight;
        /// <summary>ネームド人物が起案した稟議への加点（drafterId!=0＝顔の見える稟議は埋もれにくい）。</summary>
        public readonly float sponsoredBonus;
        /// <summary>再浮上した稟議への加点（黙殺後に正しさが判明＝今こそ捌くべき）。</summary>
        public readonly float resurfacedBonus;
        /// <summary>歪んで伝わった稟議への減点（内容が劣化＝信頼が落ちる）。</summary>
        public readonly float distortedPenalty;
        /// <summary>滞留が古いほど優先度を底上げする係数（飢餓回避＝古参を埋もれさせない）。</summary>
        public readonly float ageBoostPerSecond;
        /// <summary>滞留がこの game-秒を超えると陳腐化（stale）＝価値が失われ始める。</summary>
        public readonly float staleDeadline;
        /// <summary>陳腐化後、滞留の超過ぶんで価値が減衰する係数（per-秒）。</summary>
        public readonly float stalingDecayPerSecond;

        public PetitionTriageParams(float inquiryUrgency, float kingBoxWeight, float politicianBoxWeight,
            float regionBoxWeight, float sponsoredBonus, float resurfacedBonus, float distortedPenalty,
            float ageBoostPerSecond, float staleDeadline, float stalingDecayPerSecond)
        {
            this.inquiryUrgency = inquiryUrgency;
            this.kingBoxWeight = kingBoxWeight;
            this.politicianBoxWeight = politicianBoxWeight;
            this.regionBoxWeight = regionBoxWeight;
            this.sponsoredBonus = sponsoredBonus;
            this.resurfacedBonus = resurfacedBonus;
            this.distortedPenalty = distortedPenalty;
            this.ageBoostPerSecond = ageBoostPerSecond;
            this.staleDeadline = staleDeadline;
            this.stalingDecayPerSecond = stalingDecayPerSecond;
        }

        public static PetitionTriageParams Default => new PetitionTriageParams(
            inquiryUrgency: 0.5f,
            kingBoxWeight: 1f,
            politicianBoxWeight: 0.7f,
            regionBoxWeight: 0.5f,
            sponsoredBonus: 0.4f,
            resurfacedBonus: 0.6f,
            distortedPenalty: 0.3f,
            ageBoostPerSecond: 0.01f,
            staleDeadline: 120f,
            stalingDecayPerSecond: 0.005f);
    }

    /// <summary>
    /// 稟議トリアージ（優先順位付け）の唯一の窓口（稟議の優先順位・MEYASU #1296）。決裁者が決裁待ちの<b>背後の山</b>を
    /// 抱えたとき、どの一件から捌くかを<b>緊急度 × 起案者の格 × 箱の格 × 滞留</b>で重み付けする。状態遷移・伝播・執行は
    /// 既存窓口（<see cref="WorkflowRules"/>／<see cref="PetitionFlowRules"/>／<see cref="RingiPipeline"/>）へ委譲し、
    /// ここは<b>並び替えと滞留の評価だけ</b>を持つ（並行新設しない＝状態機械を作り直さない）。
    /// 重みは <see cref="Petition"/> の<b>既存フィールド</b>（origin/box/drafterId/distorted/status/vindicated）から導く＝Petition は変更しない。
    /// 滞留時間（ageSeconds）は呼び出し側が渡す（<see cref="DecisionTriageRules"/> が締切を渡すのと同じ流儀＝Petition は時刻を持たない）。
    /// 決定論・有界・null/空安全。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PetitionTriageRules
    {
        /// <summary>箱の格の重み（国王＞政治家＞地方）。</summary>
        public static float BoxWeight(BoxKind box, PetitionTriageParams p)
        {
            switch (box)
            {
                case BoxKind.国王: return p.kingBoxWeight;
                case BoxKind.政治家: return p.politicianBoxWeight;
                case BoxKind.地方: return p.regionBoxWeight;
                default: return p.regionBoxWeight;
            }
        }

        /// <summary>出自による緊急度（諮問＝上が問い返した＝急ぐ／建白・注入＝0）。</summary>
        public static float OriginUrgency(PetitionOrigin origin, PetitionTriageParams p)
            => origin == PetitionOrigin.諮問 ? p.inquiryUrgency : 0f;

        /// <summary>
        /// この稟議の優先度スコア（≥0・大きいほど先に捌く）。<b>箱の格＋出自の緊急度＋起案者の格＋再浮上加点＋滞留底上げ
        /// −歪みの減点</b>。陳腐化（staleDeadline 超）後は超過ぶんで価値が逓減する。決定論・0 で下限クランプ。
        /// </summary>
        public static float PriorityScore(Petition pet, float ageSeconds, PetitionTriageParams p)
        {
            if (pet == null) return 0f;
            float age = ageSeconds < 0f ? 0f : ageSeconds;

            float score = BoxWeight(pet.box, p);
            score += OriginUrgency(pet.origin, p);
            if (pet.drafterId != 0) score += p.sponsoredBonus;                 // 顔の見える稟議
            if (pet.vindicated || pet.status == PetitionStatus.再浮上) score += p.resurfacedBonus; // 正しさが判明
            if (pet.distorted) score -= p.distortedPenalty;                    // 内容が劣化

            // 滞留の底上げ（飢餓回避）＝古いものを埋もれさせない
            score += age * p.ageBoostPerSecond;

            // 陳腐化＝古すぎる稟議は価値が逓減（鮮度切れ）
            float decay = StalingDecay(age, p);
            score *= decay;

            return score < 0f ? 0f : score;
        }

        public static float PriorityScore(Petition pet, float ageSeconds)
            => PriorityScore(pet, ageSeconds, PetitionTriageParams.Default);

        /// <summary>陳腐化したか＝滞留が staleDeadline を超えた（鮮度切れ）。staleDeadline≤0 は無期限（常に false）。</summary>
        public static bool IsStale(float ageSeconds, PetitionTriageParams p)
            => p.staleDeadline > 0f && ageSeconds >= p.staleDeadline;

        public static bool IsStale(float ageSeconds) => IsStale(ageSeconds, PetitionTriageParams.Default);

        /// <summary>
        /// 陳腐化による価値の残存率（1=新鮮〜0+＝鮮度切れで逓減）。staleDeadline までは 1.0、超過ぶん×stalingDecayPerSecond
        /// で 1 から引く（最低 0.05 でゼロ割れを防ぐ＝完全消滅はしない）。staleDeadline≤0 は無期限＝常に 1.0。
        /// </summary>
        public static float StalingDecay(float ageSeconds, PetitionTriageParams p)
        {
            if (p.staleDeadline <= 0f) return 1f;
            float over = ageSeconds - p.staleDeadline;
            if (over <= 0f) return 1f;
            return Mathf.Clamp(1f - over * p.stalingDecayPerSecond, 0.05f, 1f);
        }

        /// <summary>
        /// 決裁待ちの山を捌く順に並べる（高スコア優先・<b>決定論で安定</b>＝同スコアは age（古い先）→ id（小さい先）で割る）。
        /// null 要素は除外。null/空入力は空リストを返す（null は返さない）。元のコレクションは変更しない（新規リストを返す）。
        /// 各稟議の滞留時間は <paramref name="ageOf"/> で引く（null なら全件 age=0＝箱の格/起案者だけで並ぶ）。
        /// </summary>
        public static List<Petition> Order(IEnumerable<Petition> backlog,
            System.Func<Petition, float> ageOf, PetitionTriageParams p)
        {
            var list = new List<Petition>();
            if (backlog == null) return list;
            foreach (var pet in backlog)
                if (pet != null) list.Add(pet);

            // スコアを一度だけ計算してキャッシュ（並べ替え中の再計算を避ける＝決定論・性能）。
            int n = list.Count;
            var score = new float[n];
            var age = new float[n];
            for (int i = 0; i < n; i++)
            {
                age[i] = ageOf != null ? ageOf(list[i]) : 0f;
                score[i] = PriorityScore(list[i], age[i], p);
            }

            // インデックスで安定ソート（高スコア先→同点は古い先→同滞留は id 小さい先→なお同じなら投入順）。
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            System.Array.Sort(idx, (a, b) =>
            {
                int c = score[b].CompareTo(score[a]);          // 高スコア先
                if (c != 0) return c;
                c = age[b].CompareTo(age[a]);                  // 古い先（長く待った者）
                if (c != 0) return c;
                c = list[a].id.CompareTo(list[b].id);          // id 小さい先
                if (c != 0) return c;
                return a.CompareTo(b);                         // 投入順（安定）
            });

            var ordered = new List<Petition>(n);
            for (int i = 0; i < n; i++) ordered.Add(list[idx[i]]);
            return ordered;
        }

        public static List<Petition> Order(IEnumerable<Petition> backlog, System.Func<Petition, float> ageOf)
            => Order(backlog, ageOf, PetitionTriageParams.Default);

        /// <summary>滞留時間を考えない単純版（箱の格＋起案者の格＋出自だけで並べる）。</summary>
        public static List<Petition> Order(IEnumerable<Petition> backlog)
            => Order(backlog, null, PetitionTriageParams.Default);

        /// <summary>
        /// 山の中で最優先の一件（無ければ null）。<see cref="Order"/> の先頭と一致する＝決裁者が「次に捌くべき」を引く窓口。
        /// </summary>
        public static Petition Next(IEnumerable<Petition> backlog, System.Func<Petition, float> ageOf,
            PetitionTriageParams p)
        {
            var ordered = Order(backlog, ageOf, p);
            return ordered.Count > 0 ? ordered[0] : null;
        }

        public static Petition Next(IEnumerable<Petition> backlog, System.Func<Petition, float> ageOf)
            => Next(backlog, ageOf, PetitionTriageParams.Default);
    }
}
