using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 資産相続の様式（#1038）。<see cref="SuccessionLaw"/>（爵位/君主位の継ぎ方）とは別＝<b>資産・封土</b>の分け方。
    /// 長子相続は家産を1人へ集中させ家を保ち、分割相続は均等割で世代ごとに散逸させる（ガベルカインドの呪い）。
    /// </summary>
    public enum InheritancePattern
    {
        長子相続, // 長男が大半を継ぐ（家産集中＝家を保つ）
        分割相続, // 全相続人で均等割（世代ごとに細る＝散逸）
        指名相続, // 当主が指名した1人が総取り
    }

    /// <summary>資産相続の調整値（長子相続で長男が取る比率・争いの基準値など）。top-level・<see cref="Default"/>。</summary>
    public readonly struct InheritanceParams
    {
        /// <summary>長子相続で長男（第一相続人）が取る比率（残りを他の相続人で均等割）。</summary>
        public readonly float primogenitureMainShare;
        /// <summary>相続争いの基準リスク（相続人が複数いるとき）。</summary>
        public readonly float baseDisputeRisk;
        /// <summary>相続人1人増えるごとの争いリスク加算。</summary>
        public readonly float perHeirDisputeRisk;
        /// <summary>分割相続での争いリスク加算（取り分が割れて揉めやすい）。</summary>
        public readonly float partitionDisputeRisk;
        /// <summary>取り分の曖昧さ（assetClarity の低さ）が争いをどれだけ増やすか。</summary>
        public readonly float ambiguityDisputeWeight;

        public InheritanceParams(float primogenitureMainShare, float baseDisputeRisk,
            float perHeirDisputeRisk, float partitionDisputeRisk, float ambiguityDisputeWeight)
        {
            this.primogenitureMainShare = primogenitureMainShare;
            this.baseDisputeRisk = baseDisputeRisk;
            this.perHeirDisputeRisk = perHeirDisputeRisk;
            this.partitionDisputeRisk = partitionDisputeRisk;
            this.ambiguityDisputeWeight = ambiguityDisputeWeight;
        }

        /// <summary>既定値：長男0.8取り・基準争い0.1・1人あたり0.05・分割加算0.3・曖昧さ重み0.4。</summary>
        public static InheritanceParams Default => new InheritanceParams(0.8f, 0.1f, 0.05f, 0.3f, 0.4f);
    }

    /// <summary>
    /// 相続・継承の純ロジック（#1038・資産の世代継承）。人物の死後、資産・封土が相続様式に従って継承される様を
    /// 決定論的に解く。<b>長子相続は家産を1人へ集中させ家を保ち、分割相続は均等割で世代ごとに指数的に細る</b>＝
    /// 「相続法が家の盛衰を決める」を式に出す。乱数なし・全入力クランプ・配列は手書きループ。
    /// 分担：<see cref="SuccessionLawRules"/>＝爵位/君主位の継承法（誰が当主になるか）／<see cref="FeudalRules"/>＝封土の
    /// 軍役・反乱／<see cref="RedistributionRules"/>＝税の階級別再分配。本クラスは<b>資産・封土そのものの取り分と散逸</b>
    /// （相続税は世代継承時の国庫取り分＝富の再分配の入口）を扱う。test-first。
    /// </summary>
    public static class InheritanceRules
    {
        /// <summary>
        /// 相続人ごとの取り分（合計＝<paramref name="totalAssets"/>）を配列で返す。長子相続＝長男が大半（残りを他で均等割）／
        /// 分割相続＝全員均等（資産の散逸）／指名相続＝指名者（index0）が総取り。相続人0以下は空配列。
        /// </summary>
        public static float[] HeirShares(float totalAssets, int heirCount, InheritancePattern pattern, InheritanceParams p)
        {
            float total = Mathf.Max(0f, totalAssets);
            int n = Mathf.Max(0, heirCount);
            if (n == 0) return new float[0];
            var shares = new float[n];
            if (n == 1)
            {
                shares[0] = total; // 相続人1人＝様式に依らず総取り
                return shares;
            }
            switch (pattern)
            {
                case InheritancePattern.分割相続:
                {
                    float each = total / n; // 均等割＝家産が割れて散る
                    for (int i = 0; i < n; i++) shares[i] = each;
                    break;
                }
                case InheritancePattern.指名相続:
                    shares[0] = total; // 指名者が総取り
                    break;
                case InheritancePattern.長子相続:
                default:
                {
                    float main = Mathf.Clamp01(p.primogenitureMainShare);
                    shares[0] = total * main; // 長男が大半
                    float rest = total * (1f - main);
                    float eachRest = rest / (n - 1);
                    for (int i = 1; i < n; i++) shares[i] = eachRest; // 残りを弟妹で均等割
                    break;
                }
            }
            return shares;
        }

        public static float[] HeirShares(float totalAssets, int heirCount, InheritancePattern pattern)
            => HeirShares(totalAssets, heirCount, pattern, InheritanceParams.Default);

        /// <summary>
        /// 世代を経た資産集中度（0..1・初代＝1.0）を返す。長子相続は1人へ集中し続け家を保つ（≒1.0）が、分割相続は
        /// 世代ごとに最大の取り分の比率しか保てず細る＝ガベルカインドの呪い。<paramref name="generations"/>は経過世代数。
        /// </summary>
        public static float DynastyConcentration(InheritancePattern pattern, int generations, int heirsPerGen, InheritanceParams p)
        {
            int gens = Mathf.Max(0, generations);
            if (gens == 0) return 1f;
            int heirs = Mathf.Max(1, heirsPerGen);
            // 1世代あたり、家の本流が保つ取り分の比率
            float retain;
            switch (pattern)
            {
                case InheritancePattern.分割相続:
                    retain = 1f / heirs; // 均等割＝本流は 1/n しか残らない（散逸）
                    break;
                case InheritancePattern.指名相続:
                case InheritancePattern.長子相続:
                default:
                    retain = heirs <= 1 ? 1f : Mathf.Clamp01(p.primogenitureMainShare); // 長男が大半を保つ
                    break;
            }
            float c = 1f;
            for (int g = 0; g < gens; g++) c *= retain; // 世代ごとに本流の取り分を掛け合わせる
            return Mathf.Clamp01(c);
        }

        public static float DynastyConcentration(InheritancePattern pattern, int generations, int heirsPerGen)
            => DynastyConcentration(pattern, generations, heirsPerGen, InheritanceParams.Default);

        /// <summary>
        /// 相続税（国庫の取り分）を返す。基礎控除 <paramref name="exemption"/> を超えた分にのみ <paramref name="taxRate"/> を課す＝
        /// 世代継承のたびに富が国庫へ再分配される。資産が控除以下なら0。
        /// </summary>
        public static float InheritanceTax(float estateValue, float taxRate, float exemption)
        {
            float estate = Mathf.Max(0f, estateValue);
            float exempt = Mathf.Max(0f, exemption);
            float rate = Mathf.Clamp01(taxRate);
            float taxable = Mathf.Max(0f, estate - exempt); // 控除超過分のみ課税
            return taxable * rate;
        }

        /// <summary>
        /// 相続争いのリスク（0..1）を返す。相続人が1人以下なら争う相手がいないため0。複数なら基準＋人数＋分割相続の加算に、
        /// 取り分の曖昧さ（<paramref name="assetClarity"/>＝1で明白・0で曖昧）ぶんを上乗せ。決定論＝発火は呼び出し側 roll。
        /// </summary>
        public static float DisputeRisk(int heirCount, InheritancePattern pattern, float assetClarity, InheritanceParams p)
        {
            int n = Mathf.Max(0, heirCount);
            if (n <= 1) return 0f; // 相続人が1人以下＝争いの相手がいない
            int extra = n - 1;
            float risk = p.baseDisputeRisk + extra * p.perHeirDisputeRisk;
            if (pattern == InheritancePattern.分割相続)
                risk += p.partitionDisputeRisk; // 取り分が割れて揉めやすい
            float ambiguity = 1f - Mathf.Clamp01(assetClarity); // 曖昧なほど揉める
            risk += ambiguity * p.ambiguityDisputeWeight;
            return Mathf.Clamp01(risk);
        }

        public static float DisputeRisk(int heirCount, InheritancePattern pattern, float assetClarity)
            => DisputeRisk(heirCount, pattern, assetClarity, InheritanceParams.Default);

        /// <summary>
        /// 世代を経た資産の細分化（1区画あたりの残存資産）を返す。分割相続は毎世代 1/heirsPerGen に割れ指数的に細る一方、
        /// 長子相続は本流が大半を保ち崩れにくい＝<see cref="DynastyConcentration"/> を初期資産へ掛けたもの。
        /// </summary>
        public static float FragmentationOverGenerations(float initialAssets, InheritancePattern pattern,
            int heirsPerGen, int generations, InheritanceParams p)
        {
            float init = Mathf.Max(0f, initialAssets);
            float concentration = DynastyConcentration(pattern, generations, heirsPerGen, p);
            return init * concentration; // 本流に残る資産
        }

        public static float FragmentationOverGenerations(float initialAssets, InheritancePattern pattern,
            int heirsPerGen, int generations)
            => FragmentationOverGenerations(initialAssets, pattern, heirsPerGen, generations, InheritanceParams.Default);

        /// <summary>
        /// 無相続人時の国庫帰属（エシート）額を返す。継ぐ者がいない（<paramref name="heirCount"/>≤0）なら全資産が国家へ。
        /// 相続人がいれば0（資産は相続人へ渡り国庫帰属しない）。
        /// </summary>
        public static float EscheatToState(int heirCount, float assets)
        {
            float total = Mathf.Max(0f, assets);
            return heirCount <= 0 ? total : 0f; // 継ぐ者なき資産は国家へ
        }
    }
}
