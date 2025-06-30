using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Platform.Authentication.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Middlewares
{
    public class RawJsonInspectionMiddlewareTests
    {
        private static DefaultHttpContext CreateHttpContext(string method, string path, string contentType, string body)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            context.Request.ContentType = contentType;
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Response.Body = new MemoryStream();
            return context;
        }

        [Fact]
        public async Task InvokeAsync_ValidAccessPackages_AllowsRequest()
        {
            // Arrange
            string json = @"{
            ""accessPackages"": [
                { ""urn"": ""urn:1"" },
                { ""urn"": ""urn:2"" }
            ]
        }";
            var context = CreateHttpContext("POST", "/authentication/api/v1/systemregister", "application/json", json);
            bool nextCalled = false;
            var middleware = new RawJsonInspectionMiddleware(ctx => 
            { 
                nextCalled = true; 
                return Task.CompletedTask; 
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled);
            Assert.NotEqual(400, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_MultipleUrnInAccessPackage_ReturnsBadRequest()
        {
            // Arrange
            string json = @"{
            ""accessPackages"": [
                { ""urn"": ""4"",""urn"":""1"", ""urn"":""3"",""urn"":""2"" }
            ]
        }";
            var context = CreateHttpContext("POST", "/authentication/api/v1/systemregister/vendor", "application/json", json);
            var middleware = new RawJsonInspectionMiddleware(_ => Task.CompletedTask);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(400, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            var reader = new StreamReader(context.Response.Body);
            string response = await reader.ReadToEndAsync();
            ProblemDetails problem = JsonSerializer.Deserialize<ProblemDetails>(response);
            Assert.Contains("Each AccessPackage object must contain only one 'urn' property.", problem.Detail);
        }

        [Fact]
        public async Task InvokeAsync_NoAccessPackages_AllowsRequest()
        {
            // Arrange
            string json = @"{ ""otherProperty"": 123 }";
            var context = CreateHttpContext("POST", "/authentication/api/v1/systemregister", "application/json", json);
            bool nextCalled = false;
            var middleware = new RawJsonInspectionMiddleware(ctx => 
            { 
                nextCalled = true; 
                return Task.CompletedTask; 
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled);
            Assert.NotEqual(400, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_NonJsonContentType_AllowsRequest()
        {
            // Arrange
            string json = @"{ ""accessPackages"": [ { ""urn"": ""urn:1"" } ] }";
            var context = CreateHttpContext("POST", "/authentication/api/v1/systemregister", "text/plain", json);
            bool nextCalled = false;
            var middleware = new RawJsonInspectionMiddleware(ctx => 
            { 
                nextCalled = true; 
                return Task.CompletedTask; 
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled);
            Assert.NotEqual(400, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WrongPath_AllowsRequest()
        {
            // Arrange
            string json = @"{ ""accessPackages"": [ { ""urn"": ""urn:1"" } ] }";
            var context = CreateHttpContext("POST", "/other/api/v1/systemregister", "application/json", json);
            bool nextCalled = false;
            var middleware = new RawJsonInspectionMiddleware(ctx => 
            { 
                nextCalled = true; 
                return Task.CompletedTask; 
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled);
            Assert.NotEqual(400, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_RootArrayWithMultipleUrnInObject_ReturnsBadRequest()
        {
            // Arrange: root array with an object that has multiple "urn" properties
            string json = @"[
                       { ""urn"": ""urn:1"", ""urn"": ""urn:2"" }
            ]";
            var context = CreateHttpContext("PUT", "/authentication/api/v1/systemregister/vendor/123/accesspackages", "application/json", json);
            var middleware = new RawJsonInspectionMiddleware(_ => Task.CompletedTask);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(400, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            var reader = new StreamReader(context.Response.Body);
            string response = await reader.ReadToEndAsync();
            ProblemDetails problem = JsonSerializer.Deserialize<ProblemDetails>(response);
            Assert.Contains("Each AccessPackage object must contain only one 'urn' property.", problem.Detail);
        }

    }
}
