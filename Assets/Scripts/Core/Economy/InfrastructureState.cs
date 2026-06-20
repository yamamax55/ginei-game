namespace Ginei
{
    /// <summary>
    /// 社会基盤（インフラ）の状態（内政 #2126 系・経済の素地）。道路・港湾・通信などの整備水準を
    /// 0..1 の集約スカラで持つ（個体粒度へ降りない＝終盤ラグ回避）。整備が高いほど経済産出・補給効率が上がる。
    /// 進行は <see cref="InfrastructureTickRules"/>（投資で上昇・無投資で経年劣化）。直列化可能な平データ。
    /// </summary>
    [System.Serializable]
    public class InfrastructureState
    {
        /// <summary>整備水準 0..1（0=未整備／1=完備）。</summary>
        public float level = 0.5f;

        public InfrastructureState() { }
        public InfrastructureState(float level) { this.level = UnityEngine.Mathf.Clamp01(level); }
    }
}
