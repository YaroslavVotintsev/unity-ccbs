namespace Mapf.Core.Planning
{
    /// <summary>
    /// Terminal status for a MAPF planning attempt.
    /// </summary>
    public enum PlannerStatus
    {
        Success,
        NoSolution,
        Cancelled,
        Timeout
    }
}
