﻿using System;
using SimpleRestServices.Client;
using SimpleRestServices.Client.Json;
using net.openstack.Core;
using net.openstack.Core.Domain;
using net.openstack.Providers.Rackspace.Exceptions;

namespace net.openstack.Providers.Rackspace
{
    internal class IdentityProviderFactory : IProviderFactory<IIdentityProvider>
    {
        private const string USIdentityUrlBase = "https://identity.api.rackspacecloud.com";
        private const string LONIdentityUrlBase = "https://lon.identity.api.rackspacecloud.com";

        private readonly ICache<UserAccess> _tokenCache;
        private readonly IRestService _restService;

        public IdentityProviderFactory(IRestService restService = null, ICache<UserAccess> tokenCache = null)
        {
            if (restService == null)
                restService = new JsonRestServices();

            if (tokenCache == null)
                tokenCache = UserAccessCache.Instance;

            _restService = restService;
            _tokenCache = tokenCache;
        }

        public IIdentityProvider Get(string geo)
        {
            switch (geo.ToLower())
            {
                case "dfw":
                case "ord":
                case "us":
                    return new GeographicalIdentityProvider(new Uri(USIdentityUrlBase), _restService, _tokenCache);
                case "lon":
                    return new GeographicalIdentityProvider(new Uri(LONIdentityUrlBase), _restService, _tokenCache);
                default:
                    throw new UnknownGeographyException(geo);
            }
        }
    }
}
