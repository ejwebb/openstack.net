﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleRestServices.Client;
using SimpleRestServices.Client.Json;
using net.openstack.Core;
using net.openstack.Core.Domain;

namespace net.openstack.Providers.Rackspace
{
    public class ProviderBase
    {
        private readonly Uri _urlBase;
        private readonly IIdentityProvider _identityProvider;
        private readonly IRestService _restService;

        protected ProviderBase(Uri urlBase, IIdentityProvider identityProvider, IRestService restService)
        {
            _urlBase = urlBase;
            _identityProvider = identityProvider;
            _restService = restService;
        }

        protected Response<T> ExecuteRESTRequest<T>(string urlPath, HttpMethod method, object body, CloudIdentity identity, bool isRetry = false, string token = null, JsonRequestSettings requestSettings = null) where T : new()
        {
            var url = new Uri(_urlBase, urlPath);

            return ExecuteRESTRequest<T>(url, method, body, identity, isRetry, token, requestSettings);
        }

        protected Response<T> ExecuteRESTRequest<T>(Uri absoluteUri, HttpMethod method, object body, CloudIdentity identity, bool isRetry = false, string token = null, JsonRequestSettings requestSettings = null) where T : new()
        {
            if (requestSettings == null)
                requestSettings = BuildDefaultRequestSettings();
            
            var headers = new Dictionary<string, string>
                              {
                                  { "X-Auth-Token", string.IsNullOrWhiteSpace(token) ? _identityProvider.GetToken(identity) : token }
                              };

            string bodyStr = null;
            if (body != null)
            {
                if (body is JObject)
                    bodyStr = body.ToString();
                else
                    bodyStr = JsonConvert.SerializeObject(body, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            }

            var response = _restService.Execute<T>(absoluteUri, method, bodyStr, headers, requestSettings);

            // on errors try again 1 time.
            if (response.StatusCode == 401)
            {
                if (!isRetry)
                {
                    return ExecuteRESTRequest<T>(absoluteUri, method, body, identity, true, _identityProvider.GetToken(identity));
                }
            }

            return response;
        }

        internal JsonRequestSettings BuildDefaultRequestSettings(IEnumerable<int> non200SuccessCodes = null)
        {
            var non200SuccessCodesAggregate = new List<int>{ 401, 409 };
            if(non200SuccessCodes != null)
                non200SuccessCodesAggregate.AddRange(non200SuccessCodes);

            return new JsonRequestSettings { RetryCount = 2, RetryDelayInMS = 200, Non200SuccessCodes = non200SuccessCodesAggregate};
        }

        protected virtual string GetServiceEndpoint(string serviceName, CloudIdentity identity)
        {
            var userAccess = _identityProvider.Authenticate(identity);

            if (userAccess == null || userAccess.ServiceCatalog == null)
                throw new UserAuthenticationException("Unable to authenticate user and retrieve authorized service endpoints");

            var serviceDetails = userAccess.ServiceCatalog.FirstOrDefault(sc => sc.Name == serviceName);

            if (serviceDetails == null || serviceDetails.Endpoints == null || serviceDetails.Endpoints.Length == 0)
                throw new UserAuthorizationException("The user does not have access to the requested service.");

            var endpoint = serviceDetails.Endpoints.FirstOrDefault(e => e.Region.Equals(identity.Region, StringComparison.OrdinalIgnoreCase));

            if(endpoint == null)
                throw new UserAuthorizationException("The user does not have access to the requested service or region.");

            return endpoint.PublicURL;
        }
    }

    public class UserAuthorizationException : Exception
    {
        public UserAuthorizationException(string message) : base(message){}
    }

    public class UserAuthenticationException : Exception
    {
        public UserAuthenticationException(string message) : base(message){}
    }
}
