using Mockporten.Configuration;
using Mockporten.Services.Implementation;
using Mockporten.Services.Interfaces;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mockporten.Tests
{
    public class SharedAccessPasswordValidatorTests
    {
        private static SharedAccessPasswordValidator Build(
            string configured, int maxFailures = 5, int lockoutMinutes = 15)
        {
            GeneralSettings settings = new()
            {
                TestIdpSharedPassword = configured,
                SharedPasswordMaxFailures = maxFailures,
                SharedPasswordLockoutMinutes = lockoutMinutes
            };
            return new SharedAccessPasswordValidator(Options.Create(settings));
        }

        [Fact]
        public void CorrectPassword_ReturnsSuccess()
        {
            var v = Build("s3cret-shared");
            Assert.Equal(SharedPasswordResult.Success, v.Validate("s3cret-shared"));
        }

        [Theory]
        [InlineData("wrong")]
        [InlineData("")]
        [InlineData("s3cret-share")]   // prefix
        [InlineData("s3cret-shared ")] // trailing space
        public void WrongPassword_ReturnsInvalid(string attempt)
        {
            var v = Build("s3cret-shared");
            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate(attempt));
        }

        [Fact]
        public void UnconfiguredPassword_FailsClosed()
        {
            var v = Build("");
            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate(""));
            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate("anything"));
        }

        [Fact]
        public void NullAttempt_ReturnsInvalid()
        {
            var v = Build("s3cret-shared");
            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate(null));
        }

        [Fact]
        public void LocksOut_AfterMaxFailures()
        {
            var v = Build("s3cret-shared", maxFailures: 3);

            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate("x"));
            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate("x"));
            // 3rd failure trips the lockout.
            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate("x"));
            // Now locked out - even the correct password is refused.
            Assert.Equal(SharedPasswordResult.LockedOut, v.Validate("s3cret-shared"));
        }

        [Fact]
        public void SuccessfulAttempt_ResetsFailureCounter()
        {
            var v = Build("s3cret-shared", maxFailures: 3);

            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate("x"));
            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate("x"));
            Assert.Equal(SharedPasswordResult.Success, v.Validate("s3cret-shared"));

            // Counter reset - two more failures must not lock out yet.
            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate("x"));
            Assert.Equal(SharedPasswordResult.InvalidPassword, v.Validate("x"));
            Assert.Equal(SharedPasswordResult.Success, v.Validate("s3cret-shared"));
        }
    }
}
