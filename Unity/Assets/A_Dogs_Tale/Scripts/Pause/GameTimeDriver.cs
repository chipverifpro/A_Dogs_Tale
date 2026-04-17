#nullable enable
using UnityEngine;

namespace DogGame
{
    /// <summary>
    /// Single Update() driver for GameTime.
    /// Attach to a central manager object.
    /// </summary>
    public sealed class GameTimeDriver : MonoBehaviour
    {
        public bool pause = false;      // Toggle this to trigger a pause/resume
        private bool lastPauseRequest;

        public void OnEnable()
        {
            GameTime.Reset();
            pause = GamePause.IsPaused;
            lastPauseRequest = pause;
            GamePause.OnPauseChanged += HandlePauseChanged;
        }

        public void OnDisable()
        {
            GamePause.OnPauseChanged -= HandlePauseChanged;
        }

        public void Update()
        {
            GameTime.Update();

            if (pause == lastPauseRequest)
                return;

            lastPauseRequest = pause;

            if (pause)
                GamePause.Pause();
            else
                GamePause.Resume();
        }

        private void HandlePauseChanged(bool isPaused)
        {
            pause = isPaused;
            lastPauseRequest = isPaused;
        }
    }
}
