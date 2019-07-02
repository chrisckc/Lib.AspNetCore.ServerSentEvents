using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Lib.AspNetCore.ServerSentEvents.Internals;
using Microsoft.Extensions.Logging;

namespace Lib.AspNetCore.ServerSentEvents
{
    internal class ServerSentEventsKeepaliveService<TServerSentEventsService> : IHostedService, IDisposable
        where TServerSentEventsService : ServerSentEventsService
    {
        #region Fields
        private readonly bool _isBehindAncm = IsBehindAncm();
        private readonly static ServerSentEventBytes _keepaliveServerSentEventBytes = ServerSentEventsHelper.GetCommentBytes("KEEPALIVE");

        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();

        private readonly ServerSentEventsServiceOptions<TServerSentEventsService> _options;
        private readonly TServerSentEventsService _serverSentEventsService;

        private readonly ILogger _logger;

        private Task _executingTask;
        #endregion

        #region Constructor
        public ServerSentEventsKeepaliveService(TServerSentEventsService serverSentEventsService, IOptions<ServerSentEventsServiceOptions<TServerSentEventsService>> options, ILogger<ServerSentEventsKeepaliveService<TServerSentEventsService>> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _serverSentEventsService = serverSentEventsService;
            _logger = logger;
            _logger.LogDebug($"Constructor: _options.KeepaliveMode: {_options.KeepaliveMode}");
            _logger.LogDebug($"Constructor: _isBehindAncm: {_isBehindAncm}");
        }
        #endregion

        #region Methods
        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            if ((_options.KeepaliveMode == ServerSentEventsKeepaliveMode.Always) || ((_options.KeepaliveMode == ServerSentEventsKeepaliveMode.BehindAncm) && _isBehindAncm))
            {
                _logger.LogDebug($"StartAsync: executing task...");
                _executingTask = ExecuteAsync(_stoppingCts.Token);

                if (_executingTask.IsCompleted)
                {
                    _logger.LogDebug($"StartAsync: task completed");
                    return _executingTask;
                }
            } else {
                _logger.LogDebug($"StartAsync: nothing to do");
            }
            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null)
            {
                return;
            }

            try
            {
                _stoppingCts.Cancel();
            }
            finally
            {
                await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }

        }

        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug($"ExecuteAsync: _options.KeepaliveInterval: {_options.KeepaliveInterval}");
            while (!stoppingToken.IsCancellationRequested)
            {
                int clientCount =_serverSentEventsService.GetClientCount();
                if (clientCount > 0) {
                    _logger.LogTrace($"ExecuteAsync: Sending keepalive to {clientCount} clients...");
                    int sentCount = await _serverSentEventsService.SendAsync(_keepaliveServerSentEventBytes, CancellationToken.None, LogLevel.Trace);
                    _logger.LogTrace($"ExecuteAsync: Sent keepalive to {sentCount} connected clients...");
                }
                await Task.Delay(TimeSpan.FromSeconds(_options.KeepaliveInterval), stoppingToken);
            }
        }

        private static bool IsBehindAncm()
        {
            return !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_PORT"))
                && !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_APPL_PATH"))
                && !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_TOKEN"));
        }

        public virtual void Dispose()
        {
            _stoppingCts.Cancel();
        }
        #endregion
    }
}
