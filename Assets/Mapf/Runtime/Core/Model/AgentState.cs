namespace Mapf.Core.Model
{
    /// <summary>
    /// Planner input state for one agent: current planning node, goal node, and earliest allowed start time.
    /// </summary>
    public readonly struct AgentState
    {
        public readonly int AgentId;
        public readonly int StartNodeId;
        public readonly int GoalNodeId;
        public readonly double EarliestStartTime;

        public AgentState(int agentId, int startNodeId, int goalNodeId, double earliestStartTime = 0)
        {
            AgentId = agentId;
            StartNodeId = startNodeId;
            GoalNodeId = goalNodeId;
            EarliestStartTime = earliestStartTime;
        }
    }
}
