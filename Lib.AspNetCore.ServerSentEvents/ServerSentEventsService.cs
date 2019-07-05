using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Lib.AspNetCore.ServerSentEvents.Internals;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Lib.AspNetCore.ServerSentEvents
{
    /// <summary>
    /// Service which provides operations over Server-Sent Events protocol.
    /// </summary>
    public class ServerSentEventsService : IServerSentEventsService
    {
        #region Events
        /// <summary>
        /// Occurs when client has connected.
        /// </summary>
        public event EventHandler<ServerSentEventsClientConnectedArgs> ClientConnected;

        /// <summary>
        /// Occurs when client has disconnected.
        /// </summary>
        public event EventHandler<ServerSentEventsClientDisconnectedArgs> ClientDisconnected;
        #endregion

        #region Fields
        private readonly ConcurrentDictionary<Guid, ServerSentEventsClient> _clients = new ConcurrentDictionary<Guid, ServerSentEventsClient>();

        private readonly ConcurrentDictionary<Guid, IServerSentEventsClient> _userClients = new ConcurrentDictionary<Guid, IServerSentEventsClient>();

        /// <summary>
        /// Logger instance
        /// </summary>
        protected ILogger _logger;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the interval after which clients will attempt to reestablish failed connections.
        /// </summary>
        public uint? ReconnectInterval { get; private set; }

        /// <summary>
        /// Gets the delay after which a client is disconnected due to another client connecting with the same userId.
        /// </summary>
        public int DisconnectionDelay { get; private set; } = 5000;
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        public ServerSentEventsService(ILogger<ServerSentEventsService> logger) {
            _logger = logger;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the client based on the unique user identifier.
        /// </summary>
        /// <param name="userId">The unique user identifier.</param>
        /// <returns>The client.</returns>
        public IServerSentEventsClient GetUserClient(Guid userId)
        {
            IServerSentEventsClient client;

            _userClients.TryGetValue(userId, out client);

            return client;
        }

        /// <summary>
        /// Gets all clients.
        /// </summary>
        /// <returns>The user clients.</returns>
        public IReadOnlyCollection<IServerSentEventsClient> GetUserClients()
        {
            return _userClients.Values.ToArray();
        }

        /// <summary>
        /// Gets all clients as IQueryable.
        /// </summary>
        /// <returns>The user clients.</returns>
        public IQueryable<IServerSentEventsClient> GetUserClientsAsQueryable()
        {
            return _userClients.Values.AsQueryable();
        }

        /// <summary>
        /// Gets the client based on the unique client identifier.
        /// </summary>
        /// <param name="clientId">The unique client identifier.</param>
        /// <returns>The client.</returns>
        public IServerSentEventsClient GetClient(Guid clientId)
        {
            ServerSentEventsClient client;

            _clients.TryGetValue(clientId, out client);

            return client;
        }

        /// <summary>
        /// Gets all clients.
        /// </summary>
        /// <returns>The clients.</returns>
        public IReadOnlyCollection<IServerSentEventsClient> GetClients()
        {
            return _clients.Values.ToArray();
        }

        /// <summary>
        /// Gets all clients as IQueryable.
        /// </summary>
        /// <returns>The clients.</returns>
        public IQueryable<IServerSentEventsClient> GetClientsAsQueryable()
        {
            return _clients.Values.AsQueryable();
        }

        /// <summary>
        /// Gets the number of connected clients
        /// </summary>
        /// <returns>The clients.</returns>
        public int GetClientCount()
        {
            return _clients.Count;
        }

        /// <summary>
        /// Changes the interval after which all clients will attempt to reestablish failed connections.
        /// </summary>
        /// <param name="reconnectInterval">The reconnect interval.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task ChangeReconnectIntervalAsync(uint reconnectInterval)
        {
            return ChangeReconnectIntervalAsync(reconnectInterval, CancellationToken.None);
        }

        /// <summary>
        /// Changes the interval after which all clients will attempt to reestablish failed connections.
        /// </summary>
        /// <param name="reconnectInterval">The reconnect interval.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task ChangeReconnectIntervalAsync(uint reconnectInterval, CancellationToken cancellationToken)
        {
            ReconnectInterval = reconnectInterval;

            ServerSentEventBytes reconnectIntervalBytes = ServerSentEventsHelper.GetReconnectIntervalBytes(reconnectInterval);

            return SendAsync(reconnectIntervalBytes, cancellationToken);
        }

        /// <summary>
        /// Sends an event message to all connected clients
        /// </summary>
        /// <param name="eventId">The eventId to send.</param>
        /// <param name="eventType">The eventType to send.</param>
        /// <param name="eventData">The eventData to send.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected Task SendSseEventAsync(string eventId, string eventType, string eventData) {
            _logger.LogDebug($"SendSseEventAsync: Sending eventId: {eventId} eventType: {eventType} to all connected clients...");
            return SendEventAsync(new ServerSentEvent {
                Id = eventId,
                Type = eventType, // If type is null, defaults to "message"
                Data = new List<string>(eventData.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None))
            });
        }

        /// <summary>
        /// Sends an event message to a specific connected client
        /// </summary>
        /// <param name="clientId">The clientId of the client.</param>
        /// <param name="eventId">The eventId to send.</param>
        /// <param name="eventType">The eventType to send.</param>
        /// <param name="eventData">The eventData to send.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected async Task<bool> SendSseEventAsync(Guid clientId, string eventId, string eventType, string eventData) {
            var client = GetClient(clientId);
            if (client != null) {
                if (client.IsConnected) {
                    _logger.LogDebug($"SendSseEventAsync: Client Found, Sending eventId: {eventId} eventType: {eventType} to Client with Id: {client.Id}");
                    return await client.SendEventAsync(new ServerSentEvent {
                        Id = eventId,
                        Type = eventType, // If type is null, defaults to "message"
                        Data = new List<string>(eventData.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None))
                    });
                } else {
                    _logger.LogDebug($"SendSseEventAsync: Client Found, Unable to send event with Id: {eventId} to Client with Id: {client.Id} as the client is not connected");
                }
            } else {
                _logger.LogDebug($"SendSseEventAsync: Client not found, Client with Id: {clientId} is not connected");
            }
            return false;
        }

        
        /// <summary>
        /// Sends an event message to a specific connected user
        /// </summary>
        /// <param name="userId">The userId of the client.</param>
        /// <param name="eventId">The eventId to send.</param>
        /// <param name="eventType">The eventType to send.</param>
        /// <param name="eventData">The eventData to send.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected async Task<bool> SendSseEventToUserAsync(Guid userId, string eventId, string eventType, string eventData) {
            var client = GetUserClient(userId);
            if (client != null) {
                if (client.IsConnected) {
                    _logger.LogDebug($"SendSseEventToUserAsync: Client Found for userId: {userId} , Sending eventId: {eventId} eventType: {eventType} to Client with Id: {client.Id}");
                    return await client.SendEventAsync(new ServerSentEvent {
                        Id = eventId,
                        Type = eventType, // If type is null, defaults to "message"
                        Data = new List<string>(eventData.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None))
                    });
                } else {
                    _logger.LogDebug($"SendSseEventToUserAsync: Client Found, Unable to send event with Id: {eventId} to Client with userId: {client.UserId} as the client is not connected");
                }
            } else {
                _logger.LogDebug($"SendSseEventToUserAsync: Client not found, Client with userId: {userId} is not connected");
            }
            return false;
        }

        /// <summary>
        /// Sends event to all clients.
        /// </summary>
        /// <param name="text">The simple text event.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task SendEventAsync(string text)
        {
            return SendAsync(ServerSentEventsHelper.GetEventBytes(text), CancellationToken.None);
        }

        /// <summary>
        /// Sends event to all clients.
        /// </summary>
        /// <param name="text">The simple text event.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task SendEventAsync(string text, CancellationToken cancellationToken)
        {
            return SendAsync(ServerSentEventsHelper.GetEventBytes(text), cancellationToken);
        }

        /// <summary>
        /// Sends event to all clients.
        /// </summary>
        /// <param name="serverSentEvent">The event.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task SendEventAsync(ServerSentEvent serverSentEvent)
        {
            return SendAsync(ServerSentEventsHelper.GetEventBytes(serverSentEvent), CancellationToken.None);
        }

        /// <summary>
        /// Sends event to all clients.
        /// </summary>
        /// <param name="serverSentEvent">The event.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task SendEventAsync(ServerSentEvent serverSentEvent, CancellationToken cancellationToken)
        {
            return SendAsync(ServerSentEventsHelper.GetEventBytes(serverSentEvent), cancellationToken);
        }

        /// <summary>
        /// Method which is called when client is establishing the connection. The base implementation raises the <see cref="ClientConnected"/> event.
        /// </summary>
        /// <param name="request">The request which has been made in order to establish the connection.</param>
        /// <param name="client">The client who is establishing the connection.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual Task OnConnectAsync(HttpRequest request, IServerSentEventsClient client)
        {
            ClientConnected?.Invoke(this, new ServerSentEventsClientConnectedArgs(request, client));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Method which is called when client is reestablishing the connection. The base implementation raises the <see cref="ClientConnected"/> event.
        /// </summary>
        /// <param name="request">The request which has been made in order to establish the connection.</param>
        /// <param name="client">The client who is reestablishing the connection.</param>
        /// <param name="lastEventId">The identifier of last event which client has received.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual Task OnReconnectAsync(HttpRequest request, IServerSentEventsClient client, string lastEventId)
        {
            ClientConnected?.Invoke(this, new ServerSentEventsClientConnectedArgs(request, client, lastEventId));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Method which is called when client is disconnecting. The base implementation raises the <see cref="ClientDisconnected"/> event.
        /// </summary>
        /// <param name="request">The original request which has been made in order to establish the connection.</param>
        /// <param name="client">The client who is disconnecting.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual Task OnDisconnectAsync(HttpRequest request, IServerSentEventsClient client)
        {
            ClientDisconnected?.Invoke(this, new ServerSentEventsClientDisconnectedArgs(request, client));

            return Task.CompletedTask;
        }

        internal async Task AddClient(ServerSentEventsClient client)
        {
            if (client.UserId != null) {
                _logger.LogDebug($"AddClient: client.UserId: {client.UserId}");
                // If the client is reconnecting it sends it's last eventId
                // Deal with any zombie clients (clients that refuse to disconnect when issued with "CLOSE" eventId)
                // Any zombie clients would just keep trying to re-connect with a last eventId of "CLOSE"
                if (client.LastEventId == "CLOSE") {
                    await client.ChangeReconnectIntervalAsync(86400000, CancellationToken.None); // set to a long time, such as 24 hours
                    bool sendSuccess = await client.SendErrorEventAsync($"The last eventId was \"CLOSE\", reconnection is not allowed");
                    _logger.LogDebug($"AddClient: client.SendErrorEventAsync() success: {sendSuccess}");
                    sendSuccess = await client.SendCloseEventAsync("Reconnection attempt refused");
                    _logger.LogDebug($"AddClient: removedClient.SendCloseEventAsync() success: {sendSuccess}");
                    await Task.Delay(DisconnectionDelay);
                    client.CloseConnection();
                    return; // prevent a reconnecting client from disconnecting an existing client
                }
                // Check if a client with same userId is already connected, only allow 1 client with same userId to be connected at any one time
                // The last connected client disconnects any previous client with the same userId.
                if (_userClients.ContainsKey(client.UserId.Value)) {                   
                    IServerSentEventsClient removedClient;
                    _userClients.TryRemove(client.UserId.Value, out removedClient);
                    bool success = await removedClient.SendErrorEventAsync($"Another client with the same UserId has connected, the connection will be closed");
                    _logger.LogDebug($"AddClient: client.SendErrorEventAsync() success: {success}");
                    // Instruct the previous client to disconnect
                    success = await removedClient.SendCloseEventAsync($"Duplicate Client (UserId: {removedClient.UserId})");
                    _logger.LogDebug($"AddClient: removedClient.SendCloseEventAsync() success: {success}");
                    // In case the client does not honour the close instruction, wait for short delay and then close the connection
                    await Task.Delay(DisconnectionDelay);
                    // If the connection is just closed from the server side without first asking the client to disconnect, the client would just auto reconnect (depending on client side EventSource library)
                    // resulting in a race condition between multiple clients with the same userId trying to reconnect and disconnecting each other
                    removedClient.CloseConnection(); // Can't be used on its own
                }
                _userClients.TryAdd(client.UserId.Value, client);
            }
            bool added = _clients.TryAdd(client.Id, client);
            if (added) {
                string message = null;
                if (client.User != null) {
                    message = $"Hello {client.User.Identity?.Name} from {this.GetType().Name}";
                } else {
                    message = $"Hello from {this.GetType().Name}";
                }
                bool success = await client.SendHelloEventAsync(message);
                _logger.LogDebug($"AddClient: client.SendHelloEventAsync() success: {success}");
            }
        }

        internal void RemoveClient(ServerSentEventsClient client)
        {
            // If the client has a userId, check if the same client is stored in _userClients for the same userId, remove if match found
            if (client.UserId.HasValue) {
                IServerSentEventsClient userClient = GetUserClient(client.UserId.Value);
                if (userClient != null && userClient.Id.Equals(client.Id)) {
                    _userClients.TryRemove(client.UserId.Value, out userClient);
                }
            }
            client.IsConnected = false;
            _clients.TryRemove(client.Id, out client);
        }

        internal void DisconnectClientsMarkedForDisconnection(LogLevel minLogLevel = LogLevel.Information) {
            int clientCount = GetClientCount();
            int disconnectedCount = 0;
            if (clientCount > 0) {
                _logger.Log(minLogLevel, $"DisconnectClientsMarkedForDisconnection: Disconnecting Clients Marked For Disconnection");
                foreach (ServerSentEventsClient client in _clients.Values) {
                    if (client.IsConnected && client.MarkedForDisconnection) {
                        client.CloseConnection();
                        disconnectedCount++;
                    }                 
                }
                _logger.Log(minLogLevel, $"{DateTime.Now.ToString()} : DisconnectClientsMarkedForDisconnection: Disconnected {disconnectedCount} Clients Marked For Disconnection");
            } else {
                _logger.Log(minLogLevel, $"DisconnectClientsMarkedForDisconnection: There are no Connected Clients");
            }
        }

        internal async Task<int> SendAsync(ServerSentEventBytes serverSentEventBytes, CancellationToken cancellationToken, LogLevel minLogLevel = LogLevel.Information)
        {
            int clientCount = GetClientCount();
            int sentCount = 0;
            if (clientCount > 0) {
                _logger.Log(minLogLevel, $"{DateTime.Now.ToString()} : SendAsync: Sending Event to {clientCount} Clients...");
                List<Task> clientsTasks = new List<Task>();
                foreach (ServerSentEventsClient client in _clients.Values)
                {
                    if (client.IsConnected)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        Task operationTask = client.SendAsync(serverSentEventBytes, cancellationToken, minLogLevel);
                        sentCount++;

                        if (operationTask.Status != TaskStatus.RanToCompletion)
                        {
                            clientsTasks.Add(operationTask);
                        }
                    } else {
                        _logger.LogWarning($"SendAsync: Client: {client.Id} was not connected, Event was not sent");
                    }
                }
                await Task.WhenAll(clientsTasks);
                _logger.Log(minLogLevel, $"{DateTime.Now.ToString()} : SendAsync: Event sent to {sentCount} Connected Clients...");
                return sentCount;
            } else {
                _logger.Log(minLogLevel, $"SendAsync: There are no Connected Clients");
            }
            return sentCount;
        }
        #endregion
    }
}
