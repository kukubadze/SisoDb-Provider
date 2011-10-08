﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using EnsureThat;
using NCore;
using PineCone.Structures.Schemas;
using SisoDb.Dac;
using SisoDb.DbSchema;
using SisoDb.Providers;
using SisoDb.Querying;
using SisoDb.Querying.Sql;
using SisoDb.Serialization;

namespace SisoDb.Sql2008
{
    public class Sql2008QueryEngine : IQueryEngine
    {
        protected readonly ISisoProviderFactory ProviderFactory;
        protected readonly IDbClient DbClient;
        protected readonly IDbSchemaManager DbSchemaManager;
        protected readonly IDbSchemaUpserter DbSchemaUpserter;
        protected readonly IStructureSchemas StructureSchemas;
        protected readonly IDbQueryGenerator QueryGenerator;
        protected readonly IJsonSerializer JsonSerializer;
        protected readonly ICommandBuilderFactory CommandBuilderFactory;

        protected internal Sql2008QueryEngine(
            ISisoProviderFactory providerFactory,
            IDbClient dbClient,
            IDbSchemaManager dbSchemaManager, 
            IDbSchemaUpserter dbSchemaUpserter,
            IStructureSchemas structureSchemas,
            IJsonSerializer jsonSerializer,
            IDbQueryGenerator queryGenerator,
            ICommandBuilderFactory commandBuilderFactory)
        {
            Ensure.That(() => providerFactory).IsNotNull();
            Ensure.That(() => dbClient).IsNotNull();
            Ensure.That(() => dbSchemaManager).IsNotNull();
            Ensure.That(() => dbSchemaUpserter).IsNotNull();
            Ensure.That(() => structureSchemas).IsNotNull();
            Ensure.That(() => jsonSerializer).IsNotNull();
            Ensure.That(() => queryGenerator).IsNotNull();
            Ensure.That(() => commandBuilderFactory).IsNotNull();

            ProviderFactory = providerFactory;
            DbClient = dbClient;
            DbSchemaManager = dbSchemaManager;
            DbSchemaUpserter = dbSchemaUpserter;
            StructureSchemas = structureSchemas;
            JsonSerializer = jsonSerializer;
            QueryGenerator = queryGenerator;
            CommandBuilderFactory = commandBuilderFactory;
        }

        public void Dispose()
        {
            DbClient.Dispose();
        }

        public int Count<T>() where T : class
        {
            var structureSchema = StructureSchemas.GetSchema(TypeFor<T>.Type);

            UpsertStructureSet(structureSchema);

            return DbClient.RowCount(structureSchema);
        }

        public int Count<T>(Expression<Func<T, bool>> expression) where T : class
        {
            var structureSchema = StructureSchemas.GetSchema(TypeFor<T>.Type);

            UpsertStructureSet(structureSchema);

            var commandBuilder = CommandBuilderFactory.CreateQueryCommandBuilder<T>();
            var queryCommand = commandBuilder.Where(expression).Command;
            var whereSql = QueryGenerator.GenerateWhere(queryCommand);

            return DbClient.RowCountByQuery(structureSchema, whereSql);
        }

        public T GetById<T>(ValueType id) where T : class
        {
            return JsonSerializer.Deserialize<T>(
                GetByIdAsJson<T>(id));
        }
        
        public IEnumerable<T> GetByIds<T>(IEnumerable<ValueType> ids) where T : class
        {
            return JsonSerializer.Deserialize<T>(
                GetByIdsAsJson<T>(ids));
        }

        public IEnumerable<T> GetByIdInterval<T>(ValueType idFrom, ValueType idTo) where T : class
        {
            var structureSchema = StructureSchemas.GetSchema(TypeFor<T>.Type);

            UpsertStructureSet(structureSchema);

            return JsonSerializer.Deserialize<T>(
                DbClient.GetJsonWhereIdIsBetween(idFrom, idTo, structureSchema));
        }

        public TOut GetByIdAs<TContract, TOut>(ValueType id)
            where TContract : class
            where TOut : class
        {
            return JsonSerializer.Deserialize<TOut>(
                GetByIdAsJson<TContract>(id));
        }

