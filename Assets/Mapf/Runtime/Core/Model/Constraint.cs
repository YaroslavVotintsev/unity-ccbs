namespace Mapf.Core.Model
{
    public readonly struct Constraint
    {
        public readonly int AgentId;
        public readonly int FromNodeId;
        public readonly int ToNodeId;
        public readonly double StartTime;
        public readonly double EndTime;

        public Constraint(int agentId, int fromNodeId, int toNodeId, double startTime, double endTime)
        {
            AgentId = agentId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            StartTime = startTime;
            EndTime = endTime;
        }

        public bool IsNodeConstraint => FromNodeId == ToNodeId;
    }
}
