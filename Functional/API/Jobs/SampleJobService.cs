namespace API.Jobs;

public class SampleJobService : ISampleJobService
{
    public Task RunSampleJobAsync()
    {
        Console.WriteLine($"[{DateTime.UtcNow:O}] Sample background job executed.");
        return Task.CompletedTask;
    }

    public Task RunRecurringJobAsync()
    {
        Console.WriteLine($"[{DateTime.UtcNow:O}] Recurring background job executed.");
        return Task.CompletedTask;
    }
}
