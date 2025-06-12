using BeatSaberMarkupLanguage.Animations;
using UnityEngine;

namespace EnhancedStreamChat.Graphics
{
    /// <summary>
    /// Simple updater that cycles through sprites from an <see cref="AnimationControllerData"/>
    /// and updates the associated <see cref="EnhancedImage"/>.
    /// This is a lightweight stand-in for the original implementation found in the
    /// archived version of the mod.
    /// </summary>
    public class AnimationStateUpdater : MonoBehaviour
    {
        public EnhancedImage Image { get; set; }
        public AnimationControllerData ControllerData { get; set; }

        private int _index;
        private float _timer;

        private void LateUpdate()
        {
            if (this.Image == null || this.ControllerData?.Sprites == null || this.ControllerData.Sprites.Length == 0)
            {
                return;
            }

            var delay = 0.1f;
            if (this.ControllerData.Delays != null && this.ControllerData.Delays.Length > this._index)
            {
                delay = this.ControllerData.Delays[this._index];
            }

            this._timer += Time.unscaledDeltaTime;
            if (this._timer >= delay)
            {
                this._timer = 0f;
                this._index = (this._index + 1) % this.ControllerData.Sprites.Length;
                this.Image.sprite = this.ControllerData.Sprites[this._index];
            }
        }
    }
}