using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Platform.Authentication.Attributes;
using Altinn.Platform.Authentication.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Filters
{
    public class TrimStringActionFilterTests
    {
        private static ActionExecutingContext CreateContext(object model)
        {
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor());
            var actionArguments = new Dictionary<string, object?> { { "model", model } };
            return new ActionExecutingContext(
                actionContext: actionContext,
                filters: new List<IFilterMetadata>(),
                actionArguments: actionArguments,
                controller: null!);
        }

        [Fact]
        public void Trims_CreateRequestSystemUser_String_Properties()
        {
            // Arrange
            var model = new CreateRequestSystemUser
            {
                ExternalRef = "  ext  ",
                SystemId = "  sys  ",
                PartyOrgNo = "\t123456789\t",
                Rights = new List<string> { "Read", "Write" },
                RedirectUrl = "  https://example.com/  ",
                FreeText = "  do not trim  "
            };

            var context = CreateContext(model);
            var filter = new TrimStringsActionFilter();

            // Act
            filter.OnActionExecuting(context);

            // Assert
            Assert.Equal("ext", model.ExternalRef);
            Assert.Equal("sys", model.SystemId);
            Assert.Equal("123456789", model.PartyOrgNo);
            Assert.Equal("https://example.com/", model.RedirectUrl);
            Assert.Equal("  do not trim  ", model.FreeText); // [DoNotTrim]
        }

        [Fact]
        public void Does_Not_Trim_Null_Strings()
        {
            // Arrange
            var model = new CreateRequestSystemUser
            {
                ExternalRef = null,
                SystemId = null,
                PartyOrgNo = null,
                Rights = new List<string>(),
                RedirectUrl = null,
                FreeText = null
            };

            var context = CreateContext(model);
            var filter = new TrimStringsActionFilter();

            // Act
            filter.OnActionExecuting(context);

            // Assert
            Assert.Null(model.ExternalRef);
            Assert.Null(model.SystemId);
            Assert.Null(model.PartyOrgNo);
            Assert.Null(model.RedirectUrl);
            Assert.Null(model.FreeText);
        }

        [Fact]
        public void Skips_DoNotTrim_Attribute()
        {
            // Arrange
            var model = new CreateRequestSystemUser
            {
                FreeText = "  should not trim  "
            };

            var context = CreateContext(model);
            var filter = new TrimStringsActionFilter();

            // Act
            filter.OnActionExecuting(context);

            // Assert
            Assert.Equal("  should not trim  ", model.FreeText);
        }

        [Fact]
        public void Does_Not_Trim_ListOfString_Elements()
        {
            // Arrange
            var model = new CreateRequestSystemUser
            {
                Rights = new List<string> { "  Read  ", " Write ", null, "  " }
            };

            var context = CreateContext(model);
            var filter = new TrimStringsActionFilter();

            // Act
            filter.OnActionExecuting(context);

            // Assert
            Assert.Equal(new List<string> { "  Read  ", " Write ", null, "  " }, model.Rights);
        }

        [Fact]
        public void Does_Not_Trim_ArrayOfString_Elements()
        {
            // Arrange
            var model = new ModelWithArray
            {
                Tags = new[] { "  tag1  ", " tag2 ", null, "  " }
            };

            var context = CreateContext(model);
            var filter = new TrimStringsActionFilter();

            // Act
            filter.OnActionExecuting(context);

            // Assert
            Assert.Equal(new[] { "  tag1  ", " tag2 ", null, "  " }, model.Tags);
        }
    }

    public class CreateRequestSystemUser
    {
        [JsonPropertyName("externalRef")]
        public string? ExternalRef { get; set; }

        [Required]
        [JsonPropertyName("systemId")]
        public string SystemId { get; set; }

        [Required]
        [JsonPropertyName("partyOrgNo")]
        public string PartyOrgNo { get; set; }

        [Required]
        [JsonPropertyName("rights")]
        public List<string> Rights { get; set; }

        [JsonPropertyName("redirectUrl")]
        public string? RedirectUrl { get; set; }

        [DoNotTrim]
        public string? FreeText { get; set; }
    }

    public class ModelWithArray
    {
        public string[] Tags { get; set; }
    }
}
