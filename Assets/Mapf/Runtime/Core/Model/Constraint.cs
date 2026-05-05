namespace Mapf.Core.Model
{
    /// <summary>
    /// Low-level planning constraint forbidding an agent from occupying a node or starting an edge traversal in a time interval.
    /// </summary>
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
