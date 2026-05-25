using Microsoft.Extensions.Configuration;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Infrastructure;

public class AppUrls : IAppUrls
{
    public AppUrls(IConfiguration configuration)
    {
        BaseUrl = (configuration["App:BaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
    }

    public string BaseUrl { get; }
}
