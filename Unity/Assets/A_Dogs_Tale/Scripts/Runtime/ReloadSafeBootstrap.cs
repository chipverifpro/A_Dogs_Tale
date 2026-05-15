using DogGame;
using DogGame.Noise;
using UnityEngine;

public static class ReloadSafeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticRuntimeState()
    {
        Dir.ResetStaticStateForReload();
        GameInputRouter.ResetStaticStateForReload();
        WorldObjectRegistry.ResetStaticStateForReload();
        DogGame.Modules.QuestManager.ResetStaticStateForReload();
        NoiseManager.ResetStaticStateForReload();
        BottomBanner.ResetStaticStateForReload();
        GamePause.ResetStaticStateForReload();
        GameTime.Reset();
    }
}
