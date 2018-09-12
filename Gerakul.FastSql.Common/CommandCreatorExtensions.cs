using System.Collections.Generic;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public static partial class CommandCreatorExtensions
    {
        public static IWrappedCommand CreateSimple(this ICommandCreator creator, string commandText, params object[] parameters)
        {
            return creator.CreateSimple(null, commandText, parameters);
        }

        public static IWrappedCommand CreateSimple(this ICommandCreator creator, QueryOptions queryOptions, string commandText, params object[] parameters)
        {
            return creator.Set(x => x.CommandCompilator.CompileSimple(commandText).Create(x, queryOptions, parameters));
        }

        public static IWrappedCommand CreateSimple(this ICommandCreator creator, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return creator.CreateSimple(null, precompiledCommand, parameters);
        }

        public static IWrappedCommand CreateSimple(this ICommandCreator creator, QueryOptions queryOptions, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return creator.Set(x => precompiledCommand.Create(x, queryOptions, parameters));
        }

        public static IWrappedCommand CreateProcedureSimple(this ICommandCreator creator, string name, params DbParameter[] parameters)
        {
            return creator.CreateProcedureSimple(null, name, parameters);
        }

        public static IWrappedCommand CreateProcedureSimple(this ICommandCreator creator, QueryOptions queryOptions, string name, params DbParameter[] parameters)
        {
            return creator.Set(x => x.CommandCompilator.CompileProcedureSimple(name).Create(x, queryOptions, parameters));
        }


        public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, string commandText, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return creator.Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileMapped<T>(commandText, fromTypeOption, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, MappedCommand<T> precompiledCommand, T value, QueryOptions queryOptions = null)
        {
            return creator.Set(x => precompiledCommand.Create(x, value, queryOptions));
        }

        public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, string commandText, IList<string> paramNames, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return creator.Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileMapped<T>(commandText, paramNames, fromTypeOption, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return creator.Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileMapped(commandText, settings, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return creator.Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileMapped(commandText, paramNames, settings, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public static IWrappedCommand CreateProcedure<T>(this ICommandCreator creator, string name, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return creator.Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileProcedure<T>(name, fromTypeOption, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public static IWrappedCommand CreateProcedure<T>(this ICommandCreator creator, string name, IList<string> paramNames, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return creator.Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileProcedure<T>(name, paramNames, fromTypeOption, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public static IWrappedCommand CreateProcedure<T>(this ICommandCreator creator, string name, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return creator.Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileProcedure(name, settings, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public static IWrappedCommand CreateProcedure<T>(this ICommandCreator creator, string name, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return creator.Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileProcedure(name, paramNames, settings, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public static IWrappedCommand CreateInsert<T>(this ICommandCreator creator, string tableName, T value, params string[] ignoreFields)
        {
            return creator.CreateInsert(tableName, value, null, ignoreFields);
        }

        public static IWrappedCommand CreateInsert<T>(this ICommandCreator creator, string tableName, T value, QueryOptions queryOptions, params string[] ignoreFields)
        {
            return creator.Set(x => x.CommandCompilator.CompileInsert<T>(tableName, ignoreFields).Create(x, value, queryOptions));
        }

        public static IWrappedCommand CreateInsertWithOutput<T>(this ICommandCreator creator, string tableName, T value, params string[] outputFields)
        {
            return creator.CreateInsertWithOutput(tableName, value, null, null, outputFields);
        }

        public static IWrappedCommand CreateInsertWithOutput<T>(this ICommandCreator creator, string tableName, T value, QueryOptions queryOptions, params string[] outputFields)
        {
            return creator.CreateInsertWithOutput(tableName, value, queryOptions, null, outputFields);
        }

        public static IWrappedCommand CreateInsertWithOutput<T>(this ICommandCreator creator, string tableName, T value, IList<string> ignoreFields, params string[] outputFields)
        {
            return creator.CreateInsertWithOutput(tableName, value, null, ignoreFields, outputFields);
        }

        public static IWrappedCommand CreateInsertWithOutput<T>(this ICommandCreator creator, string tableName, T value, QueryOptions queryOptions, IList<string> ignoreFields, params string[] outputFields)
        {
            return creator.Set(x => x.CommandCompilator.CompileInsertWithOutput<T>(tableName, FieldSettings.FromType<T>(FromTypeOption.Default), ignoreFields, outputFields).Create(x, value, queryOptions));
        }

        public static IWrappedCommand CreateUpdate<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
        {
            return creator.CreateUpdate(tableName, value, null, keyFields);
        }

        public static IWrappedCommand CreateUpdate<T>(this ICommandCreator creator, string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return creator.Set(x => x.CommandCompilator.CompileUpdate<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        public static IWrappedCommand CreateDelete<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
        {
            return creator.CreateDelete(tableName, value, null, keyFields);
        }

        public static IWrappedCommand CreateDelete<T>(this ICommandCreator creator, string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return creator.Set(x => x.CommandCompilator.CompileDelete<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        public static IWrappedCommand CreateMerge<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
        {
            return creator.CreateMerge(tableName, value, null, keyFields);
        }

        public static IWrappedCommand CreateMerge<T>(this ICommandCreator creator, string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return creator.Set(x => x.CommandCompilator.CompileMerge<T>(tableName, keyFields).Create(x, value, queryOptions));
        }
    }
}
