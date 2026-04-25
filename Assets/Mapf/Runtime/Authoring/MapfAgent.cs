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

        private MapfNode _lastRuntimeGoal;
        private MapfCoordinator _coordinator;

        public int AgentId => agentId;
        public MapfNode StartNode => startNode;
        public MapfNode GoalNode => goalNode;

        public void SetGoal(MapfNode goal)
        {
            goalNode = goal;
            _lastRuntimeGoal = goal;
        }

        public void Configure(int id, MapfNode start, MapfNode goal)
        {
            agentId = id;
            startNode = start;
            goalNode = goal;
            _lastRuntimeGoal = goal;
        }

        private void Start()
        {
            _lastRuntimeGoal = goalNode;
            _coordinator = FindAnyObjectByType<MapfCoordinator>();
        }

        private void Update()
        {
            if (!Application.isPlaying || goalNode == _lastRuntimeGoal)
                return;

            _lastRuntimeGoal = goalNode;
            _coordinator ??= FindAnyObjectByType<MapfCoordinator>();
            if (_coordinator != null && goalNode != null)
                _coordinator.RequestAgentGoal(this, goalNode);
        }
    }
}
