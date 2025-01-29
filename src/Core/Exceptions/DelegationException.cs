using System;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Core.Exceptions
{
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
