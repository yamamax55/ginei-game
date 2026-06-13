using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 国家予算の純ロジック（国家予算の基盤・唯一の窓口）。歳出を分野へ配分し（予算編成）、歳入との均衡（黒字/赤字・緊縮）を
    /// 測り、各分野の<b>出資度</b>を実効値（基準非破壊）として返す＝軍事/建艦/研究は出力倍率、内政/社会保障/外交は加点。
    /// 歳出総額を <see cref="FiscalState"/>.baseExpenditure へ接続し、PB/債務（#161 <see cref="FiscalRules"/>）へ合流する。
    /// 細かい通貨/品目管理は持たない（タイクン化回避）。係数は #106 方針・実効値パターン。test-first。
    /// </summary>
    public static class BudgetRules
    {
        /// <summary>歳出分野の数（<see cref="BudgetCategory"/> の要素数）。</summary>
        public const int CategoryCount = 6;

        /// <summary>出資度の上限倍率（必要額の MaxFundingFactor 倍で頭打ち＝過剰投資の逓減）。</summary>
        public const float MaxFundingFactor = 2f;

        /// <summary>内政の出資度→安定度の加点幅（満額で0・過剰で+・不足で−の振幅）。</summary>
        public const float AdminStabilityScale = 10f;
        /// <summary>社会保障の出資度→希望(0..1)の加点幅。</summary>
        public const float WelfareHopeScale = 0.3f;
        /// <summary>外交の出資度→opinion の加点幅。</summary>
        public const float DiplomacyOpinionScale = 20f;

        // ===== 配分の読み書き =====

        /// <summary>分野の配分額（null/未知は0）。</summary>
        public static float Get(NationalBudget b, BudgetCategory c)
        {
            if (b == null) return 0f;
            switch (c)
            {
                case BudgetCategory.軍事: return b.military;
                case BudgetCategory.建艦: return b.shipbuilding;
                case BudgetCategory.内政: return b.administration;
                case BudgetCategory.社会保障: return b.welfare;
                case BudgetCategory.研究: return b.research;
                case BudgetCategory.外交: return b.diplomacy;
                default: return 0f;
            }
        }

        /// <summary>分野の配分額を設定（負は0でクランプ＝非負・基準非破壊の方針）。</summary>
        public static void Set(NationalBudget b, BudgetCategory c, float amount)
        {
            if (b == null) return;
            float v = Mathf.Max(0f, amount);
            switch (c)
            {
                case BudgetCategory.軍事: b.military = v; break;
                case BudgetCategory.建艦: b.shipbuilding = v; break;
                case BudgetCategory.内政: b.administration = v; break;
                case BudgetCategory.社会保障: b.welfare = v; break;
                case BudgetCategory.研究: b.research = v; break;
                case BudgetCategory.外交: b.diplomacy = v; break;
            }
        }

        /// <summary>分野の配分額を増減（結果は非負でクランプ）。</summary>
        public static void Add(NationalBudget b, BudgetCategory c, float delta)
            => Set(b, c, Get(b, c) + delta);

        /// <summary>歳出総額＝全分野の合計（負配分は0扱い＝FiscalState.baseExpenditure に相当）。</summary>
        public static float Total(NationalBudget b)
        {
            if (b == null) return 0f;
            return Mathf.Max(0f, b.military) + Mathf.Max(0f, b.shipbuilding) + Mathf.Max(0f, b.administration)
                 + Mathf.Max(0f, b.welfare) + Mathf.Max(0f, b.research) + Mathf.Max(0f, b.diplomacy);
        }

        /// <summary>分野シェア 0..1＝その分野の配分/歳出総額（総額0は0＝優先度の重み）。</summary>
        public static float Share(NationalBudget b, BudgetCategory c)
        {
            float total = Total(b);
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Max(0f, Get(b, c)) / total);
        }

        // ===== 歳入との均衡 =====

        /// <summary>収支＝歳入−歳出総額（黒字＞0／赤字＜0）。</summary>
        public static float Balance(NationalBudget b, float revenue)
            => revenue - Total(b);

        public static bool IsDeficit(NationalBudget b, float revenue) => Balance(b, revenue) < 0f;
        public static bool IsSurplus(NationalBudget b, float revenue) => Balance(b, revenue) > 0f;

        /// <summary>均衡予算か（収支の絶対値が許容誤差以内）。</summary>
        public static bool IsBalanced(NationalBudget b, float revenue, float tolerance = 1e-3f)
            => Mathf.Abs(Balance(b, revenue)) <= Mathf.Max(0f, tolerance);

        /// <summary>赤字率 0..1＝不足分/歳出総額（黒字や歳出0は0）。緊縮の必要度。</summary>
        public static float DeficitRatio(NationalBudget b, float revenue)
        {
            float total = Total(b);
            if (total <= 0f) return 0f;
            float shortfall = total - Mathf.Max(0f, revenue);
            return Mathf.Clamp01(shortfall / total);
        }

        // ===== 予算編成（配分の操作） =====

        /// <summary>歳出総額を target へ比例縮尺（緊縮/積極＝全分野を同率で増減・total0 は無変化＝シェア保存）。</summary>
        public static void ScaleToTotal(NationalBudget b, float targetTotal)
        {
            if (b == null) return;
            float total = Total(b);
            if (total <= 0f) return;
            float k = Mathf.Max(0f, targetTotal) / total;
            b.military = Mathf.Max(0f, b.military) * k;
            b.shipbuilding = Mathf.Max(0f, b.shipbuilding) * k;
            b.administration = Mathf.Max(0f, b.administration) * k;
            b.welfare = Mathf.Max(0f, b.welfare) * k;
            b.research = Mathf.Max(0f, b.research) * k;
            b.diplomacy = Mathf.Max(0f, b.diplomacy) * k;
        }

        /// <summary>歳入を超える歳出を歳入まで切り詰める（緊縮＝比例縮小・シェア保存）。実際に切ったら true。</summary>
        public static bool CapToRevenue(NationalBudget b, float revenue)
        {
            if (b == null) return false;
            float cap = Mathf.Max(0f, revenue);
            if (Total(b) <= cap) return false;
            ScaleToTotal(b, cap);
            return true;
        }

        /// <summary>
        /// 重み（分野ごと0..）に比例して pool を配分し予算を組む（予算編成の自動化）。重み総和&lt;=0 は均等配分。
        /// weights は <see cref="BudgetCategory"/> の序数で対応（短い/null は不足分0扱い）。
        /// </summary>
        public static void AllocateByWeights(NationalBudget b, float pool, float[] weights)
        {
            if (b == null) return;
            pool = Mathf.Max(0f, pool);
            float sum = 0f;
            if (weights != null)
                for (int i = 0; i < weights.Length && i < CategoryCount; i++)
                    sum += Mathf.Max(0f, weights[i]);
            for (int i = 0; i < CategoryCount; i++)
            {
                float w = (weights != null && i < weights.Length) ? Mathf.Max(0f, weights[i]) : 0f;
                float share = (sum > 0f) ? w / sum : 1f / CategoryCount;
                Set(b, (BudgetCategory)i, pool * share);
            }
        }

        // ===== 実効値（出資度・基準非破壊） =====

        /// <summary>出資度＝配分/必要額（0..<see cref="MaxFundingFactor"/>。need≤0 は満額1＝過不足なし）。実効値の核。</summary>
        public static float FundingFactor(float allocated, float need)
        {
            if (need <= 0f) return 1f;
            return Mathf.Clamp(Mathf.Max(0f, allocated) / need, 0f, MaxFundingFactor);
        }

        /// <summary>分野の出資度（その分野の配分 vs need）。</summary>
        public static float FundingFactor(NationalBudget b, BudgetCategory c, float need)
            => FundingFactor(Get(b, c), need);

        /// <summary>不足ペナルティ 0..1＝満額に対する不足割合（満額以上は0）。建艦/研究/軍事の減速に。</summary>
        public static float ShortfallPenalty(float allocated, float need)
            => Mathf.Clamp01(1f - FundingFactor(allocated, need));

        /// <summary>軍事の即応倍率（出資度＝0..MaxFundingFactor）。戦闘/維持へ係数#106。</summary>
        public static float MilitaryReadinessFactor(NationalBudget b, float need)
            => FundingFactor(b, BudgetCategory.軍事, need);

        /// <summary>建艦の生産倍率（出資度）。`ShipyardRules` の productionFactor へ掛ける想定。</summary>
        public static float ShipbuildingFactor(NationalBudget b, float need)
            => FundingFactor(b, BudgetCategory.建艦, need);

        /// <summary>研究の出力倍率（出資度）。`ResearchRules` の output へ掛ける想定。</summary>
        public static float ResearchOutputFactor(NationalBudget b, float need)
            => FundingFactor(b, BudgetCategory.研究, need);

        /// <summary>内政の安定度加点（±<see cref="AdminStabilityScale"/>＝満額で0・過剰で+・不足で−）。`GovernanceRules` の目標へ。</summary>
        public static float AdministrationStabilityBonus(NationalBudget b, float need)
            => (FundingFactor(b, BudgetCategory.内政, need) - 1f) * AdminStabilityScale;

        /// <summary>社会保障の希望加点（±<see cref="WelfareHopeScale"/>）。希望#852 のドリフトへ（高税負担の対）。</summary>
        public static float WelfareHopeBonus(NationalBudget b, float need)
            => (FundingFactor(b, BudgetCategory.社会保障, need) - 1f) * WelfareHopeScale;

        /// <summary>外交の opinion 加点（±<see cref="DiplomacyOpinionScale"/>）。#189 へ。</summary>
        public static float DiplomacyOpinionBonus(NationalBudget b, float need)
            => (FundingFactor(b, BudgetCategory.外交, need) - 1f) * DiplomacyOpinionScale;

        // ===== FiscalState 接続 =====

        /// <summary>予算の歳出総額を <see cref="FiscalState"/>.baseExpenditure へ反映（PB/債務 #161 へ接続）。null 安全。</summary>
        public static void ApplyToFiscalState(NationalBudget b, FiscalState s)
        {
            if (s == null) return;
            s.baseExpenditure = Total(b);
        }
    }
}
