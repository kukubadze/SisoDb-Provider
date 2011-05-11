﻿using SisoDb.Providers.AzureProvider.DbSchema;
using SisoDb.Providers.DbSchema;

namespace SisoDb.Providers
{
    public class SqlAzureProviderFactory : ISisoProviderFactory
    {
        public IDbColumnGenerator GetDbColumnGenerator()
        {
            return new AzureDbColumnGenerator();
        }
    }
}