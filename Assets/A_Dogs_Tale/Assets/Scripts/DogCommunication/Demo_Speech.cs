using UnityEngine;

public class Demo_Speech : MonoBehaviour
{
    [SerializeField] private BottomBanner bottomBanner;

    private DogGame.Comms.SpeechService speechService;

    private void Awake()
    {
        speechService = new DogGame.Comms.SpeechService(bottomBanner);
    }

    public void TestSpeak(WorldObject npcHuman, WorldObject intendedDog)
    {
        speechService.Speak(npcHuman, intendedDog, "Come Fido, you are a good dog!");
    }
}