using System.Collections.Generic;
using UnityEngine;

public class Demo_Speech : MonoBehaviour
{
    public List<string> demoMessages;
    [SerializeField] private BottomBanner bottomBanner;

    private DogGame.Comms.SpeechService speechService;

    private void Awake()
    {
        speechService = new DogGame.Comms.SpeechService(bottomBanner);
        demoMessages = new();
        // example of speech
        demoMessages.Add ("Come Fido, you are a good dog!");
        demoMessages.Add ("Stay Fido!");
        demoMessages.Add ("Don't get into the trash <untranslated>(points to trash can)</untranslated>. Bad Dog! <learnword>trash</learnword>");
        demoMessages.Add ("Dinner Time!");
        demoMessages.Add ("Don't touch my slippers.");
        demoMessages.Add ("Please fetch my slippers <untranslated>(points to feet)</untranslated> <learnword>slippers</learnword>");
        demoMessages.Add ("Find my shoe <untranslated>(points to foot)</untranslated> <learnword>shoe</learnword>");
        demoMessages.Add ("You are Fido <learnword>Fido</learnword>");
    }

    // call this and one of the above speech lines will be displayed.
    public void TestSpeak(WorldObject npcHuman, WorldObject intendedDog)
    {
        int message_number = UnityEngine.Random.Range(0,demoMessages.Count);
        speechService.Speak(npcHuman, intendedDog, demoMessages[message_number]);
    }
}