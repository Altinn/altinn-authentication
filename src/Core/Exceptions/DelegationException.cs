using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Core.Exceptions
{
    [ExcludeFromCodeCoverage]
    public class DelegationException : Exception
    {
        public ProblemDetails ProblemDetails { get; }

        public DelegationException(ProblemDetails problemDetails)
            : base(problemDetails.Detail)
        {
            ProblemDetails = problemDetails;
        }
    }
}
