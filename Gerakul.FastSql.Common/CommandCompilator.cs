using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Gerakul.FastSql.Common
{
    public class CommandCompilator
    {
        private ContextProvider contextProvider;

        public CommandCompilator(ContextProvider contextProvider)
        {
            this.contextProvider = contextProvider;
        }

        #region Simple

        public SimpleCommand CompileSimple(string commandText)
        {
            return new SimpleCommand(contextProvider, commandText);
        }

        public SimpleCommand CompileProcedureSimple(string name)
        {
            return new SimpleCommand(contextProvider, name, CommandType.StoredProcedure);
        }

        #endregion

        #region Mapped

        public MappedCommand<T> CompileMapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings,
            bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, commandText, paramNames, settings, caseSensitiveParamsMatching);
        }

        public MappedCommand<T> CompileMapped<T>(string commandText, IList<FieldSettings<T>> settings, bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, commandText, contextProvider.ParamsFromCommandText(commandText), settings, caseSensitiveParamsMatching);
        }

        public MappedCommand<T> CompileMapped<T>(string commandText, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default,
            bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, commandText, paramNames, FieldSettings.FromType<T>(fromTypeOption), caseSensitiveParamsMatching);
        }

        public MappedCommand<T> CompileMapped<T>(string commandText, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, commandText, contextProvider.ParamsFromCommandText(commandText), FieldSettings.FromType<T>(fromTypeOption),
                caseSensitiveParamsMatching);
        }

        public MappedCommand<T> CompileMapped<T>(T proto, string commandText, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default, 
            bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, commandText, paramNames, FieldSettings.FromType(proto, fromTypeOption), caseSensitiveParamsMatching);
        }

        public MappedCommand<T> CompileMapped<T>(T proto, string commandText, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, commandText, contextProvider.ParamsFromCommandText(commandText), FieldSettings.FromType(proto, fromTypeOption), 
                caseSensitiveParamsMatching);
        }

        #region Stored procedures

        public MappedCommand<T> CompileProcedure<T>(string name, IList<string> paramNames, IList<FieldSettings<T>> settings, bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, name, paramNames, settings, caseSensitiveParamsMatching, CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(string name, IList<FieldSettings<T>> settings, bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, name, contextProvider.ParamsFromSettings(settings), settings, caseSensitiveParamsMatching, CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(string name, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default, 
            bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, name, paramNames, FieldSettings.FromType<T>(fromTypeOption), caseSensitiveParamsMatching, CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(string name, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
        {
            var settings = FieldSettings.FromType<T>(fromTypeOption);
            return new MappedCommand<T>(contextProvider, name, contextProvider.ParamsFromSettings(settings), settings, caseSensitiveParamsMatching, CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(T proto, string name, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
        {
            return new MappedCommand<T>(contextProvider, name, paramNames, FieldSettings.FromType(proto, fromTypeOption), caseSensitiveParamsMatching, CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(T proto, string name, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
        {
            var settings = FieldSettings.FromType(proto, fromTypeOption);
            return new MappedCommand<T>(contextProvider, name, contextProvider.ParamsFromSettings(settings), settings, caseSensitiveParamsMatching, CommandType.StoredProcedure);
        }

        #endregion

        #region Special commands

        #region Insert

        public MappedCommand<T> CompileInsert<T>(string tableName, IList<FieldSettings<T>> settings, params string[] ignoreFields)
        {
            var fields = settings.Select(x => x.Name).Except(ignoreFields.Select(x => x)).ToArray();
            string query = contextProvider.CommandTextGenerator.Insert(tableName, fields);
            return new MappedCommand<T>(contextProvider, query, fields, settings, true);
        }

        public MappedCommand<T> CompileInsert<T>(string tableName, FromTypeOption fromTypeOption, params string[] ignoreFields)
        {
            return CompileInsert(tableName, FieldSettings.FromType<T>(fromTypeOption), ignoreFields);
        }

        public MappedCommand<T> CompileInsert<T>(string tableName, params string[] ignoreFields)
        {
            return CompileInsert(tableName, FieldSettings.FromType<T>(FromTypeOption.Default), ignoreFields);
        }

        public MappedCommand<T> CompileInsert<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] ignoreFields)
        {
            return CompileInsert(tableName, FieldSettings.FromType(proto, fromTypeOption), ignoreFields);
        }

        public MappedCommand<T> CompileInsert<T>(T proto, string tableName, params string[] ignoreFields)
        {
            return CompileInsert(tableName, FieldSettings.FromType(proto, FromTypeOption.Default), ignoreFields);
        }

        public MappedCommand<T> CompileInsertWithOutput<T>(string tableName, IList<FieldSettings<T>> settings,
            IList<string> ignoreFields, params string[] outputFields)
        {
            var ignFields = ignoreFields?.ToArray() ?? new string[0];

            if (outputFields.Length == 0)
            {
                return CompileInsert(tableName, settings, ignFields);
            }

            var fields = settings.Select(x => x.Name).Except(ignFields.Select(x => x)).ToArray();
            string query = contextProvider.CommandTextGenerator.InsertWithOutput(tableName, fields, outputFields);
            return new MappedCommand<T>(contextProvider, query, fields, settings, true);
        }

        #endregion

        #region Update

        public MappedCommand<T> CompileUpdate<T>(string tableName, IList<FieldSettings<T>> settings, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            var fields = settings.Select(x => x.Name).Except(notKeyIgnoreFields.Select(x => x)).ToArray();
            var fieldsToUpdate = fields.Except(keyFields.Select(x => x)).ToArray();
            string query = contextProvider.CommandTextGenerator.Update(tableName, keyFields, fieldsToUpdate);
            return new MappedCommand<T>(contextProvider, query, fields, settings, true);
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
        {
            return CompileUpdate(tableName, settings, keyFields, new List<string>());
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, new List<string>());
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType<T>(), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, params string[] keyFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType<T>(), keyFields, new List<string>());
        }

        public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, new List<string>());
        }

        public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType(proto), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, params string[] keyFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType(proto), keyFields, new List<string>());
        }

        #endregion

        #region Delete

        public MappedCommand<T> CompileDelete<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
        {
            var keys = keyFields == null || keyFields.Length == 0 ? settings.GetNames() : keyFields;
            string query = contextProvider.CommandTextGenerator.Delete(tableName, keys);
            return new MappedCommand<T>(contextProvider, query, keys, settings, true);
        }

        public MappedCommand<T> CompileDelete<T>(string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileDelete(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields);
        }

        public MappedCommand<T> CompileDelete<T>(string tableName, params string[] keyFields)
        {
            return CompileDelete(tableName, FieldSettings.FromType<T>(), keyFields);
        }

        public MappedCommand<T> CompileDelete<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileDelete(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields);
        }

        public MappedCommand<T> CompileDelete<T>(T proto, string tableName, params string[] keyFields)
        {
            return CompileDelete(tableName, FieldSettings.FromType(proto), keyFields);
        }

        #endregion

        #region Merge

        public MappedCommand<T> CompileMerge<T>(string tableName, IList<FieldSettings<T>> settings, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            var fields = settings.Select(x => x.Name).Except(notKeyIgnoreFields.Select(x => x)).ToArray();
            var fieldsToUpdate = fields.Except(keyFields.Select(x => x)).ToArray();
            string query = contextProvider.CommandTextGenerator.Merge(tableName, keyFields, fieldsToUpdate);
            return new MappedCommand<T>(contextProvider, query, fields, settings, true);
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
        {
            return CompileMerge(tableName, settings, keyFields, new List<string>());
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, new List<string>());
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType<T>(), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, params string[] keyFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType<T>(), keyFields, new List<string>());
        }

        public MappedCommand<T> CompileMerge<T>(T proto, string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileMerge<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, new List<string>());
        }

        public MappedCommand<T> CompileMerge<T>(T proto, string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType(proto), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileMerge<T>(T proto, string tableName, params string[] keyFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType(proto), keyFields, new List<string>());
        }

        #endregion

        #endregion

        #endregion
    }
}
