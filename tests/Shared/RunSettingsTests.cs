using System.Globalization;
using AwesomeAssertions;
using Xunit;

namespace Gerakul.FastSql.Tests.Shared
{
    // compiled into every test project, so each project checks that its xunit.runner.json is applied
    public class RunSettingsTests
    {
        [Fact]
        public void Culture_IsInvariant()
        {
            CultureInfo.CurrentCulture.Should().Be(CultureInfo.InvariantCulture);
            CultureInfo.CurrentUICulture.Should().Be(CultureInfo.InvariantCulture);
        }
    }
}
