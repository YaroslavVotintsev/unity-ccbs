namespace Mapf.Core.Planning
{
    public sealed class MapfPlannerSettings
    {
        public double AgentRadius { get; set; } = 0.35;
        public double AgentSpeed { get; set; } = 1.0;
        public double TimeLimitSeconds { get; set; } = 5.0;
        public double Epsilon { get; set; } = 1e-6;
        public int MaxHighLevelNodes { get; set; } = 20000;
        public int MaxLowLevelNodes { get; set; } = 5000;
        public int MaxLocalRepairIterations { get; set; } = 128;
        public ReplanStrategy ReplanStrategy { get; set; } = ReplanStrategy.AffectedAgentWithGlobalFallback;
    }
}
