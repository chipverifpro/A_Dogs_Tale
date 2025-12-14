using System.Collections.Generic;
using DogGame.Language;
using UnityEngine;

public class Demo_Speech : MonoBehaviour
{
    public List<string> demoMessages;
    [SerializeField] private BottomBanner bottomBanner;

    public DogSpeechDictionary dogSpeechDictionary;
    //private DogGame.Comms.SpeechService speechService;

    private void Awake()
    {
        //speechService = new DogGame.Comms.SpeechService(bottomBanner);
        demoMessages = new();
        // example of speech
        demoMessages.Add ("Come Fido, <+>you are a good dog!</+");
        demoMessages.Add ("Stay Fido!");
        demoMessages.Add ("Don't get into the trash <untranslated>(points to trash can)</untranslated>. <->Bad Dog!</-> <learn>trash</learn>");
        demoMessages.Add ("<+>Dinner Time!</+>");
        demoMessages.Add ("<->Don't touch my slippers.</->");
        demoMessages.Add ("Please fetch my slippers <untranslated>(points to feet)</untranslated> <learn>slippers</learn>");
        demoMessages.Add ("Find my shoe <untranslated>(points to foot)</untranslated> <learn>shoe</learn>");
        demoMessages.Add ("You are Fido <learn>Fido</learn>");
        demoMessages.Add ("Hi");
        demoMessages.Add ("<i>italics</i>");
        demoMessages.Add ("bland<+>positive</+>bland again <learn>positive</learn> <learn>bland</learn>");
        demoMessages.Add ("bland<+>positive</+>bland again <learn>positive</learn> <learn>bland</learn>");
        demoMessages.Add ("<->negative</->");
        demoMessages.Add ("<untranslated>Eyes open wide</untranslated>");
        demoMessages.Add ("I am your King <learn>king</learn>, your King am I");
        demoMessages.Add ("The End");
    }

    // call this and one of the above speech lines will be displayed.
    // currently triggers when clicking on a worldObject.
    public void TestSpeak(WorldObject npcHuman, WorldObject intendedDog)
    {
        int message_number = UnityEngine.Random.Range(0,demoMessages.Count);
        dogSpeechDictionary.Speak(npcHuman, intendedDog, demoMessages[message_number]);
    }
}