namespace Mapf.Core.Planning
{
    /// <summary>
    /// Strategy used when runtime goal changes request replanning.
    /// </summary>
    public enum ReplanStrategy
    {
        Global,
        AffectedAgentWithGlobalFallback
    }
}
