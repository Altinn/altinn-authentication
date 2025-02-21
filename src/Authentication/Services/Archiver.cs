using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Microsoft.Extensions.Hosting;

namespace ArchiverService;

/// <summary>
/// IHost Service Worker to archive old data regurlarly
/// </summary>
/// <param name="requestRepository">The request repository class</param>
public sealed class Archiver(IRequestRepository requestRepository) : BackgroundService
{
    private const int SOFT_DELETE_TIMEOUT_DAYS = 28;
    private const int COPY_ARCHIVE_TIMEOUT_DAYS = 30;

    /// <summary>
    /// The work is done here
    /// </summary>
    /// <param name="stoppingToken">The stopping token</param>
    /// <returns></returns>    
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(10000, stoppingToken);        
            await requestRepository.SetDeleteTimedoutRequests(SOFT_DELETE_TIMEOUT_DAYS);
            await Task.Delay(10000, stoppingToken);
            await requestRepository.CopyOldRequestsToArchive(COPY_ARCHIVE_TIMEOUT_DAYS);
        }
    }
}
