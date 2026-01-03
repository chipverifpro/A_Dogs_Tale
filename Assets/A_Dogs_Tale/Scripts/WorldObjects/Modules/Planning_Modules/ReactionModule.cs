using UnityEngine;
using DogGame.Modules;
using System.Threading.Tasks;
using UnityEditor.Tilemaps;


public class ReactionModule : WorldModule
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    protected override void Update()
    {
/*      
        // parse a list of conditions, 
        // if met, queue up the associatied response tasks.
        if (agent=dog && agent_unknown && agent_distance<dog_threshold && !agent.InPack())
        {
            Task.Add (Bark);
            Task.Add (MoveTo(agent, run));
            Task.Add Sniff (agent);
            Task.Add Conditional (agent(friendly)) Begin
                Task.Add JoinPack(agent);
                Task.Complete
            End Else
                Task.Add MoveAway(agent, run, 10seconds)
                Task.Complete
        }

        // what to do if there is an exception (task cannot complete?)
*/
    }
}
