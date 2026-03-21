using System;
using System.Collections.Generic;
using DogGame.Modules;
using Unity.AppUI.UI;
using UnityEngine;

namespace DogGame.Lua
{
    public class HearingSoundState
    {
        public WorldObject listener;        // provided by creator
        public WorldObject noiseMaker;      // provided by creator
        public string soundType = "";       // provided by creator
        public string humanWords;           // provided by creator
        public float loudnessDb;            // provided by creator, defined as volume at 1m in decibels

        public Vector3 noiseMakerLocation;  // collected by constructor
        public Vector3 listenerLocation;    // collected by constructor
        public float distance;              // calc by constructor
        public string direction = "";       // calc by constructor
        public string humanToDogWords = ""; // converted by constructor
        public string humanToDogTone = "";  // extracted by constructor, negative, neutral, positive
        public float perceivedVolumeDb;     // calc by constructor (in decibels)
        public float time;                  // collected by constructor
        public bool reported = false;       // updated by SendToLLM to identify as old info, don't resend as new event. (or just delete it?)
        
        // derived-on-the-fly parameters
        public float age => Time.time - time;

        public string TryHearingSoundToText (Detail detail,
                                             float threshold,
                                             out bool heard)
        {
            // outputs:
            string description = ""; 
            heard = false;

            if (detail == Detail.None)      // early exit optimization
            {
                heard = false;
                return "";
            }

            // ---- begin variable list----
            // Listener
            String softListener;            // Fido
            string speciesListener;         // dog

            // NoiseMaker
            bool knowsNoiseMaker;           // false or true
            String softNoiseMaker;          // cat   or Fifi
            string speciesNoiseMaker;       // cat
            
            // Sound
            String softSoundType;           // bark
            String softDirection;           // northeast
            String softDistance;            // 5m
            String softVolume;              // faintly, clearly, loudly, etc
            // bool heard is a parameter    // false iff inaudible
            string softHowLongAgo = "";     // about 3 seconds ago
            
            // human words
            String humanToDogWords = "";    // Bad dog! blah blah blah shoe
            String humanToDogTone = "";     // negative, neutral, positive
            
            // ---- begin converting ----
            softListener = listener.DisplayName;
            speciesListener = listener.llmConfigModule.identity.species.ToString();

            softNoiseMaker = listener.knowledgeModule.KnowsAgentAs(noiseMaker, out knowsNoiseMaker);         
            speciesNoiseMaker = noiseMaker.llmConfigModule.identity.species.ToString();

            softSoundType = soundType;
            softDirection = direction;
            softDistance = distance.ToString();
            softVolume = GetSoftVolume(speciesListener, perceivedVolumeDb, out heard);

            if (age > 2f)
            {
                softHowLongAgo = $"About {age:0} seconds ago, ";
            }

            if (humanWords != "" && speciesListener == "dog")
            {
                humanToDogWords = listener.knowledgeModule.TranslateFromHuman(humanWords, out humanToDogTone);                
            }
            
            if (heard==false) return "";

            if (humanToDogWords == "")
            {    
                // not human speech
                switch (detail)
                {
                    case Detail.Low:
                        description = $"You heard a {softVolume} {softSoundType} to the {softDirection}.";
                        break;
                    case Detail.Medium:
                        description = $"You heard a {softSoundType} sound to the {softDirection} about {softDistance} away that is {softVolume}.";
                        break;
                    case Detail.High:
                        description = $"{softListener} heard a {softSoundType} sound to the {softDirection} about {softDistance} away that is {softVolume}.";
                        break;
                    default:
                        description = "";
                        break;
                }
            }
            else    // translatedWords != ""
            {
                // human speech
                switch (detail)
                {
                    case Detail.Low:
                        description = $"You heard \"{humanToDogWords}\" in a {humanToDogTone} tone from the {softDirection}.";
                        break;
                    case Detail.Medium:
                        description = $"{softHowLongAgo}, you heard {noiseMaker} say \"{humanToDogWords}\" in a {humanToDogTone} tone from the {softDirection} about {softDistance} away that is {softVolume}.";
                        break;
                    case Detail.High:
                        description = $"{softHowLongAgo}, {softListener} heard {noiseMaker} say \"{humanToDogWords}\" in a {humanToDogTone} tone from the {softDirection} about {softDistance} away that is {softVolume}.";
                        break;
                    default:
                        description = "";
                        break;
                }
            }


            return description;
        }


