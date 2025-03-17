using System;
using System.Collections.Generic;
using Altinn.Authentication.Core.Problems;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Helpers;
using Xunit;

namespace Altinn.Platform.Authentication.Tests
{
    public class AuthenticationHelperTests
    {
        [Fact]
        public void HasDuplicateRights_NoDuplicates_ReturnsFalse()
        {
            // Arrange
            var rights = new List<Right>
                {
                    new Right
                    {
                        Action = "Read",
                        Resource = new List<AttributePair>
                        {
                            new AttributePair { Id = "urn:altinn:resource", Value = "value1" }
                        }
                    },
                    new Right
                    {
                        Action = "Write",
                        Resource = new List<AttributePair>
                        {
                            new AttributePair { Id = "urn:altinn:resource", Value = "value2" }
                        }
                    }
                };

            // Act
            bool result = AuthenticationHelper.HasDuplicateRights(rights);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasDuplicateRights_SameRights_DifferentActions_ReturnsFalse()
        {
            // Arrange
            var rights = new List<Right>
                {
                    new Right
                    {
                        Action = "Read",
                        Resource = new List<AttributePair>
                        {
                            new AttributePair { Id = "urn:altinn:resource", Value = "value1" }
                        }
                    },
                    new Right
                    {
                        Action = "Write",
                        Resource = new List<AttributePair>
                        {
                            new AttributePair { Id = "urn:altinn:resource", Value = "value1" }
                        }
                    }
                };

            // Act
            bool result = AuthenticationHelper.HasDuplicateRights(rights);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasDuplicateRights_WithDuplicates_ReturnsTrue()
        {
            // Arrange
            var rights = new List<Right>
                {
                    new Right
                    {
                        Action = "Read",
                        Resource = new List<AttributePair>
                        {
                            new AttributePair { Id = "urn:altinn:resource", Value = "value1" }
                        }
                    },
                    new Right
                    {
                        Action = "Read",
                        Resource = new List<AttributePair>
                        {
                            new AttributePair { Id = "urn:altinn:resource", Value = "value1" }
                        }
                    }
                };

            // Act
            bool result = AuthenticationHelper.HasDuplicateRights(rights);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasDuplicateRights_EmptyList_ReturnsFalse()
        {
            // Arrange
            var rights = new List<Right>();

            // Act
            bool result = AuthenticationHelper.HasDuplicateRights(rights);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasDuplicateRights_SingleRight_ReturnsFalse()
        {
            // Arrange
            var rights = new List<Right>
                {
                    new Right
                    {
                        Action = "Read",
                        Resource = new List<AttributePair>
                        {
                            new AttributePair { Id = "urn:altinn:resource", Value = "value1" }
                        }
                    }
                };

            // Act
            bool result = AuthenticationHelper.HasDuplicateRights(rights);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateRedirectUrl_ValidUrlWithoutQuery_ReturnsTrue()
        {
            // Arrange

            List<Uri> allowedRedirectUrls = new List<Uri>
                    {
                        new Uri("https://example.com/callback")
                    };
            string redirectURL = "https://example.com/callback";

            // Act
            var result = AuthenticationHelper.ValidateRedirectUrl(redirectURL, allowedRedirectUrls);

            // Assert
            Assert.True(result.Value);
        }

        [Fact]
        public void ValidateRedirectUrl_ValidUrlWithQuery_ReturnsTrue()
        {
            // Arrange
            List<Uri> allowedRedirectUrls = new List<Uri>
                    {
                        new Uri("https://example.com/callback")
                    };
            string redirectURL = "https://example.com/callback?param=value";

            // Act
            var result = AuthenticationHelper.ValidateRedirectUrl(redirectURL, allowedRedirectUrls);

            // Assert
            Assert.True(result.Value);
        }

        [Fact]
        public void ValidateRedirectUrl_InvalidUrl_ReturnsProblem()
        {
            // Arrange
            List<Uri> allowedRedirectUrls = new List<Uri>
                    {
                        new Uri("https://example.com/callback")
                    };
            string redirectURL = "https://invalid.com/callback";

            // Act
            var result = AuthenticationHelper.ValidateRedirectUrl(redirectURL, allowedRedirectUrls);

            // Assert
            Assert.False(result.Value);
            Assert.Equal(Problem.RedirectUriNotFound, result.Problem);
        }

        [Fact]
        public void ValidateRedirectUrl_EmptyAllowedRedirectUrls_ReturnsProblem()
        {
            // Arrange

            List<Uri> allowedRedirectUrls = new List<Uri>();
            string redirectURL = "https://example.com/callback";

            // Act
            var result = AuthenticationHelper.ValidateRedirectUrl(redirectURL, allowedRedirectUrls);

            // Assert
            Assert.False(result.Value);
            Assert.Equal(Problem.RedirectUriNotFound, result.Problem);
        }

        [Fact]
        public void ValidateRedirectUrl_ValidUrlWithFragment_ReturnsTrue()
        {
            // Arrange

            List<Uri> allowedRedirectUrls = new List<Uri>
                {
                    new Uri("https://example.com/callback")
                };
            string redirectURL = "https://example.com/callback#section";

            // Act
            var result = AuthenticationHelper.ValidateRedirectUrl(redirectURL, allowedRedirectUrls);

            // Assert
            Assert.True(result.Value);
        }

        [Fact]
        public void ValidateRedirectUrl_ValidUrlWithDifferentPath_ReturnsFalse()
        {
            // Arrange
            List<Uri> allowedRedirectUrls = new List<Uri>
                {
                    new Uri("https://example.com/callback")
                };
            string redirectURL = "https://example.com/callback/path";

            // Act
            var result = AuthenticationHelper.ValidateRedirectUrl(redirectURL, allowedRedirectUrls);

            // Assert
            Assert.False(result.Value);
        }

        [Fact]
        public void ValidateRedirectUrl_ValidUrlWithPort_ReturnsTrue()
        {
            // Arrange
            List<Uri> allowedRedirectUrls = new List<Uri>
                {
                    new Uri("https://example.com:8080/callback")
                };
            string redirectURL = "https://example.com:8080/callback";

            // Act
            var result = AuthenticationHelper.ValidateRedirectUrl(redirectURL, allowedRedirectUrls);

            // Assert
            Assert.True(result.Value);
        }
    }
}
