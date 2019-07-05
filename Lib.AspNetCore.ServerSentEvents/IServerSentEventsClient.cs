using System;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;

namespace Lib.AspNetCore.ServerSentEvents
{
    /// <summary>
    /// Contract for client listening for Server-Sent Events
    /// </summary>
    public interface IServerSentEventsClient
    {
        #region Properties
        /// <summary>
        /// Gets the unique client identifier.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the unique user identifier.
        /// </summary>
        Guid? UserId { get; set; }  // added this to track connected users

        /// <summary>
        /// Gets the System.Security.Claims.ClaimsPrincipal for user associated with the client.
        /// </summary>
        ClaimsPrincipal User { get; }

        /// <summary>
        /// Gets or sets the string LastEventId sent to the client.
        /// </summary>
        string LastEventId { get; set; }

        /// <summary>
        /// Gets the value indicating if client is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the value indicating if client is connected.
        /// </summary>
        bool MarkedForDisconnection { get; }
        #endregion

        #region Methods
        /// <summary>
        /// Sends event to client.
        /// </summary>
        /// <param name="text">The simple text event.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<bool> SendEventAsync(string text);

        /// <summary>
        /// Sends event to client.
        /// </summary>
        /// <param name="text">The simple text event.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<bool> SendEventAsync(string text, CancellationToken cancellationToken);

        /// <summary>
        /// Sends event to client.
        /// </summary>
        /// <param name="serverSentEvent">The event.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<bool> SendEventAsync(ServerSentEvent serverSentEvent);

        /// <summary>
        /// Sends event to client.
        /// </summary>
        /// <param name="serverSentEvent">The event.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<bool> SendEventAsync(ServerSentEvent serverSentEvent, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a hello message to the client
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<bool> SendHelloEventAsync(string message);

        /// <summary>
        /// Sends an error message to the client
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<bool> SendErrorEventAsync(string message);

        /// <summary>
        /// Instructs the client to disconnect from the server.
        /// </summary>
        /// <param name="message">The message to send, reason for disconnect.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<bool> SendCloseEventAsync(string message);

        /// <summary>
        /// Instructs the client to disconnect from the server.
        /// </summary>
        /// <param name="message">The message to send, reason for disconnect.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<bool> SendCloseEventAsync(string message, CancellationToken cancellationToken);

        /// <summary>
        /// Closes the connection to the client.
        /// </summary>
        /// <returns>whether the operation succeeded</returns>
        bool CloseConnection();

        #endregion
    }
}
