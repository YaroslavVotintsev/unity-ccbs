namespace Mapf.UnityAdapter
{
    /// <summary>
    /// Identifiers for built-in scenario presets available through <see cref="MapfScenarioSpawner"/>.
    /// </summary>
    public enum MapfScenarioPreset
    {
        BasicStraightLineSingleAgent,
        BasicCrossIntersection,
        BasicSidestepSwap,
        BasicPassingLoop,
        BasicWaitBayMerge,
        BasicThreeAgentCorridorWithTwoBays,
        BasicLoggedElevenNodeThreeAgent,
        ThreeAgentsElevenNodeOppositeEnds,
        FourAgentsTwelveNodeOppositeEnds,
        FiveAgentsThirteenNodeOppositeEnds,
        FiveAgentsLongSideBayCorridor
    }
}
