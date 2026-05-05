namespace Mapf.Core.Model
{
    /// <summary>
    /// Continuous-time collision between two timed moves.
    /// </summary>
    public readonly struct Conflict
    {
        public readonly int AgentA;
        public readonly int AgentB;
        public readonly TimedMove MoveA;
        public readonly TimedMove MoveB;
        public readonly double Time;

        public Conflict(int agentA, int agentB, TimedMove moveA, TimedMove moveB, double time)
        {
            AgentA = agentA;
            AgentB = agentB;
            MoveA = moveA;
            MoveB = moveB;
            Time = time;
        }

        public bool IsValid => AgentA >= 0 && AgentB >= 0 && AgentA != AgentB;
    }
}
