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

        public void Update()
        {
            GameTime.Update();

            if (GamePause.IsPaused == pause) return;

            if (pause) GamePause.Pause();
            else GamePause.Resume();

        }
        private void OnEnable()
        {
            GameTime.Reset();
        }
    }
}