using System;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

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
        private readonly CancellationTokenSource _connectionCts; //  The CancellationToken used to signal disconnection
        #endregion

        #region Properties
        /// <summary>
        /// Gets the unique client identifier.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// Gets the unique user identifier.
        /// </summary>
        public Guid? UserId { get; set; } // added this to track connected users

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

        /// <summary>
        /// Gets the value indicating if client is connected.
        /// </summary>
        public bool MarkedForDisconnection { get; internal set; }
        #endregion

        #region Constructor
        internal ServerSentEventsClient(Guid id, ClaimsPrincipal user, HttpResponse response, ILogger logger, CancellationTokenSource connectionCts)
        {
            Id = id;
            User = user ?? throw new ArgumentNullException(nameof(user));

            _response = response ?? throw new ArgumentNullException(nameof(response));
            _logger = logger; // Allows error to be logged when sending Event, can be null
            _connectionCts = connectionCts;
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

        internal async Task<bool> SendAsync(ServerSentEventBytes serverSentEvent, CancellationToken cancellationToken, LogLevel minLogLevel = LogLevel.Debug)
        {
            try {
                CheckIsConnected(); // it is up to the caller to first check IsConnected, this will throw if IsConnected == false
                if (MarkedForDisconnection) {
                    _logger.Log(minLogLevel, $"Client SendAsync: Client is Marked for Disconnection, event will not be sent to: {this.ToString()}");
                    return false;
                }
                _logger.Log(minLogLevel, $"Client SendAsync: Sending Event to: {this.ToString()}");
                await _response.WriteAsync(serverSentEvent, cancellationToken);
                return true;
            } catch (Exception ex) {
                if (_logger != null) {
                    _logger.LogError($"Client SendAsync: Error: {this.ToString()} Exception: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Sends a hello message to the client
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task<bool> SendHelloEventAsync(string message)
        {
            return SendEventAsync(new ServerSentEvent { Id = "HELLO", Data = new List<string>() { message }});
        }

        /// <summary>
        /// Sends an error message to the client
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task<bool> SendErrorEventAsync(string message)
        {
            return SendEventAsync(new ServerSentEvent { Id = "ERROR", Data = new List<string>() { message }});
        }

        /// <summary>
        /// Instructs the client to disconnect from the server.
        /// </summary>
        /// <param name="message">The message to send, reason for disconnect.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<bool> SendCloseEventAsync(string message)
        {
            bool sent = await SendEventAsync(new ServerSentEvent { Id = "CLOSE", Data = new List<string>() { message }});
            if (sent) {
                MarkedForDisconnection = true; // client will be ignored when sending events
            }
            return sent;
        }

        /// <summary>
        /// Instructs the client to disconnect from the server.
        /// </summary>
        /// <param name="message">The message to send, reason for disconnect.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<bool> SendCloseEventAsync(string message, CancellationToken cancellationToken)
        {
            bool sent = await SendEventAsync(new ServerSentEvent { Id = "CLOSE", Data = new List<string>() { message }}, cancellationToken);
            if (sent) {
                MarkedForDisconnection = true; // client will be ignored when sending events
            }
            return sent;
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
        /// Closes the connection to the client.
        /// </summary>
        /// <returns>whether the operation succeeded</returns>
        public bool CloseConnection()
        {
            try {
                CheckIsConnected(); // it is up to the caller to first check IsConnected, this will throw if IsConnected == false
                if (_connectionCts != null) {
                    if (!_connectionCts.IsCancellationRequested) {
                        _logger.LogDebug($"Client CloseAsync: triggering _connectionCts.Cancel()");
                        _connectionCts.Cancel();
                    } else {
                        _logger.LogDebug($"Client CloseAsync: _connectionCts.Cancel() has already been triggered");
                    }
                    return true;
                }
                return false;
            } catch (Exception ex) {
                if (_logger != null) {
                    _logger.LogError($"Client CloseAsync: Error triggering _connectionCts.Cancel(), Exception: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// ToString() override
        /// </summary>
        public override string ToString() => $"Client: Id: '{Id.ToString()} User.Identity.Name: {User?.Identity?.Name} ConnectedAt: {ConnectedAt} LastEventId: {LastEventId}";
        #endregion
    }
}
