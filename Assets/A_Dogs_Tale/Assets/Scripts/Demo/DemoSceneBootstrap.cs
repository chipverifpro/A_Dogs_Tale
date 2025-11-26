using UnityEngine;

namespace DogGame.AI
{
    /// <summary>
    /// Simple demo bootstrap:
    /// - Finds the Player agent and creates a Pack for them.
    /// - Assigns all "PackFollower" tagged agents into that pack as followers.
    /// - Leaves wanderers alone (they just wander).
    /// </summary>
    public class DemoSceneBootstrap : MonoBehaviour
    {
        [Header("Player Pack Setup")]
        public PackTacticsProfile playerPackTactics;

        private Pack playerPack;

        private void Awake()
        {
            // 1. Find the player agent.
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO == null)
            {
                Debug.LogError("DemoSceneBootstrap: No GameObject with tag 'Player' found in scene.");
                return;
            }

            AgentController playerAgent = playerGO.GetComponent<AgentController>();
            if (playerAgent == null)
            {
                Debug.LogError("DemoSceneBootstrap: Player GameObject has no AgentController.");
                return;
            }

            // 2. Create a new pack for the player.
            playerPack = new Pack(playerAgent, playerPackTactics)
            {
                packName = "Player Pack"
            };

            // Ensure the player knows they are in a pack.
            AgentPackMember playerPackMember = playerGO.GetComponent<AgentPackMember>();
            if (playerPackMember != null)
            {
                playerPackMember.JoinPack(playerPack, setAsLeader: true);
            }
            else
            {
                Debug.LogWarning("DemoSceneBootstrap: Player has no AgentPackMember component.");
            }

            // 3. Find all follower agents by tag and add them to the pack.
            GameObject[] followerObjects = GameObject.FindGameObjectsWithTag("PackFollower");
            foreach (GameObject followerGO in followerObjects)
            {
                AgentController followerAgent = followerGO.GetComponent<AgentController>();
                AgentPackMember followerPackMember = followerGO.GetComponent<AgentPackMember>();

                if (followerAgent == null || followerPackMember == null)
                {
                    Debug.LogWarning($"DemoSceneBootstrap: Follower object '{followerGO.name}' is missing AgentController or AgentPackMember.");
                    continue;
                }

                followerPackMember.JoinPack(playerPack, setAsLeader: false);

                // Make sure their decision mode is Follower.
                followerAgent.BecomeFollower();
            }

            Debug.Log($"DemoSceneBootstrap: Created pack '{playerPack.packName}' with {playerPack.members.Count} members.");
        }
    }
}