        string GetSoftVolume (string species, float perceivedVolumeDb, out bool heard)
        {
            float dbBoost;
            string softLoudness;

            heard = false;
            dbBoost = GetSpeciesHearingDbBoost(species);
            softLoudness = LoudnessTerm(perceivedVolumeDb + dbBoost);
            if (softLoudness != "") heard = true;
            return softLoudness;
        }

        float GetSpeciesHearingDbBoost(string species) => species.ToLowerInvariant() switch
        {
            "human" => 0f,
            "dog" => 5f,
            "cat" => 7f,
            "bird" => 8f,
            "lizard" => -2f,
            "squirrel" => 6f,
            "monkey" => 3f,
            _ => 0f
        };

        string LoudnessTerm(float db) => db switch
        {
            < 10f => "",                // inaudible
            < 20f => "barely audible",
            < 30f => "very faintly",
            < 40f => "faintly",
            < 50f => "softly",
            < 60f => "clearly",
            < 70f => "noticeably loudly",
            < 80f => "loudly",
            < 90f => "very loudly",
            < 100f => "extremely loudly",
            _ => "deafeningly"
        };

        // constructor
        public HearingSoundState (WorldObject listener, WorldObject noiseMaker, 
                                  String soundType, String humanWords, float loudnessDb)
        {
            // copy provided parameters to structure
            this.listener = listener;
            this.noiseMaker = noiseMaker;
            this.soundType = soundType;
            this.humanWords = humanWords;
            this.loudnessDb = loudnessDb;

            // get current locations
            noiseMakerLocation = noiseMaker.pos3d_map;
            listenerLocation   = listener.pos3d_map;

            // calculate distance from Agent to sound
            Vector3 deltaLocation = noiseMakerLocation - listenerLocation;
            distance=Vector3.Magnitude(deltaLocation);

            noiseMakerLocation = noiseMaker.pos3d_world;
            listenerLocation = listener.pos3d_world;
            // create a string describing direction and optionally distance
            // example: "northwest 5.1m" // replace distance with 0f to disable distance in string.
            direction = LocationToDirection (noiseMakerLocation, distance);

            // calculate sound volume based on loudness and distance and eventually obstructions.
            float dBdistance = 20f * Mathf.Log10(Mathf.Max(distance, 0.0001f));
            int wallCount = 0;  // TODO: find this from map
            int doorCount = 0;  // TODO: find this from routing
            // simplified barriers: if we aren't in same room, assume one door.
            if (noiseMaker.locationModule.cell.room_number != listener.locationModule.cell.room_number)
                doorCount = 1;
            float wallDb = 15f;             // example
            float doorDb = wallDb * 0.5f;   // example half as much
            float obstacleDb = wallCount * wallDb + doorCount * doorDb;
            perceivedVolumeDb = loudnessDb - dBdistance - obstacleDb;

            // ideally, find all the routes, and then do per-straight segment calculate dB loss, and each door.
            // if no path, do a straightline between points and count walls.

            time = Time.time;
        }

        public void UpdateHearingSoundState(Detail detail)
        {
            // placeholder for update right before usage.
            // direction description could be done here
        }


        /// <summary>
        /// LocationToDirection
        ///   Returns a string like "northwest".  Note that the secondary direction must
        ///   be at least 25% of the distance of the primary direction, or you will just
        ///   get one, like "north".
        ///   Added "above" and "below" if z magnitude > 10;
        /// </summary>
        private float Ndist, Sdist, Edist, Wdist;
        private float primaryDist;