        public IEnumerable<TOut> GetByIdsAs<TContract, TOut>(IEnumerable<ValueType> ids)
            where TContract : class
            where TOut : class
        {
            return JsonSerializer.Deserialize<TOut>(
                GetByIdsAsJson<TContract>(ids.Select(i => i)));
        }

        public string GetByIdAsJson<T>(ValueType id) where T : class
        {
            var structureSchema = StructureSchemas.GetSchema(TypeFor<T>.Type);

            UpsertStructureSet(structureSchema);

            return DbClient.GetJsonById(id, structureSchema);
        }

        public IEnumerable<string> GetByIdsAsJson<T>(IEnumerable<ValueType> ids) where T : class
        {
            var structureSchema = StructureSchemas.GetSchema(TypeFor<T>.Type);

            UpsertStructureSet(structureSchema);

            return DbClient.GetJsonByIds(ids, structureSchema.IdAccessor.IdType, structureSchema);
        }

        public IEnumerable<T> GetAll<T>() where T : class
        {
            var command = new GetCommand();

            return JsonSerializer.Deserialize<T>(
                GetAllAsJson<T>(command));
        }

        public IEnumerable<T> GetAll<T>(Action<IGetCommandBuilder<T>> commandInitializer) where T : class
        {
            var commandBuilder = CommandBuilderFactory.CreateGetCommandBuilder<T>();
            commandInitializer(commandBuilder);

            return JsonSerializer.Deserialize<T>(
                GetAllAsJson<T>(commandBuilder.Command));
        }

        public IEnumerable<TOut> GetAllAs<TContract, TOut>()
            where TContract : class
            where TOut : class
        {
            return JsonSerializer.Deserialize<TOut>(
                GetAllAsJson<TContract>());
        }

        public IEnumerable<TOut> GetAllAs<TContract, TOut>(Action<IGetCommandBuilder<TContract>> commandInitializer)
            where TContract : class
            where TOut : class
        {
            var commandBuilder = CommandBuilderFactory.CreateGetCommandBuilder<TContract>();
            commandInitializer(commandBuilder);

            return JsonSerializer.Deserialize<TOut>(
                GetAllAsJson<TContract>(commandBuilder.Command));
        }

        public IEnumerable<string> GetAllAsJson<T>() where T : class
        {
            var commandBuilder = CommandBuilderFactory.CreateGetCommandBuilder<T>();

            return GetAllAsJson<T>(commandBuilder.Command);
        }

        public IEnumerable<string> GetAllAsJson<T>(Action<IGetCommandBuilder<T>> commandInitializer) where T : class
        {
            var commandBuilder = CommandBuilderFactory.CreateGetCommandBuilder<T>();
            commandInitializer(commandBuilder);

            return GetAllAsJson<T>(commandBuilder.Command);
        }

        private IEnumerable<string> GetAllAsJson<T>(IGetCommand getCommand) where T : class
        {
            Ensure.That(() => getCommand).IsNotNull();

            var structureSchema = StructureSchemas.GetSchema(TypeFor<T>.Type);

            UpsertStructureSet(structureSchema);

            string sql;
            if (getCommand.HasSortings || getCommand.HasIncludes)
            {
                var queryCommand = new QueryCommand(getCommand.Includes) { Sortings = getCommand.Sortings };
                var query = QueryGenerator.Generate(queryCommand, structureSchema);
                sql = query.Sql;
            }
            else
                sql = DbClient.SqlStatements.GetSql("GetAll").Inject(structureSchema.GetStructureTableName());

            using (var cmd = DbClient.CreateCommand(CommandType.Text, sql))
            {
                return ConsumeReader(cmd);
            }
        }

        public IEnumerable<T> NamedQuery<T>(INamedQuery query) where T : class
        {
            return JsonSerializer.Deserialize<T>(
                NamedQueryAsJson<T>(query));
        }

        public IEnumerable<TOut> NamedQueryAs<TContract, TOut>(INamedQuery query)
            where TContract : class
            where TOut : class
        {
            return JsonSerializer.Deserialize<TOut>(
                NamedQueryAsJson<TContract>(query));
        }

