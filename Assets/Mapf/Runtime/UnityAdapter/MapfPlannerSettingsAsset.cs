using Mapf.Core.Planning;
using UnityEngine;

namespace Mapf.UnityAdapter
{
    /// <summary>
    /// ScriptableObject wrapper for planner settings so scenes can share and tune MAPF parameters.
    /// </summary>
    [CreateAssetMenu(menuName = "MAPF/Planner Settings")]
    public sealed class MapfPlannerSettingsAsset : ScriptableObject
    {
        [SerializeField] private double agentRadius = 0.35;
        [SerializeField] private double agentSpeed = 1.0;
        [SerializeField] private double timeLimitSeconds = 5.0;
        [SerializeField] private int maxHighLevelNodes = 20000;
        [SerializeField] private int maxLowLevelNodes = 5000;
        [SerializeField] private int maxLocalRepairIterations = 128;

        /// <summary>
        /// Converts serialized Unity fields into a pure C# settings object used by the planner.
        /// </summary>
        public MapfPlannerSettings ToSettings()
        {
            return new MapfPlannerSettings
            {
                AgentRadius = agentRadius,
                AgentSpeed = agentSpeed,
                TimeLimitSeconds = timeLimitSeconds,
                MaxHighLevelNodes = maxHighLevelNodes,
                MaxLowLevelNodes = maxLowLevelNodes,
                MaxLocalRepairIterations = maxLocalRepairIterations,
                ReplanStrategy = ReplanStrategy.AffectedAgentWithGlobalFallback
            };
        }
    }
}