        public String LocationToDirection(Vector3 pos, float distance = 0f)
        {
            Vector3 listenerLocation = listener.pos3d_world;
            Vector3 currentMap   = new (listenerLocation.x, listenerLocation.z, listenerLocation.y);
            Vector3 deltaLocation = pos - currentMap;
            //Debug.Log("currentMap = " + currentMap + "  delta = " + deltaLocation);
            direction = "";
            primaryDist = 1f;   // minimum distance

            // distance to from currentLocation to pos in each direction.
            Ndist = Mathf.Max(0, deltaLocation.y);
            Sdist = Mathf.Max(0, -deltaLocation.y);
            Edist = Mathf.Max(0, deltaLocation.x);
            Wdist = Mathf.Max(0, -deltaLocation.x);

            //Debug.Log($"N={Ndist} S={Sdist} E={Edist} W={Wdist}");
            
            // call this twice.  First call will zero out the primaryDirection,
            // so the second call can get the next most.
            String first_direction = primaryDirection();
            String second_direction = primaryDirection();

            if(second_direction == "")
                direction = first_direction;                    // N/S/E/W
            else if(first_direction == "east" || first_direction == "west") 
                direction = second_direction + first_direction; // swap order for EN/WN/ES/WS
            else
                direction = first_direction + second_direction; // NE/NW/SE/SW

            if (deltaLocation.z > 10f) direction += " above";
            if (deltaLocation.z < -10f) direction += " below";

            if (distance > 1f) direction += $" {distance:0.0}m";
            return direction;
        }

        public String primaryDirection()
        {
            String direction = "";

            if (isDirMax(Ndist))
            {
                primaryDist = Ndist;
                Ndist = 0f;
                direction = "north";
            }
            else if (isDirMax(Sdist))
            {
                primaryDist = Sdist;
                Sdist = 0f;
                direction = "south";
            }
            else if (isDirMax(Edist))
            {
                primaryDist = Edist;
                Edist = 0f;
                direction = "east";
            }
            else if (isDirMax(Wdist))
            {
                primaryDist = Wdist;
                Wdist = 0f;
                direction = "west";
            }
            //Debug.Log(direction);
            return direction;
        }

        public bool isDirMax(float primary)
        {
            float max = Mathf.Max(Ndist, Sdist, Edist, Wdist);
            //Debug.Log($"primary = {primary},  primaryDist = {primaryDist},  max = {max},  Ndist={Ndist}, Sdist={Sdist} Edist={Edist} Wdist={Wdist}");
            if ((primary == max) && (max != 0f) && (max > primaryDist * 0.25f))
                return true;    // this value is maximum, and at least 25% of primaryDist.
            return false;
        }

    }

// ================================================================ //
//      HearingState class
//         Deals with a list of HearingSoundState (above)
// ================================================================ //

    public class HearingState
    {
        public List<HearingSoundState> recentSounds = new();
        public bool loudNoise               = false;
        public bool barkHeard               = false;
        public bool humanVoiceHeard         = false;
        public bool distressBark            = false;
        public HearingSoundState nearestBark= null;
        public HearingSoundState lastSound  = null;

        // pointers to parent objects for ease-of-reference:
        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // placeholder for update right before usage.

            // pass the update request to each in recentSounds
            for (int i = recentSounds.Count - 1; i >= 0; i--)
            {
                recentSounds[i].UpdateHearingSoundState(detail);
            }
        }

        // must prefill heard with:
        //  type, noiseMakerLocation, loudness
        public void AddSoundHeard(WorldObject listener, WorldObject noiseMaker, string type, string humanWords, float loudness)
        {
            HearingSoundState heard = new(listener, noiseMaker, type, humanWords, loudness);
            // everything else is calculated during construction

            // Add the new sound to the list
            recentSounds.Add(heard);
        }

        public void Tick(float interval)
        {
            for (int i = recentSounds.Count - 1; i >= 0; i--)
            {
                if (recentSounds[i].age > 10f)
                    recentSounds.RemoveAt(i);
            }

            if (nearestBark?.age > 10f)
                nearestBark = null;

            if (lastSound?.age > 10f)
                lastSound = null;

            loudNoise = loudNoise && lastSound?.age < 1f;
            barkHeard = barkHeard && nearestBark?.age < 2f;
            humanVoiceHeard = humanVoiceHeard && lastSound?.age < 2f;
            distressBark = distressBark && nearestBark?.age < 2f;
        }

    }
}
