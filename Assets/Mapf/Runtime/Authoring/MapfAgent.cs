using Mapf.UnityAdapter;
using UnityEngine;

namespace Mapf.Authoring
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MapfAgentController))]
    public sealed class MapfAgent : MonoBehaviour
    {
        [SerializeField] private int agentId;
        [SerializeField] private MapfNode startNode;
        [SerializeField] private MapfNode goalNode;

        public int AgentId => agentId;
        public MapfNode StartNode => startNode;
        public MapfNode GoalNode => goalNode;

        public void SetGoal(MapfNode goal)
        {
            goalNode = goal;
        }

        public void Configure(int id, MapfNode start, MapfNode goal)
        {
            agentId = id;
            startNode = start;
            goalNode = goal;
        }
    }
}
