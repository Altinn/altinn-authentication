using System.Collections.Generic;
using Altinn.Platform.Authentication.Core.Models;
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
        public void DoesResourceAlreadyExists_NoDuplicates_ReturnsFalse()
        {
            // Arrange
            var newRights = new List<Right>
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

            var existingRights = new List<Right>
            {
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
            bool result = AuthenticationHelper.DoesResourceAlreadyExists(newRights, existingRights);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DoesResourceAlreadyExists_NoDuplicates_DifferentActions_ReturnsFalse()
        {
            // Arrange
            var newRights = new List<Right>
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

            var existingRights = new List<Right>
            {
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
            bool result = AuthenticationHelper.DoesResourceAlreadyExists(newRights, existingRights);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DoesResourceAlreadyExists_NoDuplicates_DifferentResource_ReturnsFalse()
        {
            // Arrange
            var newRights = new List<Right>
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

            var existingRights = new List<Right>
            {
                new Right
                {
                    Action = "Read",
                    Resource = new List<AttributePair>
                    {
                        new AttributePair { Id = "urn:altinn:resource", Value = "value2" }
                    }
                }
            };

            // Act
            bool result = AuthenticationHelper.DoesResourceAlreadyExists(newRights, existingRights);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DoesResourceAlreadyExists_WithDuplicates_ReturnsTrue()
        {
            // Arrange
            var newRights = new List<Right>
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

            var existingRights = new List<Right>
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
            bool result = AuthenticationHelper.DoesResourceAlreadyExists(newRights, existingRights);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void DoesResourceAlreadyExists_EmptyNewRights_ReturnsFalse()
        {
            // Arrange
            var newRights = new List<Right>();

            var existingRights = new List<Right>
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
            bool result = AuthenticationHelper.DoesResourceAlreadyExists(newRights, existingRights);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DoesResourceAlreadyExists_EmptyExistingRights_ReturnsFalse()
        {
            // Arrange
            var newRights = new List<Right>
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

            var existingRights = new List<Right>();

            // Act
            bool result = AuthenticationHelper.DoesResourceAlreadyExists(newRights, existingRights);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DoesResourceAlreadyExists_BothListsEmpty_ReturnsFalse()
        {
            // Arrange
            var newRights = new List<Right>();
            var existingRights = new List<Right>();

            // Act
            bool result = AuthenticationHelper.DoesResourceAlreadyExists(newRights, existingRights);

            // Assert
            Assert.False(result);
        }
    }
}
