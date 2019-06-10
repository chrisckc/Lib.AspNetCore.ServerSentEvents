using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Lib.AspNetCore.ServerSentEvents
{
    /// <summary>
    /// Options for Server-Sent Events endpoint.
    /// </summary>
    public class ServerSentEventsOptions
    {
        /// <summary>
        /// Initializes new instance of <see cref="ServerSentEventsOptions"/>.
        /// </summary>
        public ServerSentEventsOptions()
        {
            OnPrepareAccept = _ => { };
            OnShouldAccept = _ => true;
        }

        /// <summary>
        /// Gets or sets the authorization rules.
        /// </summary>
        public ServerSentEventsAuthorization Authorization { get; set; }

        /// <summary>
        /// Called before the status code and Content-Type  have been set, and before the headers have been written.
        /// This can be used to check and reject the request if required.
        /// </summary>
        public Func<HttpContext, bool> OnShouldAccept { get; set; }

        /// <summary>
        /// Called after the status code and Content-Type  have been set, but before the headers has been written.
        /// This can be used to add or change the response headers.
        /// </summary>
        public Action<HttpResponse> OnPrepareAccept { get; set; }
    }
}
