namespace UniScheduler.Application.Common.Interfaces;

public interface IAppUrls
{
    // Public base URL of the frontend, used to build links sent in emails.
    string BaseUrl { get; }
}
