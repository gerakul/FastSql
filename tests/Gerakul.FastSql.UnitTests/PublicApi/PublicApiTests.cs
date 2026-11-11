using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using PublicApiGenerator;
using Xunit;

namespace Gerakul.FastSql.UnitTests.PublicApi
{
    // Pins the public API of the libraries. When an API change is intended (for example during the upgrade),
    // replace Approved/<Assembly>.txt with the <Assembly>.received.txt written next to it.
    public class PublicApiTests
    {
        private static readonly ApiGeneratorOptions options = new ApiGeneratorOptions()
        {
            // not API: these change with the target framework and project metadata
            ExcludeAttributes = new[]
            {
                "System.Runtime.Versioning.TargetFrameworkAttribute",
                "System.Reflection.AssemblyMetadataAttribute"
            }
        };

        [Fact]
        public void Common_PublicApi_MatchesApproved()
        {
            VerifyPublicApi(typeof(ICommandCreator).Assembly);
        }

        [Fact]
        public void SqlServer_PublicApi_MatchesApproved()
        {
            VerifyPublicApi(typeof(SqlContextProvider).Assembly);
        }

        [Fact]
        public void PostgreSql_PublicApi_MatchesApproved()
        {
            VerifyPublicApi(typeof(NpgsqlContextProvider).Assembly);
        }

        private static void VerifyPublicApi(Assembly assembly, [CallerFilePath] string sourceFilePath = "")
        {
            var name = assembly.GetName().Name;
            var approvedDirectory = Path.Combine(Path.GetDirectoryName(sourceFilePath), "Approved");
            var approvedPath = Path.Combine(approvedDirectory, name + ".txt");
            var receivedPath = Path.Combine(approvedDirectory, name + ".received.txt");

            var actual = Normalize(assembly.GeneratePublicApi(options));
            var approved = File.Exists(approvedPath) ? Normalize(File.ReadAllText(approvedPath)) : null;

            if (actual == approved)
            {
                if (File.Exists(receivedPath))
                {
                    File.Delete(receivedPath);
                }

                return;
            }

            Directory.CreateDirectory(approvedDirectory);
            File.WriteAllText(receivedPath, actual);
            Assert.Fail($"The public API of {name} differs from the approved one. Compare {receivedPath} with {approvedPath}; "
                + "if the change is intended, replace the approved file with the received one.");
        }

        private static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").TrimEnd('\n') + "\n";
        }
    }
}
