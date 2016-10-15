﻿using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Ocelot.Library.Infrastructure.Configuration;
using Ocelot.Library.Infrastructure.DownstreamRouteFinder;
using Ocelot.Library.Infrastructure.Errors;
using Ocelot.Library.Infrastructure.Repository;
using Ocelot.Library.Infrastructure.Responses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ocelot.Library.Infrastructure.Authentication;

namespace Ocelot.Library.Infrastructure.Middleware
{
    public class AuthenticationMiddleware : OcelotMiddleware
    {
        private readonly RequestDelegate _next;
        private RequestDelegate _authenticationNext;
        private readonly IScopedRequestDataRepository _scopedRequestDataRepository;
        private readonly IApplicationBuilder _app;
        private readonly IAuthenticationProviderFactory _authProviderFactory;

        public AuthenticationMiddleware(RequestDelegate next, IApplicationBuilder app,
            IScopedRequestDataRepository scopedRequestDataRepository, IAuthenticationProviderFactory authProviderFactory) 
            : base(scopedRequestDataRepository)
        {
            _next = next;
            _scopedRequestDataRepository = scopedRequestDataRepository;
            _authProviderFactory = authProviderFactory;
            _app = app;
        }

        public async Task Invoke(HttpContext context)
        {
            var downstreamRoute = _scopedRequestDataRepository.Get<DownstreamRoute>("DownstreamRoute");

            if (downstreamRoute.IsError)
            {
                SetPipelineError(downstreamRoute.Errors);
                return;
            }

            if (IsAuthenticatedRoute(downstreamRoute.Data.ReRoute))
            {
                var authenticationNext = _authProviderFactory.Get(downstreamRoute.Data.ReRoute.AuthenticationProvider, _app);

                if (!authenticationNext.IsError)
                {
                    await authenticationNext.Data.Handler.Invoke(context);
                }
                else
                {
                    SetPipelineError(authenticationNext.Errors);
                }

                if (context.User.Identity.IsAuthenticated)
                {
                    await _next.Invoke(context);
                }
                else
                {   
                    SetPipelineError(new List<Error> {new UnauthenticatedError($"Request for authenticated route {context.Request.Path} by {context.User.Identity.Name} was unauthenticated")});
                }      
            }
            else
            {
                await _next.Invoke(context);
            }
        }

        private static bool IsAuthenticatedRoute(ReRoute reRoute)
        {
            return reRoute.IsAuthenticated;
        }
    }
}
