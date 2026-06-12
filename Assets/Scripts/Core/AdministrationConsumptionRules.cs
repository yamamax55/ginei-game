using UnityEngine;

namespace Ginei
{
    /// <summary>統治機構の機能区分（STATEDEM-1・#2077）。行政（官僚機構）／インフラ（維持）／公共サービス。</summary>
    public enum AdminFunction { 行政, インフラ, 公共サービス }

    /// <summary>
    /// 行政が消費する物資の原単位（STATEDEM-1・#2077・lookup・唯一の窓口）。
    /// 既存 <see cref="ResourceType"/>{物資/弾薬/燃料} を流用＝統治機構が住民1人あたりに必要とする物資。
    /// 行政・公共は物資中心、インフラは物資＋燃料。弾薬は行政が消費しない（軍#2049 と差別化）。集約・lookup。test-first。
    /// </summary>
    public static class AdministrationConsumptionRules
    {
        // 1人あたりの消費原単位 [ResourceType 物資/弾薬/燃料][AdminFunction 行政/インフラ/公共サービス]。唯一の出所。
        private static readonly float[][] rates =
        {
            //          行政    インフラ 公共サービス
            new[] { 0.02f, 0.03f, 0.02f }, // 物資（行政事務・施設維持・サービス）
            new[] { 0.00f, 0.00f, 0.00f }, // 弾薬＝行政は消費しない（軍専用）
            new[] { 0.01f, 0.02f, 0.01f }, // 燃料（移動・インフラ稼働）
        };

        /// <summary>1人あたりの消費原単位（資源×機能）。</summary>
        public static float UpkeepRate(ResourceType type, AdminFunction function)
            => rates[(int)type][(int)function];

        /// <summary>1人あたりの総原単位（全機能の合計）。</summary>
        public static float PerCapitaRate(ResourceType type)
        {
            float sum = 0f;
            var row = rates[(int)type];
            for (int i = 0; i < row.Length; i++) sum += row[i];
            return sum;
        }
    }
}
