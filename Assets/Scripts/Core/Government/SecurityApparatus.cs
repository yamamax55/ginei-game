using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 秘密警察（シュタージ型・#166）の純データ。国家の治安装置を3つの軸で表す：
    /// <see cref="surveillance"/>（監視網の広さ＝盗聴/密告の網羅性）、<see cref="informantNetwork"/>
    /// （非公式協力者IM の密度＝市民同士の監視）、<see cref="repression"/>（弾圧の苛烈さ＝逮捕/処断の強度）。
    /// 反対派の抑圧・クーデター摘発に効く一方、弾圧は支持を蝕む（恐怖統治のトレードオフ）。
    /// 解決は <see cref="SecurityRules"/>（static）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class SecurityApparatus
    {
        public int id;
        public float surveillance = 0f;      // 監視網の広さ 0..1
        public float informantNetwork = 0f;  // 密告者（IM）網の密度 0..1
        public float repression = 0f;        // 弾圧の苛烈さ 0..1

        public SecurityApparatus() { }

        public SecurityApparatus(int id, float surveillance = 0f, float informantNetwork = 0f, float repression = 0f)
        {
            this.id = id;
            this.surveillance = Mathf.Clamp01(surveillance);
            this.informantNetwork = Mathf.Clamp01(informantNetwork);
            this.repression = Mathf.Clamp01(repression);
        }
    }
}
