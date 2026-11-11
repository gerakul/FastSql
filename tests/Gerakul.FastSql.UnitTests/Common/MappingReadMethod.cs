namespace Gerakul.FastSql.UnitTests.Common
{
    // The typed reading methods of DataReaderExtensions, so that one theory can run a scenario through each of them.
    // They share the column matching and value conversion code (ReadInfo), but build their results differently.
    public enum MappingReadMethod
    {
        ReadAll,
        ReadAllAsync,
        ReadAllAnonymous,
        ReadAllAnonymousAsync,
        ReadAllFirstColumn,
        ReadAllFirstColumnAsync
    }
}
