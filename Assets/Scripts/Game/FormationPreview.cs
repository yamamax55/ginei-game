using UnityEngine;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 移動先決定中に、選択部隊の陣形を「目標地点＋指定向き」で半透明表示するプレビュー。
    /// 旗艦中心(原点)＋各配下艦スロットに、艦スプライトを淡い陣営色で描く。
    /// ルートを目標地点に置き z 回転させることで、子（スロット）が一括で回転・配置される
    /// （Squadron が TransformPoint で配置するのと同じ「+Y=前方」基準）。
    /// FleetCommander が生成・表示制御する（シーン手配置不要）。
    /// </summary>
    public class FormationPreview : MonoBehaviour
    {
        [Tooltip("半透明の不透明度")]
        public float alpha = 0.3f;

        [Tooltip("艦体・マーカーより手前に描くための sorting order")]
        public int sortingOrder = 200;

        /// <summary>
        /// 指定部隊の陣形でゴーストを構築し、表示する。
        /// </summary>
        public void Show(Squadron squad)
        {
            Build(squad);
            gameObject.SetActive(true);
        }

        /// <summary>目標地点と向き(z角)を設定する（毎フレーム呼んでよい。安価）。</summary>
        public void SetPose(Vector2 pos, float angleZ)
        {
            transform.position = new Vector3(pos.x, pos.y, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, angleZ);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Build(Squadron squad)
        {
            // 既存ゴーストを破棄して作り直す（陣形・隻数の変化に追従）
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            if (squad == null) return;

            Sprite sprite = squad.GetShipSprite();
            if (sprite == null) return;

            Color baseColor = squad.GetShipColor();
            Color ghostColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            // 旗艦中心（原点、等倍）
            CreateGhost(Vector2.zero, sprite, ghostColor, 1f);

            // 配下艦スロット（縮小）
            List<Vector2> slots = squad.GetFormationSlots();
            float escortScale = squad.memberScale;
            for (int i = 0; i < slots.Count; i++)
            {
                CreateGhost(slots[i], sprite, ghostColor, escortScale);
            }
        }

        private void CreateGhost(Vector2 localPos, Sprite sprite, Color color, float scale)
        {
            GameObject go = new GameObject("Ghost");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            go.transform.localRotation = Quaternion.identity; // ルートの向き＝前方に追従
            go.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;            // alpha 低めで半透明（Sprites/Default はアルファ合成対応）
            sr.sortingOrder = sortingOrder;
        }
    }
}
