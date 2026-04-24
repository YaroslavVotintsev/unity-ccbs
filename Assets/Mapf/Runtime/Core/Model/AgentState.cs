namespace Mapf.Core.Model
{
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
