using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Interfaces;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    public class PDPPermitMock: IPDP
    {
        /// <inheritdoc/>
        public Task<XacmlJsonResponse> GetDecisionForRequest(XacmlJsonRequestRoot xacmlJsonRequest)
        {
            //var response = new XacmlJsonResponse
            //{
            //    Response = new List<XacmlJsonResult>(new[] { new XacmlJsonResult { Decision = "Permit" } })
            //};

            //return Task.FromResult(response);

            string decision = "Permit";

            // Check for claim "userid" with value "2234" in all AccessSubjects
            if (xacmlJsonRequest?.Request?.AccessSubject != null)
            {
                // If AccessSubject is a list, iterate through each subject
                foreach (var subject in xacmlJsonRequest.Request.AccessSubject)
                {
                    if (subject?.Attribute != null)
                    {
                        foreach (var attribute in subject.Attribute)
                        {
                            if (attribute?.AttributeId == "urn:altinn:userid" && attribute?.Value != null && attribute.Value.Contains("2234"))
                            {
                                decision = "NotApplicable";
                                break;
                            }
                        }
                    }

                    if (decision == "NotApplicable")
                    {
                        break;
                    }
                }
            }

            var response = new XacmlJsonResponse
            {
                Response = new List<XacmlJsonResult>(new[] { new XacmlJsonResult { Decision = decision } })
            };

            return Task.FromResult(response);
        }

        /// <inheritdoc/>
        public Task<bool> GetDecisionForUnvalidateRequest(XacmlJsonRequestRoot xacmlJsonRequest, ClaimsPrincipal user)
        {
            return Task.FromResult(true);
        }
    }
}
