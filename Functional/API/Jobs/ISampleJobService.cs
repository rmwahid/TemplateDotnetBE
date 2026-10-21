namespace API.Jobs;

public interface ISampleJobService
{
    Task RunSampleJobAsync();
    Task RunRecurringJobAsync();
}
