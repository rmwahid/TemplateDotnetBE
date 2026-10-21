using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Service.Common;

/// <summary>
/// Transaction outcome metrics. Counters answer "how often" without putting a
/// production Error log on expected business paths; tracing still carries the
/// operation name.
/// </summary>
internal static class UnitOfWorkMetrics
{
    private static readonly Meter Meter = new("TemplateDotnetBE.UnitOfWork", "1.0");

    private static readonly Counter<long> Commits =
        Meter.CreateCounter<long>("uow.commits");

    private static readonly Counter<long> Rollbacks =
        Meter.CreateCounter<long>("uow.rollbacks");

    private static readonly Counter<long> Failures =
        Meter.CreateCounter<long>("uow.failures");

    private static readonly Counter<long> Cancellations =
        Meter.CreateCounter<long>("uow.cancellations");

    private static readonly Counter<long> Reads =
        Meter.CreateCounter<long>("uow.reads");

    private static readonly Counter<long> Replays =
        Meter.CreateCounter<long>("uow.idempotent_replays");

    private static readonly Counter<long> FailureSideEffects =
        Meter.CreateCounter<long>("uow.failure_side_effects");

    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("uow.duration", unit: "ms");

    private static KeyValuePair<string, object?> Operation(string operationName) =>
        new("operation", operationName);

    public static void Committed(string operationName, double elapsedMs)
    {
        var operation = Operation(operationName);
        Commits.Add(1, operation);
        Duration.Record(elapsedMs, operation,
            new KeyValuePair<string, object?>("outcome", "committed"));
    }

    public static void BusinessRolledBack(string operationName, double elapsedMs)
    {
        var operation = Operation(operationName);
        Rollbacks.Add(1, operation,
            new KeyValuePair<string, object?>("outcome", "business"));
        Duration.Record(elapsedMs, operation,
            new KeyValuePair<string, object?>("outcome", "rolled_back"));
    }

    public static void Failed(string operationName, double elapsedMs)
    {
        var operation = Operation(operationName);
        Failures.Add(1, operation);
        Duration.Record(elapsedMs, operation,
            new KeyValuePair<string, object?>("outcome", "failed"));
    }

    public static void Cancelled(string operationName, double elapsedMs)
    {
        var operation = Operation(operationName);
        Cancellations.Add(1, operation);
        Duration.Record(elapsedMs, operation,
            new KeyValuePair<string, object?>("outcome", "cancelled"));
    }

    public static void ReadCompleted(string operationName, double elapsedMs)
    {
        var operation = Operation(operationName);
        Reads.Add(1, operation);
        Duration.Record(elapsedMs, operation,
            new KeyValuePair<string, object?>("outcome", "read"));
    }

    public static void Replayed(string operationName)
    {
        Replays.Add(1, Operation(operationName));
    }

    public static void FailureSideEffectCommitted(string operationName, double elapsedMs)
    {
        var operation = Operation(operationName);
        FailureSideEffects.Add(1, operation);
        Duration.Record(elapsedMs, operation,
            new KeyValuePair<string, object?>("outcome", "failure_side_effect"));
    }
}
