using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gerakul.FastSql.Common
{
    internal class CommandCompilator
    {
        private DbContext context;

        public CommandCompilator(DbContext context)
        {
            this.context = context;
        }

        #region Simple

        internal SimpleCommand Compile(string commandText)
        {
            return new SimpleCommand(context, commandText);
        }

        #endregion

        #region Mapped

        public MappedCommand<T> Compile<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings)
        {
            return new MappedCommand<T>(context, commandText, paramNames, settings);
        }

        public MappedCommand<T> Compile<T>(string commandText, IList<FieldSettings<T>> settings)
        {
            return new MappedCommand<T>(context, commandText, context.ParseCommandText(commandText), settings);
        }

        public MappedCommand<T> Compile<T>(string commandText, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(context, commandText, paramNames, FieldSettings.FromType<T>(fromTypeOption));
        }

        public MappedCommand<T> Compile<T>(string commandText, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(context, commandText, context.ParseCommandText(commandText), FieldSettings.FromType<T>(fromTypeOption));
        }

        public MappedCommand<T> Compile<T>(T proto, string commandText, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(context, commandText, paramNames, FieldSettings.FromType(proto, fromTypeOption));
        }

        public MappedCommand<T> Compile<T>(T proto, string commandText, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new MappedCommand<T>(context, commandText, context.ParseCommandText(commandText), FieldSettings.FromType(proto, fromTypeOption));
        }

        #region Special commands

        #region Insert

        public MappedCommand<T> CompileInsert<T>(string tableName, IList<FieldSettings<T>> settings, bool getIdentity, params string[] ignoreFields)
        {
            var fields = settings.Select(x => x.Name.ToLowerInvariant()).Except(ignoreFields.Select(x => x.ToLowerInvariant())).ToArray();
            string query = context.CommandTextGenerator.Insert(tableName, getIdentity, fields);
            return new MappedCommand<T>(context, query, fields, settings);
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
            string query = context.CommandTextGenerator.Update(tableName, keyFields, fieldsToUpdate);
            return new MappedCommand<T>(context, query, fields, settings);
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

        #region Merge

        public MappedCommand<T> CompileMerge<T>(string tableName, IList<FieldSettings<T>> settings, IList<string> keyFields, IList<string> notKeyIgnoreFields)
        {
            var fields = settings.Select(x => x.Name.ToLowerInvariant()).Except(notKeyIgnoreFields.Select(x => x.ToLowerInvariant())).ToArray();
            var fieldsToUpdate = fields.Except(keyFields.Select(x => x.ToLowerInvariant())).ToArray();
            string query = context.CommandTextGenerator.Merge(tableName, keyFields, fieldsToUpdate);
            return new MappedCommand<T>(context, query, fields, settings);
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
