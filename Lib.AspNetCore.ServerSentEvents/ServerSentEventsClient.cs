using System;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Lib.AspNetCore.ServerSentEvents.Internals
{
    /// <summary>
    /// Represents client listening for Server-Sent Events
    /// </summary>
    public sealed class ServerSentEventsClient : IServerSentEventsClient
    {
        #region Fields
        private readonly HttpResponse _response;
        private readonly ILogger _logger;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the unique client identifier.
        /// </summary>
        public Guid? Id { get; set; } // Changed this to nullable and public settable to allow it to be set later

        /// <summary>
        /// Gets or sets the System.Security.Claims.ClaimsPrincipal for user associated with the client.
        /// </summary>
        public ClaimsPrincipal User { get; private set; }

        /// <summary>
        /// Gets or sets the string LastEventId sent to the client.
        /// </summary>
        public string LastEventId { get; set; }

        /// <summary>
        /// Gets or sets the date the client was connected.
        /// </summary>
        public DateTimeOffset ConnectedAt { get; private set; }

        /// <summary>
        /// Gets the value indicating if client is connected.
        /// </summary>
        public bool IsConnected { get; internal set; }
        #endregion

        #region Constructor
        // Added this additional constructor to support setting Guid later
        internal ServerSentEventsClient(ClaimsPrincipal user, HttpResponse response, ILogger logger)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));

            _response = response ?? throw new ArgumentNullException(nameof(response));
            _logger = logger; // Allows error to be logged when sending Event, can be null
            ConnectedAt = DateTime.UtcNow;
            IsConnected = true;
        }
        internal ServerSentEventsClient(Guid id, ClaimsPrincipal user, HttpResponse response, ILogger logger)
        {
            Id = id;
            User = user ?? throw new ArgumentNullException(nameof(user));

            _response = response ?? throw new ArgumentNullException(nameof(response));
            _logger = logger; // Allows error to be logged when sending Event, can be null
            ConnectedAt = DateTime.UtcNow;
            IsConnected = true;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Sends event to client.
        /// </summary>
        /// <param name="text">The simple text event.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task<bool> SendEventAsync(string text)
        {
            return SendAsync(ServerSentEventsHelper.GetEventBytes(text), CancellationToken.None);
        }

        /// <summary>
        /// Sends event to client.
        /// </summary>
        /// <param name="text">The simple text event.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task<bool> SendEventAsync(string text, CancellationToken cancellationToken)
        {
            return SendAsync(ServerSentEventsHelper.GetEventBytes(text), cancellationToken);
        }

        /// <summary>
        /// Sends event to client.
        /// </summary>
        /// <param name="serverSentEvent">The event.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<bool> SendEventAsync(ServerSentEvent serverSentEvent)
        {
            bool success = await SendAsync(ServerSentEventsHelper.GetEventBytes(serverSentEvent), CancellationToken.None);
            if (success) {
                LastEventId = serverSentEvent.Id;
            }
            return success;
        }

        /// <summary>
        /// Sends event to client.
        /// </summary>
        /// <param name="serverSentEvent">The event.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<bool> SendEventAsync(ServerSentEvent serverSentEvent, CancellationToken cancellationToken)
        {
            bool success = await SendAsync(ServerSentEventsHelper.GetEventBytes(serverSentEvent), cancellationToken);
            if (success) {
                LastEventId = serverSentEvent.Id;
            }
            return success;
        }

        internal async Task<bool> SendAsync(ServerSentEventBytes serverSentEvent, CancellationToken cancellationToken)
        {
            try {
                CheckIsConnected(); // it is up to the caller to first check IsConnected, this will throw if IsConnected == false
                _logger.LogDebug($"Client SendAsync: Sending Event to: {this.ToString()}");
                await _response.WriteAsync(serverSentEvent, cancellationToken);
                return true;
            } catch (Exception ex) {
                if (_logger != null) {
                    _logger.LogError($"Client SendAsync: Error: {this.ToString()} Exception: {ex.Message}");
                }
                return false;
            }
        }

        internal Task<bool> ChangeReconnectIntervalAsync(uint reconnectInterval, CancellationToken cancellationToken)
        {
            return SendAsync(ServerSentEventsHelper.GetReconnectIntervalBytes(reconnectInterval), cancellationToken);
        }

        private void CheckIsConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("The client isn't connected.");
            }
        }

        /// <summary>
        /// ToString() override
        /// </summary>
        public override string ToString() => $"Client: Id: '{Id?.ToString()} User.Identity.Name: {User?.Identity?.Name} ConnectedAt: {ConnectedAt} LastEventId: {LastEventId}";
        #endregion
    }
}
