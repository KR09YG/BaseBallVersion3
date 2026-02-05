public readonly struct RunnerETA
{
    public readonly Runner Runner;
    public readonly BaseId TargetBase;
    public readonly float Remaining;

    public RunnerETA(Runner runner, BaseId targetBase, float remaining)
    {
        Runner = runner;
        TargetBase = targetBase;
        Remaining = remaining;
    }
}
