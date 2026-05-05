namespace Mapf.Core.Model
{
    /// <summary>
    /// Timed path segment that must be respected by the planner but may not belong to the currently planned agent set.
    /// Used for committed mid-edge motion during replanning.
    /// </summary>
    public readonly struct Reservation
    {
        public readonly int AgentId;
        public readonly TimedPath Path;

        public Reservation(int agentId, TimedPath path)
        {
            AgentId = agentId;
            Path = path;
        }
    }
}
