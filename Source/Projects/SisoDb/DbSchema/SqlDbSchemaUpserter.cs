﻿using System.Text;
using EnsureThat;
using PineCone.Structures.Schemas;
using SisoDb.Dac;
using SisoDb.Structures;

namespace SisoDb.DbSchema
{
    public class SqlDbSchemaUpserter : IDbSchemaUpserter
    {
        private readonly SqlDbStructuresSchemaBuilder _structuresDbSchemaBuilder;
        private readonly SqlDbIndexesSchemaBuilder _indexesDbSchemaBuilder;
        private readonly SqlDbUniquesSchemaBuilder _uniquesDbSchemaBuilder;

        private readonly SqlDbIndexesSchemaSynchronizer _indexesDbSchemaSynchronizer;
        private readonly SqlDbUniquesSchemaSynchronizer _uniquesDbSchemaSynchronizer;
        private readonly IDbClient _dbClient;

        public SqlDbSchemaUpserter(IDbClient dbClient)
        {
            Ensure.That(dbClient, "dbClient").IsNotNull();

            _dbClient = dbClient;

            _structuresDbSchemaBuilder = new SqlDbStructuresSchemaBuilder(_dbClient.SqlStatements);
            _indexesDbSchemaBuilder = new SqlDbIndexesSchemaBuilder(_dbClient.SqlStatements);
            _uniquesDbSchemaBuilder = new SqlDbUniquesSchemaBuilder(_dbClient.SqlStatements);

            _indexesDbSchemaSynchronizer = new SqlDbIndexesSchemaSynchronizer(_dbClient);
            _uniquesDbSchemaSynchronizer = new SqlDbUniquesSchemaSynchronizer(_dbClient);
        }

        public void Upsert(IStructureSchema structureSchema)
        {
            var structuresTableName = structureSchema.GetStructureTableName();
            var indexesTableName = structureSchema.GetIndexesTableName();
            var uniquesTableName = structureSchema.GetUniquesTableName();

            var structuresTableExists = _dbClient.TableExists(structuresTableName);
            var indexesTableExists = _dbClient.TableExists(indexesTableName);
            var uniquesTableExists = _dbClient.TableExists(uniquesTableName);

            if(indexesTableExists)
                _indexesDbSchemaSynchronizer.Synchronize(structureSchema);

            if(uniquesTableExists)
                _uniquesDbSchemaSynchronizer.Synchronize(structureSchema);

            if (structuresTableExists && indexesTableExists && uniquesTableExists)
                return;

            _dbClient.ExecuteNonQuery(GenerateSql(structureSchema, structuresTableExists, indexesTableExists, uniquesTableExists),
                new DacParameter("entityHash", structureSchema.Hash),
                new DacParameter("entityName", structureSchema.Name));
        }

        private string GenerateSql(IStructureSchema structureSchema, bool structuresTableExists, bool indexesTableExists, bool uniquesTableExists)
        {
            var structuresSql = structuresTableExists ? "" : _structuresDbSchemaBuilder.GenerateSql(structureSchema);
            var indexesSql = indexesTableExists ? "" : _indexesDbSchemaBuilder.GenerateSql(structureSchema);
            var uniquesSql = uniquesTableExists ? "" : _uniquesDbSchemaBuilder.GenerateSql(structureSchema);

            var sql = new StringBuilder();

            if (!structuresTableExists)
                sql.AppendLine(structuresSql);

            if (!indexesTableExists)
                sql.AppendLine(indexesSql);

            if (!uniquesTableExists)
                sql.AppendLine(uniquesSql);

            return sql.ToString();
        }
    }
}