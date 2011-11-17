using System;
using System.Collections.Generic;
using System.Linq;
using EnsureThat;
using NCore;
using NCore.Collections;
using SisoDb.Dac;
using SisoDb.Querying.Lambdas;
using SisoDb.Querying.Lambdas.Converters.Sql;
using SisoDb.Querying.Sql;
using SisoDb.Resources;
using SisoDb.Structures;

namespace SisoDb.Querying
{
    public abstract class DbQueryGenerator : IDbQueryGenerator
    {
        protected readonly ILambdaToSqlWhereConverter WhereConverter;
        protected readonly ILambdaToSqlSortingConverter SortingConverter;
        protected readonly ILambdaToSqlIncludeConverter IncludeConverter;

        protected DbQueryGenerator(
            ILambdaToSqlWhereConverter whereConverter,
            ILambdaToSqlSortingConverter sortingConverter,
            ILambdaToSqlIncludeConverter includeConverter)
        {
            Ensure.That(whereConverter, "whereConverter").IsNotNull();
            Ensure.That(sortingConverter, "sortingConverter").IsNotNull();
            Ensure.That(includeConverter, "includeConverter").IsNotNull();

            WhereConverter = whereConverter;
            SortingConverter = sortingConverter;
            IncludeConverter = includeConverter;
        }

        public SqlQuery GenerateQuery(IQueryCommand queryCommand)
        {
            Ensure.That(queryCommand, "queryCommand").IsNotNull();

            return queryCommand.HasPaging
                ? CreateSqlQueryForPaging(queryCommand)
                : CreateSqlQuery(queryCommand);
        }

        public SqlQuery GenerateQueryReturningStrutureIds(IQueryCommand queryCommand)
        {
            Ensure.That(queryCommand, "queryCommand").IsNotNull();

            if (!queryCommand.HasWhere || (queryCommand.HasIncludes || queryCommand.HasSortings || queryCommand.HasPaging))
                throw new ArgumentException(ExceptionMessages.DbQueryGenerator_GenerateWhere);

            return CreateSqlQueryReturningStructureIds(queryCommand);
        }

        protected virtual SqlQuery CreateSqlQueryReturningStructureIds(IQueryCommand queryCommand)
        {
            var where = WhereConverter.Convert(queryCommand.StructureSchema, queryCommand.Where);

            var sql = string.Format(
                "select {0}s.StructureId from [{1}] s{2} inner join [{3}] si on si.[StructureId] = s.[StructureId]{4}{5} group by s.StructureId",
                GenerateTakeString(queryCommand).AppendWith(" "),
                queryCommand.StructureSchema.GetStructureTableName(),
                GenerateWhereJoinsString(queryCommand, where).PrependWith(" "),
                queryCommand.StructureSchema.GetIndexesTableName(),
                GenerateMemberPathsStringForJoin(where).PrependWith(" and si.[MemberPath] in(").AppendWith(")"),
                where.CriteriaString.PrependWith(" where "));

            return new SqlQuery(sql, where.Parameters.ToArray());
        }

        protected virtual SqlQuery CreateSqlQuery(IQueryCommand queryCommand)
        {
            var includes = GenerateIncludes(queryCommand);
            var where = WhereConverter.Convert(queryCommand.StructureSchema, queryCommand.Where);
            var sortings = SortingConverter.Convert(queryCommand.StructureSchema, queryCommand.Sortings);
            
            var sql = string.Format(
                "select {0}s.Json{1} from [{2}] s inner join [{5}] si on si.[StructureId] = s.[StructureId]{6}{3}{4}{7}{8}{9};",
                GenerateTakeString(queryCommand).AppendWith(" "),
                SqlInclude.ToJsonOutputDefinitionString(includes).PrependWith(", "),
                queryCommand.StructureSchema.GetStructureTableName(),
                GenerateWhereJoinsString(queryCommand, where).PrependWith(" "),
                SqlInclude.ToJoinString(includes).PrependWith(" "),
                queryCommand.StructureSchema.GetIndexesTableName(),
                GenerateMemberPathsStringForJoin(where, sortings).PrependWith(" and si.[MemberPath] in(").AppendWith(")"),
                where.CriteriaString.PrependWith(" where "),
                GenerateGroupingMembersString(queryCommand).PrependWith(" group by "),
                SqlSorting.ToSortingString(sortings, "min({0})").PrependWith(" order by "));

            return new SqlQuery(sql, where.Parameters.ToArray());
        }

