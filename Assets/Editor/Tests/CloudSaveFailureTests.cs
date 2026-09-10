using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class CloudSaveFailureTests
    {
        [TestCase("400", "BadParameterException", "400/BadParameterException")]
        [TestCase("401", "UnauthorizedException", "401/UnauthorizedException")]
        [TestCase("429", "TooManyRequests", "429/TooManyRequests")]
        [TestCase(null, null, "NO_RESPONSE")]
        [TestCase("secret-token", "email@example.com", "NO_RESPONSE")]
        [TestCase("500", "<b>data</b>", "500")]
        public void RetainsOnlySafeFailureIdentifiers(string status, string error, string expected)
        {
            Assert.That(MukJumpAccountRuntime.FormatCloudSaveFailure(status, error),
                Is.EqualTo("서버 저장을 다시 시도합니다 (" + expected + ")"));
        }
    }
}
