using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 陣営に応じて艦隊の各パーツ（艦体スプライト、選択リング、ラベル等）の色を一括設定するクラス。
    /// </summary>
    [RequireComponent(typeof(FleetStrength))]
    public class FactionColor : MonoBehaviour
    {
        [Header("配色設定")]
        public Color imperialColor = new Color(0.9f, 0.2f, 0.2f); // 帝国: 赤系
        public Color allianceColor = new Color(0.2f, 0.5f, 0.9f); // 同盟: 青系

        [Tooltip("旗艦の発光ハロー(FlagshipMarkerGlow)に乗せる陣営色の濃さ(アルファ)")]
        public float flagshipGlowAlpha = 0.55f;

        private FleetStrength fleetStrength;

        private void Awake()
        {
            fleetStrength = GetComponent<FleetStrength>();
        }

        private void Start()
        {
            ApplyColors();
        }

        /// <summary>
        /// 現在の陣営に基づいて全ての子要素の色を更新します。
        /// </summary>
        [ContextMenu("Apply Colors Now")]
        public void ApplyColors()
        {
            if (fleetStrength == null) fleetStrength = GetComponent<FleetStrength>();
            
            Color targetColor = (fleetStrength.faction == Faction.帝国) ? imperialColor : allianceColor;

            // 1. スプライトの色分け (旗艦およびSquadron配下の全艦)
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers)
            {
                // 選択リング(黄固定)と旗艦マーカー本体(金固定)は陣営色で塗らないので除外する
                if (sr.gameObject.name == "SelectionRing") continue;
                if (sr.gameObject.name == "FlagshipMarker") continue;

                // 旗艦の発光ハローだけは陣営色を薄く乗せる（帝国/同盟の識別＋マーカー強調）。
                // 金ダイヤ＝旗艦の目印、ハロー色＝陣営、と役割を分ける。
                if (sr.gameObject.name == "FlagshipMarkerGlow")
                {
                    sr.color = new Color(targetColor.r, targetColor.g, targetColor.b, flagshipGlowAlpha);
                    continue;
                }

                sr.color = targetColor;
            }

            // 2. テキストラベルの色分け (頭上の提督名・兵力)
            TextMesh[] worldTexts = GetComponentsInChildren<TextMesh>(true);
            foreach (var txt in worldTexts)
            {
                txt.color = targetColor;
            }
            
            // 3. 武器（ビーム）の色も陣営に合わせる (オプション的な配慮)
            FleetWeapon weapon = GetComponent<FleetWeapon>();
            if (weapon != null)
            {
                weapon.beamColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0.8f);
            }

            // 4. 射界線の色も陣営に合わせる
            WeaponArc arc = GetComponent<WeaponArc>();
            if (arc != null)
            {
                arc.gizmoColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0.5f);
            }
        }

        // インスペクターで値をいじった時に反映されるようにする
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplyColors();
            }
        }
    }
}