        public IEnumerable<string> NamedQueryAsJson<T>(INamedQuery query) where T : class
        {
            var structureSchema = StructureSchemas.GetSchema(TypeFor<T>.Type);
            
            UpsertStructureSet(structureSchema);

            using (var cmd = DbClient.CreateCommand(CommandType.StoredProcedure, query.Name, query.Parameters.ToArray()))
            {
                return ConsumeReader(cmd);
            }
        }

        public IEnumerable<T> Where<T>(Expression<Func<T, bool>> expression)
            where T : class
        {
            return Query<T>(q => q.Where(expression));
        }

        public IEnumerable<TOut> WhereAs<TContract, TOut>(Expression<Func<TContract, bool>> expression)
            where TContract : class
            where TOut : class
        {
            return QueryAs<TContract, TOut>(q => q.Where(expression));
        }

        public IEnumerable<string> WhereAsJson<T>(Expression<Func<T, bool>> expression)
            where T : class
        {
            return QueryAsJson<T>(q => q.Where(expression));
        }

        public IEnumerable<T> Query<T>(Action<IQueryCommandBuilder<T>> commandInitializer)
            where T : class
        {
            var commandBuilder = CommandBuilderFactory.CreateQueryCommandBuilder<T>();
            commandInitializer(commandBuilder);

            return JsonSerializer.Deserialize<T>(
                QueryAsJson<T>(commandBuilder.Command));
        }

        public IEnumerable<TOut> QueryAs<TContract, TOut>(Action<IQueryCommandBuilder<TContract>> commandInitializer)
            where TContract : class
            where TOut : class
        {
            var commandBuilder = CommandBuilderFactory.CreateQueryCommandBuilder<TContract>();
            commandInitializer(commandBuilder);

            return JsonSerializer.Deserialize<TOut>(
                QueryAsJson<TContract>(commandBuilder.Command));
        }

        public IEnumerable<string> QueryAsJson<T>(Action<IQueryCommandBuilder<T>> commandInitializer) where T : class
        {
            var commandBuilder = CommandBuilderFactory.CreateQueryCommandBuilder<T>();
            commandInitializer(commandBuilder);

            return QueryAsJson<T>(commandBuilder.Command);
        }

        private IEnumerable<string> QueryAsJson<T>(IQueryCommand queryCommand) where T : class
        {
            Ensure.That(() => queryCommand).IsNotNull();

            var structureSchema = StructureSchemas.GetSchema(TypeFor<T>.Type);
            UpsertStructureSet(structureSchema);

            var query = QueryGenerator.Generate(queryCommand, structureSchema);
            var parameters = query.Parameters.Select(p => new DacParameter(p.Name, p.Value)).ToArray();

            using (var cmd = DbClient.CreateCommand(CommandType.Text, query.Sql, parameters))
            {
                return ConsumeReader(cmd);
            }
        }

        private static IEnumerable<string> ConsumeReader(IDbCommand cmd)
        {
            using (var reader = cmd.ExecuteReader(CommandBehavior.SingleResult | CommandBehavior.SequentialAccess))
            {
                Func<string> read;

                if(reader.FieldCount < 2)
                    read = () => reader.GetString(0);
                else 
                    read = () => GetMergedJsonStructure(reader);

                while (reader.Read())
                {
                    yield return read();
                }

                reader.Close();
            }
        }

        private static string GetMergedJsonStructure(IDataRecord dataRecord)
        {
            var sb = new StringBuilder();
            sb.Append(dataRecord.GetString(0));
            sb.Remove(sb.Length - 1, 1);
            sb.Append(",");

            foreach (var childJson in ReadChildJson(dataRecord))
                sb.Append(childJson);

            sb.Append("}");

            return sb.ToString();
        }

        private static IEnumerable<string> ReadChildJson(IDataRecord dataRecord)
        {
            var lastIndex = dataRecord.FieldCount - 1;
            for (var c = 1; c <= lastIndex; c++)
                yield return string.Format("\"{0}\":{1}{2}",
                    dataRecord.GetName(c),
                    dataRecord.GetString(c),
                    (c < lastIndex) ? "," : "");
        }

        protected void UpsertStructureSet(IStructureSchema structureSchema)
        {
            DbSchemaManager.UpsertStructureSet(structureSchema, DbSchemaUpserter);
        }
    }
}