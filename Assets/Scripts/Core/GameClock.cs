using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ゲーム時間の唯一の権威（TIME-1 #947）。累積 game-seconds を保持し、速度倍率・ポーズを反映して進める。
    /// 長期戦役の精度のため累積は <see cref="double"/>。負の経過時間/速度は0へクランプ（巻き戻り禁止）。
    /// 純データ（非 MonoBehaviour・非 static）＝Game層が1インスタンス保持し、毎フレーム <see cref="Advance"/> で回す想定。
    /// </summary>
    [Serializable]
    public class GameClock
    {
        /// <summary>累積したゲーム内秒数（game-seconds）。精度のため double。</summary>
        public double elapsedSeconds;

        /// <summary>速度倍率（0.5/1/2/3…）。負は <see cref="SetSpeed"/> で0へクランプ。</summary>
        public float speed = 1f;

        /// <summary>ポーズ中フラグ（true の間は <see cref="Advance"/> で進まない）。</summary>
        public bool paused;

        /// <summary>累積ゲーム秒数（読み取り）。</summary>
        public double ElapsedSeconds => elapsedSeconds;

        /// <summary>
        /// 実時間 <paramref name="realDt"/> 秒に対応する実効ゲーム秒数。ポーズ中は0、
        /// それ以外は <c>max(0,realDt) × max(0,speed)</c>（負入力は0クランプ）。
        /// </summary>
        public double EffectiveDt(float realDt)
        {
            return paused ? 0.0 : (double)Mathf.Max(0f, realDt) * Mathf.Max(0f, speed);
        }

        /// <summary>実時間 <paramref name="realDt"/> 秒ぶん時間を進める（実効秒数を累積へ加算）。</summary>
        public void Advance(float realDt)
        {
            elapsedSeconds += EffectiveDt(realDt);
        }

        /// <summary>速度倍率を設定（負は0へクランプ）。</summary>
        public void SetSpeed(float value)
        {
            speed = Mathf.Max(0f, value);
        }

        /// <summary>ポーズする。</summary>
        public void Pause() => paused = true;

        /// <summary>ポーズ解除する。</summary>
        public void Resume() => paused = false;

        /// <summary>ポーズ状態を反転する。</summary>
        public void TogglePause() => paused = !paused;
    }
}
