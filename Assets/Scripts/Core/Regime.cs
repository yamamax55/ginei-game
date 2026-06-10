using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 王朝/秩序＝天命と易姓革命（孔子 #867・転換エンジン #801/#823・カリスマの日常化 #814）。
    /// 統治の<see cref="legitimacy"/>（天命/正統性）は<see cref="corruption"/>（腐敗/制度疲労）で衰え、
    /// <see cref="virtue"/>（徳）が高いほど腐敗が遅い。正統性を失えば天命を失い（易姓革命の機）、腐敗が高じれば
    /// 改革者/異端者が立つ（ルター #824）。秩序転換エンジンの心臓。解決は <see cref="DynastyRules"/>（static）。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Regime
    {
        public int id;
        public Faction faction;
        public float legitimacy = 1f; // 天命/正統性 0..1
        public float corruption = 0f; // 腐敗/制度疲労 0..1
        public float virtue = 0.5f;   // 統治の徳 0..1（高いほど腐敗が遅い）

        public Regime() { }

        public Regime(int id, Faction faction, float legitimacy = 1f, float corruption = 0f, float virtue = 0.5f)
        {
            this.id = id;
            this.faction = faction;
            this.legitimacy = Mathf.Clamp01(legitimacy);
            this.corruption = Mathf.Clamp01(corruption);
            this.virtue = Mathf.Clamp01(virtue);
        }
    }
}
