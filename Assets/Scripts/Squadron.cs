using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 陣形の種類定義
    /// </summary>
    public enum Formation
    {
        横陣, // Line Abreast
        縦陣, // Line Ahead
        方陣, // Square
        楔形  // Wedge
    }

    /// <summary>
    /// 旗艦（自身）の周囲に配下艦を陣形通りに配置・追従させるクラス。
    /// </summary>
    public class Squadron : MonoBehaviour
    {
        [Header("陣形設定")]
        public Formation currentFormation = Formation.横陣;
        
        [Tooltip("艦同士の間隔")]
        public float spacing = 2.0f;
        
        [Tooltip("方陣などの列数")]
        public int columns = 3;

        [Tooltip("追従の滑らかさ（秒）")]
        public float smoothTime = 0.3f;

        [Header("配下艦")]
        [Tooltip("配下艦のリスト（Inspectorでセットするか、子のSpriteRendererを自動取得）")]
        public List<Transform> memberShips = new List<Transform>();

        // SmoothDamp用の速度バッファ
        private List<Vector2> velocities = new List<Vector2>();

        private void Start()
        {
            // memberShipsが空の場合、子のTransformを取得（自分自身は除く）
            if (memberShips.Count == 0)
            {
                foreach (Transform child in transform)
                {
                    memberShips.Add(child);
                }
            }

            // 速度バッファを初期化
            for (int i = 0; i < memberShips.Count; i++)
            {
                velocities.Add(Vector2.zero);
            }
        }

        private void Update()
        {
            // 数字キーでの陣形切り替えテスト
            HandleFormationInput();

            // 各艦を目標スロットへ移動
            UpdateShipPositions();
        }

        /// <summary>
        /// 数字キー1〜4で陣形を切り替えます。
        /// </summary>
        private void HandleFormationInput()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame) currentFormation = Formation.横陣;
            if (Keyboard.current.digit2Key.wasPressedThisFrame) currentFormation = Formation.縦陣;
            if (Keyboard.current.digit3Key.wasPressedThisFrame) currentFormation = Formation.方陣;
            if (Keyboard.current.digit4Key.wasPressedThisFrame) currentFormation = Formation.楔形;
        }

        /// <summary>
        /// 陣形に基づいた各艦の目標座標を計算し、SmoothDampで追従させます。
        /// </summary>
        private void UpdateShipPositions()
        {
            for (int i = 0; i < memberShips.Count; i++)
            {
                if (memberShips[i] == null) continue;

                // 旗艦のローカル座標系での目標位置を計算
                Vector2 localSlotPos = GetSlotPosition(i);
                
                // ローカル座標をワールド座標に変換
                // transform.TransformPoint を使うことで、旗艦の回転・スケールに対応
                Vector3 targetWorldPos = transform.TransformPoint(localSlotPos);

                // Z軸は旗艦と同じにする（2D）
                targetWorldPos.z = transform.position.z;

                // SmoothDampで滑らかに移動
                Vector2 currentPos = memberShips[i].position;
                Vector2 velocity = velocities[i];
                
                Vector2 nextPos = Vector2.SmoothDamp(
                    currentPos, 
                    targetWorldPos, 
                    ref velocity, 
                    smoothTime
                );

                velocities[i] = velocity;
                memberShips[i].position = new Vector3(nextPos.x, nextPos.y, targetWorldPos.z);

                // 向きを旗艦に合わせる
                memberShips[i].rotation = transform.rotation;
            }
        }

        /// <summary>
        /// 陣形とインデックスに応じたローカル座標スロットを計算します。
        /// 旗艦（自分）は (0,0) とし、配下艦はその周囲に配置されます。
        /// </summary>
        private Vector2 GetSlotPosition(int index)
        {
            // インデックスを1ずらす（0番目は旗艦のすぐ隣などにするため）
            // 旗艦自身はこのスクリプトがついた親オブジェクトなので、memberShipsには含まれない想定
            int shipIdx = index + 1; 

            switch (currentFormation)
            {
                case Formation.横陣:
                    // 左右に広がる。奇数番は右、偶数番は左
                    float side = (shipIdx % 2 == 0) ? -1f : 1f;
                    int step = (shipIdx + 1) / 2;
                    return new Vector2(side * step * spacing, 0);

                case Formation.縦陣:
                    // 後ろに並ぶ
                    return new Vector2(0, -shipIdx * spacing);

                case Formation.方陣:
                    // columns列で格子状に配置。旗艦を最前列中央付近に置くイメージ
                    int row = shipIdx / columns;
                    int col = shipIdx % columns;
                    float xOffset = (col - (columns - 1) / 2f) * spacing;
                    float yOffset = -row * spacing;
                    return new Vector2(xOffset, yOffset);

                case Formation.楔形:
                    // V字。1:右後、2:左後、3:さらに右後...
                    int vRow = (shipIdx + 1) / 2;
                    float vSide = (shipIdx % 2 == 0) ? -1f : 1f;
                    return new Vector2(vSide * vRow * spacing, -vRow * spacing);

                default:
                    return Vector2.zero;
            }
        }
    }
}