        protected virtual SqlQuery CreateSqlQueryForPaging(IQueryCommand queryCommand)
        {
            var includes = GenerateIncludes(queryCommand);
            var where = WhereConverter.Convert(queryCommand.StructureSchema, queryCommand.Where);
            var sortings = SortingConverter.Convert(queryCommand.StructureSchema, queryCommand.Sortings);

            var innerSelect = string.Format(
                "select s.Json{0}, row_number() over (order by {6}) as RowNum from [{1}] s inner join [{4}] si on si.[StructureId] = s.[StructureId]{5}{2}{3}{7}{8}",
                SqlInclude.ToJsonOutputDefinitionString(includes).PrependWith(", "),
                queryCommand.StructureSchema.GetStructureTableName(),
                GenerateWhereJoinsString(queryCommand, where).PrependWith(" "),
                SqlInclude.ToJoinString(includes).PrependWith(" "),
                queryCommand.StructureSchema.GetIndexesTableName(),
                GenerateMemberPathsStringForJoin(where, sortings).PrependWith(" and si.[MemberPath] in(").AppendWith(")"),
                queryCommand.HasSortings ? SqlSorting.ToSortingString(sortings, "min({0})") : "s.[StructureId]",
                where.CriteriaString.PrependWith(" where "),
                GenerateGroupingMembersString(queryCommand).PrependWith(" group by "));

            var sql = string.Format("with pagedRs as ({0}) select {1}pagedRs.Json{2} from pagedRs where pagedRs.RowNum between @pagingFrom and @pagingTo;",
                innerSelect,
                GenerateTakeString(queryCommand).AppendWith(" "),
                SqlInclude.ToJsonOutputDefinitionString(includes).PrependWith(", "));

            var takeFromRowNum = (queryCommand.Paging.PageIndex * queryCommand.Paging.PageSize) + 1;
            var takeToRowNum = (takeFromRowNum + queryCommand.Paging.PageSize) - 1;
            var queryParams = new List<IDacParameter>(where.Parameters)
            {
                new DacParameter("@pagingFrom", takeFromRowNum),
                new DacParameter("@pagingTo", takeToRowNum)
            };

            return new SqlQuery(sql, queryParams);
        }

        protected virtual string GenerateGroupingMembersString(IQueryCommand queryCommand)
        {
            var shouldHaveGrouping = queryCommand.HasIncludes || queryCommand.HasPaging || queryCommand.HasSortings || queryCommand.HasWhere;

            if (!shouldHaveGrouping)
                return string.Empty;

            return "s.[StructureId], s.Json";
            //return string.Format("s.[StructureId], s.Json{0}",
            //    SqlInclude.ToColumnDefinitionString(includes).PrependWith(", "));
        }

        protected virtual string GenerateTakeString(IQueryCommand queryCommand)
        {
            if (!queryCommand.HasTakeNumOfStructures)
                return string.Empty;

            return string.Format("top({0})", queryCommand.TakeNumOfStructures);
        }

        protected virtual string GenerateMemberPathsStringForJoin(SqlWhere where, IList<SqlSorting> sortings = null)
        {
            if (where == null && (sortings == null || sortings.Count == 0))
                return string.Empty;

            IEnumerable<string> memberPaths = new string[]{};
            
            if(where != null)
                memberPaths = memberPaths.MergeDistinctWith(where.MemberPaths);
            
            if(sortings != null)
                memberPaths = memberPaths.MergeDistinctWith(sortings.Select(s => s.MemberPath));

            return memberPaths.ToJoinedString(", ", "'{0}'");
        }

        protected virtual string GenerateWhereJoinsString(IQueryCommand queryCommand, SqlWhere where)
        {
            var indexesTableName = queryCommand.StructureSchema.GetIndexesTableName();

            var joins = new List<string>(where.MemberPaths.Length);

            foreach (var memberPath in where.MemberPaths)
            {
                joins.Add(string.Format("left join [{0}] as mem{1} on mem{1}.[StructureId]=s.[StructureId] and mem{1}.[MemberPath]='{2}'", 
                    indexesTableName,
                    joins.Count,
                    memberPath));
            }

            return string.Join(" ", joins);
        }

        protected virtual IList<SqlInclude> GenerateIncludes(IQueryCommand queryCommand)
        {
            if (!queryCommand.HasIncludes)
                return new List<SqlInclude>();

            IParsedLambda mergedIncludes = null;
            foreach (var include in queryCommand.Includes)
            {
                if (mergedIncludes == null)
                    mergedIncludes = include;
                else
                    mergedIncludes = mergedIncludes.MergeAsNew(include);
            }

            return IncludeConverter.Convert(queryCommand.StructureSchema, mergedIncludes);
        }
    }
}