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

        #endregion

        #region Mapped

        public MappedCommand<T> CompileMapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings)
        {
            return new MappedCommand<T>(contextProvider, commandText, paramNames, settings);
        }

        public MappedCommand<T> CompileMapped<T>(string commandText, IList<FieldSettings<T>> settings)
        {
            return new MappedCommand<T>(contextProvider, commandText, contextProvider.ParamsFromCommandText(commandText), settings);
        }

        public MappedCommand<T> CompileMapped<T>(string commandText, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(contextProvider, commandText, paramNames, FieldSettings.FromType<T>(fromTypeOption));
        }

        public MappedCommand<T> CompileMapped<T>(string commandText, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(contextProvider, commandText, contextProvider.ParamsFromCommandText(commandText), FieldSettings.FromType<T>(fromTypeOption));
        }

        public MappedCommand<T> CompileMapped<T>(T proto, string commandText, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(contextProvider, commandText, paramNames, FieldSettings.FromType(proto, fromTypeOption));
        }

        public MappedCommand<T> CompileMapped<T>(T proto, string commandText, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(contextProvider, commandText, contextProvider.ParamsFromCommandText(commandText), FieldSettings.FromType(proto, fromTypeOption));
        }

        #region Stored procedures

        public MappedCommand<T> CompileProcedure<T>(string name, IList<string> paramNames, IList<FieldSettings<T>> settings)
        {
            return new MappedCommand<T>(contextProvider, name, paramNames, settings, CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(string name, IList<FieldSettings<T>> settings)
        {
            return new MappedCommand<T>(contextProvider, name, contextProvider.ParamsFromSettings(settings), settings, CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(string name, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(contextProvider, name, paramNames, FieldSettings.FromType<T>(fromTypeOption), CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(string name, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            var settings = FieldSettings.FromType<T>(fromTypeOption);
            return new MappedCommand<T>(contextProvider, name, contextProvider.ParamsFromSettings(settings), settings, CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(T proto, string name, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(contextProvider, name, paramNames, FieldSettings.FromType(proto, fromTypeOption), CommandType.StoredProcedure);
        }

        public MappedCommand<T> CompileProcedure<T>(T proto, string name, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            var settings = FieldSettings.FromType(proto, fromTypeOption);
            return new MappedCommand<T>(contextProvider, name, contextProvider.ParamsFromSettings(settings), settings, CommandType.StoredProcedure);
        }

        #endregion

        #region Special commands

        #region Insert

        public MappedCommand<T> CompileInsert<T>(string tableName, IList<FieldSettings<T>> settings, bool getIdentity, params string[] ignoreFields)
        {
            var fields = settings.Select(x => x.Name.ToLowerInvariant()).Except(ignoreFields.Select(x => x.ToLowerInvariant())).ToArray();
            string query = contextProvider.CommandTextGenerator.Insert(tableName, getIdentity, fields);
            return new MappedCommand<T>(contextProvider, query, fields, settings);
        }

        public MappedCommand<T> CompileInsert<T>(string tableName, FromTypeOption fromTypeOption, bool getIdentity, params string[] ignoreFields)
        {
            return CompileInsert(tableName, FieldSettings.FromType<T>(fromTypeOption), getIdentity, ignoreFields);
        }

        public MappedCommand<T> CompileInsert<T>(string tableName, bool getIdentity, params string[] ignoreFields)
        {
            return CompileInsert(tableName, FieldSettings.FromType<T>(FromTypeOption.Default), getIdentity, ignoreFields);
        }

        public MappedCommand<T> CompileInsert<T>(T proto, string tableName, FromTypeOption fromTypeOption, bool getIdentity, params string[] ignoreFields)
        {
            return CompileInsert(tableName, FieldSettings.FromType(proto, fromTypeOption), getIdentity, ignoreFields);
        }

        public MappedCommand<T> CompileInsert<T>(T proto, string tableName, bool getIdentity, params string[] ignoreFields)
        {
            return CompileInsert(tableName, FieldSettings.FromType(proto, FromTypeOption.Default), getIdentity, ignoreFields);
        }

        #endregion

        #region Update

        public MappedCommand<T> CompileUpdate<T>(string tableName, IList<FieldSettings<T>> settings, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            var fields = settings.Select(x => x.Name.ToLowerInvariant()).Except(notKeyIgnoreFields.Select(x => x.ToLowerInvariant())).ToArray();
            var fieldsToUpdate = fields.Except(keyFields.Select(x => x.ToLowerInvariant())).ToArray();
            string query = contextProvider.CommandTextGenerator.Update(tableName, keyFields, fieldsToUpdate);
            return new MappedCommand<T>(contextProvider, query, fields, settings);
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
            return new MappedCommand<T>(contextProvider, query, keys, settings);
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
            var fields = settings.Select(x => x.Name.ToLowerInvariant()).Except(notKeyIgnoreFields.Select(x => x.ToLowerInvariant())).ToArray();
            var fieldsToUpdate = fields.Except(keyFields.Select(x => x.ToLowerInvariant())).ToArray();
            string query = contextProvider.CommandTextGenerator.Merge(tableName, keyFields, fieldsToUpdate);
            return new MappedCommand<T>(contextProvider, query, fields, settings);
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
