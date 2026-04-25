namespace Mapf.Core.Model
{
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
