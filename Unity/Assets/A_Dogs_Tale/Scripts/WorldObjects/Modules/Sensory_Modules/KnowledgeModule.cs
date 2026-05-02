using System.Collections.Generic;
using UnityEngine;
using DogGame.Noise;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using InspectorTools;

//
//   friends list
//   doggy dictionary
//   commands and tricks known [FUTURE]
//
namespace DogGame.Modules
{
    /// <summary>
    /// Maintains and looks up friends and doggy dictionary words
    /// </summary>
    [InspectorNote("Sensory_Modules/Knowledge Module", "Keep track of friends list, doggy dictionary, and commands and tricks known (future).")]
    [DisallowMultipleComponent]
    public class KnowledgeModule : WorldModule
    {
        [Header("Information Known by this Agent")]
        public List<WorldObject> knownAgents = new();       // includes any WorldObject
        public List<String> knownWords = new();

        public string substituteUnknownWord = "--blah--";
    
        //========================
        // ---- Friends List ----
        //========================
        #region Friends List
        public bool LearnKnownAgent(WorldObject newAgent)
        {
            if (!knownAgents.Contains(newAgent))
            {
                knownAgents.Add(newAgent);
                return true;
            }
            return false;
        }

        int LearnNamesOfPackMembers(List<WorldObject>packMemberList)
        {
            int countAdded = 0;
            foreach (WorldObject agent in packMemberList)
            {
                if (LearnKnownAgent(agent))
                    countAdded++;
            }
            return countAdded;
        }

        // given an Agent's worldObject, returns their Display name if known by THIS agent, otherwise their type
        public string KnowsAgentAs(WorldObject agentX, out bool knowsAgentX)
        {
            if (knownAgents.Contains(agentX))
            {
                knowsAgentX = true;
                return ToTitleCase(agentX.DisplayName);
            }

            knowsAgentX = false;
            return ToTitleCase(agentX.llmConfigModule.identity.species.ToString());
        }      

        // given a name, returns true if it's name is known by THIS agent.
        public bool KnowsAgentName(string agentXName)
        {
            bool known;
            agentXName = ToTitleCase(agentXName);
            foreach (WorldObject agentX in knownAgents)
            {
                if (agentXName == ToTitleCase(agentX.DisplayName))
                {
                    KnowsAgentAs(agentX, out known);
                    if (known) return true;
                }
            }
            return false;
        }

        // converts gerMan ShepHerd to German Shepherd
        public string ToTitleCase(string input)
        {
            string result = CultureInfo.CurrentCulture.TextInfo
                            .ToTitleCase(input.ToLower());

            //string result = CultureInfo.InvariantCulture.TextInfo
            //                .ToTitleCase(input.ToLowerInvariant());
            return result;
        }

        #endregion
        //============================
        // ---- Doggy Dictionary ----
        //============================
        #region Doggy Dictionary
        public bool LearnKnownWord(string newWord)
        {
            if (!knownWords.Contains(newWord))
            {
                knownWords.Add(newWord);
                return true;
            }
            return false;
        }

        public int LearnKnownWords(List<string> newlist)
        {
            int countnew = 0;
            foreach (string word in newlist)
            {
                if (LearnKnownWord(word)) countnew++;
            }
            return countnew;
        }

        // returns known words, or --blah-- for unknown ones.
        public string KnowsWordAs(string wordIn, out bool knowsWord)
        {
            if (knownWords.Contains(wordIn))
            {
                knowsWord = true;
                return wordIn;
            }

            if (KnowsAgentName(wordIn))
            {
                knowsWord = true;
                return wordIn;
            }

            knowsWord = false;
            return substituteUnknownWord;
        }

        // same as above but for a whole string.
        // preserves spacing and punctuation.
        public string TranslateFromHuman(string humanString, out string tone)
        {
            tone = "neutral";
            List<string> dogWords = new();
            bool isKnown;
            List<string> humanWords = WordSplitter(humanString);
            foreach (string humanWord in humanWords)
            {
                char firstchar = humanWord[0];
                if (char.IsLetterOrDigit(firstchar) || firstchar=='\'')
                {
                    dogWords.Add(KnowsWordAs(humanWord, out isKnown)); // translate word
                } else
                {
                    dogWords.Add(humanWord);    // keep spacing, punctuation, etc.
                }
            }
            return(string.Join("",dogWords));
        }

        // "I came. I saw.\n";
        //   becomes
        // ["I", " ", "came", ".", " ", "I", " ", "saw", ".", "\n"]
        public List<string> WordSplitter(string input)
        {
            List<string> parts = Regex.Matches(input, @"[A-Za-z0-9']+|\s+|[^A-Za-z0-9'\s]")
                .Select(m => m.Value)
                .ToList();

            return parts;
        }
        #endregion

        #region debug
        // DEBUG test sequence
        public void Start()
        {
            List<string> learnWords = new List<string>
            {
                "Chihuahua",
                "good",
                "dog",
                "bad",
                "cat",
                "came",
                "come"
            };
            LearnNamesOfPackMembers(worldObject.packMemberModule.currentPack.packAgentList);
            string tone;
            string humanString = "I came, I saw.  GermanShepherd didn't see.";
            string dogString = TranslateFromHuman(humanString, out tone);
            Debug.Log(humanString);
            Debug.Log(dogString);
            LearnKnownAgent(worldObject);
            LearnKnownWords(learnWords);
            dogString = TranslateFromHuman(humanString, out tone);
            Debug.Log(dogString);
        }
        #endregion
    }
}
