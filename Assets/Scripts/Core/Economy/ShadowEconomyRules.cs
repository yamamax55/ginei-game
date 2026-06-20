using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 闇経済（地下経済）の純ロジック。
    /// 高税率・物資欠乏は経済活動を非課税の闇市場へ追いやり課税ベースを侵食する。
    /// 取締りはこれを抑制する。既存ゲーム型に依存しない自己完結の static ルール。
    /// （名称：本来の依頼名 BlackMarketRules は既存と衝突したため同義語に変更）
    /// </summary>
    public static class ShadowEconomyRules
    {
        /// <summary>調整パラメータ（マジックナンバーの単一の置き場）。</summary>
        public readonly struct Params
        {
            /// <summary>税率が闇市場規模へ寄与する感度。</summary>
            public readonly float taxSensitivity;
            /// <summary>欠乏が闇市場規模へ寄与する感度。</summary>
            public readonly float scarcitySensitivity;
            /// <summary>取締りが闇市場規模を抑える効果。</summary>
            public readonly float enforcementEffect;
            /// <summary>闇市場が占めうる最大比率（上限クランプ）。</summary>
            public readonly float maxShare;

            public Params(float taxSensitivity, float scarcitySensitivity, float enforcementEffect, float maxShare)
            {
                this.taxSensitivity = taxSensitivity;
                this.scarcitySensitivity = scarcitySensitivity;
                this.enforcementEffect = enforcementEffect;
                this.maxShare = maxShare;
            }

            /// <summary>既定値。</summary>
            public static Params Default => new Params(0.5f, 0.4f, 0.6f, 0.8f);
        }

        private static float Clamp01(float v) => Mathf.Clamp01(v);

        /// <summary>
        /// 闇市場の経済シェア（0..maxShare）。税率・欠乏で増え、取締りで減る。
        /// </summary>
        public static float BlackMarketShare(float taxRate01, float scarcity01, float enforcement01, Params p)
        {
            float tax = Clamp01(taxRate01);
            float scarcity = Clamp01(scarcity01);
            float enforcement = Clamp01(enforcement01);
            float raw = tax * p.taxSensitivity
                      + scarcity * p.scarcitySensitivity
                      - enforcement * p.enforcementEffect;
            // 0..maxShare へクランプ
            return Mathf.Clamp(raw, 0f, p.maxShare);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float BlackMarketShare(float taxRate01, float scarcity01, float enforcement01)
            => BlackMarketShare(taxRate01, scarcity01, enforcement01, Params.Default);

        /// <summary>
        /// 課税可能ベース。闇市場へ逃げたぶんを差し引いた総ベース。
        /// </summary>
        public static float TaxableBase(float grossBase, float blackMarketShare01)
            => grossBase * (1f - Clamp01(blackMarketShare01));

        /// <summary>取締りコスト。取締り度合いに比例（非負）。</summary>
        public static float EnforcementCost(float enforcement01, float costPerUnit)
            => Mathf.Max(0f, enforcement01) * Mathf.Max(0f, costPerUnit);
    }
}
