public interface ITimeProviderService
{
    Task<DateTime> NowAsync();
}
