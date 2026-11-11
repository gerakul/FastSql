using System;
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
            var fields = settings.Select(x => x.Name).Except(ToMemberNames(ignoreFields, settings)).ToArray();
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
            var ignFields = ignoreFields?.ToArray() ?? [];

            if (outputFields.Length == 0)
            {
                return CompileInsert(tableName, settings, ignFields);
            }

            var fields = settings.Select(x => x.Name).Except(ToMemberNames(ignFields, settings)).ToArray();
            string query = contextProvider.CommandTextGenerator.InsertWithOutput(tableName, fields, outputFields);
            return new MappedCommand<T>(contextProvider, query, fields, settings, true);
        }

        #endregion

        #region Update

        public MappedCommand<T> CompileUpdate<T>(string tableName, IList<FieldSettings<T>> settings, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            CheckKeyFields(keyFields);
            var keys = ToMemberNames(keyFields, settings);
            var fields = settings.Select(x => x.Name).Except(ToMemberNames(notKeyIgnoreFields, settings)).ToArray();
            var fieldsToUpdate = fields.Except(keys).ToArray();
            if (fieldsToUpdate.Length == 0)
            {
                throw new ArgumentException("Update has no fields to set: all fields are key fields or ignored", nameof(keyFields));
            }

            string query = contextProvider.CommandTextGenerator.Update(tableName, keys, fieldsToUpdate);
            return new MappedCommand<T>(contextProvider, query, fields, settings, true);
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
        {
            return CompileUpdate(tableName, settings, keyFields, Array.Empty<string>());
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, Array.Empty<string>());
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType<T>(), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileUpdate<T>(string tableName, params string[] keyFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType<T>(), keyFields, Array.Empty<string>());
        }

        public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, Array.Empty<string>());
        }

        public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType(proto), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, params string[] keyFields)
        {
            return CompileUpdate(tableName, FieldSettings.FromType(proto), keyFields, Array.Empty<string>());
        }

        #endregion

        #region Delete

        public MappedCommand<T> CompileDelete<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
        {
            var keys = keyFields == null || keyFields.Length == 0 ? settings.GetNames() : ToMemberNames(keyFields, settings);
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
            CheckKeyFields(keyFields);
            var keys = ToMemberNames(keyFields, settings);
            var fields = settings.Select(x => x.Name).Except(ToMemberNames(notKeyIgnoreFields, settings)).ToArray();
            var fieldsToUpdate = fields.Except(keys).ToArray(); // none: the text generator makes an insert if missing
            string query = contextProvider.CommandTextGenerator.Merge(tableName, keys, fieldsToUpdate);
            return new MappedCommand<T>(contextProvider, query, fields, settings, true);
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
        {
            return CompileMerge(tableName, settings, keyFields, Array.Empty<string>());
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, Array.Empty<string>());
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType<T>(), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileMerge<T>(string tableName, params string[] keyFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType<T>(), keyFields, Array.Empty<string>());
        }

        public MappedCommand<T> CompileMerge<T>(T proto, string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileMerge<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, Array.Empty<string>());
        }

        public MappedCommand<T> CompileMerge<T>(T proto, string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType(proto), keyFields, notKeyIgnoreFields);
        }

        public MappedCommand<T> CompileMerge<T>(T proto, string tableName, params string[] keyFields)
        {
            return CompileMerge(tableName, FieldSettings.FromType(proto), keyFields, Array.Empty<string>());
        }

        #endregion

        #endregion

        #endregion

        // names the caller gives (keys, ignored fields) match members case-insensitively, as parameters do by default;
        // an exact match wins, and the generated text gets the member's spelling; unknown names are kept as given
        private static string[] ToMemberNames<T>(IEnumerable<string> names, IList<FieldSettings<T>> settings)
        {
            return (names ?? [])
                .Select(x => settings.Any(y => y.Name == x)
                    ? x
                    : settings.FirstOrDefault(y => string.Equals(y.Name, x, StringComparison.OrdinalIgnoreCase))?.Name ?? x)
                .ToArray();
        }

        // without key fields the generated where / on clause is empty, which the server rejects
        private static void CheckKeyFields(IList<string> keyFields)
        {
            if (keyFields == null || keyFields.Count == 0)
            {
                throw new ArgumentException("At least one key field is required", nameof(keyFields));
            }
        }
    }
}
