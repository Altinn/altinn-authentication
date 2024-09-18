using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Xml;
using Altinn.AccessManagement.Core.Constants;
using Altinn.AccessManagement.Tests.Models;
using Altinn.Authorization.ABAC;
using Altinn.Authorization.ABAC.Constants;
using Altinn.Authorization.ABAC.Utils;
using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Storage.Interface.Models;
using Newtonsoft.Json;

namespace Altinn.AccessManagement.Tests.Mocks
{
    /// <summary>
    /// Mock for IPDP
    /// </summary>
    public class PepWithPDPAuthorizationMock : IPDP
    {
        private const string OrgAttributeId = "urn:altinn:org";

        private const string AppAttributeId = "urn:altinn:app";

        private const string UserAttributeId = "urn:altinn:userid";

        private const string AltinnRoleAttributeId = "urn:altinn:rolecode";

        private const string TaskAttributeId = "urn:altinn:task";

        private const string PartyAttributeId = "urn:altinn:partyid";

        /// <inheritdoc />
        public async Task<XacmlJsonResponse> GetDecisionForRequest(XacmlJsonRequestRoot xacmlJsonRequest)
        {
            return await Authorize(xacmlJsonRequest.Request);
        }

        private async Task<XacmlJsonResponse> Authorize(XacmlJsonRequest decisionRequest)
        {
            if (decisionRequest.MultiRequests == null || decisionRequest.MultiRequests.RequestReference == null
                || decisionRequest.MultiRequests.RequestReference.Count < 2)
            {
                XacmlContextRequest request = XacmlJsonXmlConverter.ConvertRequest(decisionRequest);
                XacmlContextResponse xmlResponse = await Authorize(request);
                return XacmlJsonXmlConverter.ConvertResponse(xmlResponse);
            }
            else
            {
                XacmlJsonResponse multiResponse = new XacmlJsonResponse();
                foreach (XacmlJsonRequestReference xacmlJsonRequestReference in decisionRequest.MultiRequests.RequestReference)
                {
                    XacmlJsonRequest jsonMultiRequestPart = new XacmlJsonRequest();

                    foreach (string refer in xacmlJsonRequestReference.ReferenceId)
                    {
                        List<XacmlJsonCategory> resourceCategoriesPart = decisionRequest.Resource.Where(i => i.Id.Equals(refer)).ToList();

                        if (resourceCategoriesPart.Count > 0)
                        {
                            if (jsonMultiRequestPart.Resource == null)
                            {
                                jsonMultiRequestPart.Resource = new List<XacmlJsonCategory>();
                            }

                            jsonMultiRequestPart.Resource.AddRange(resourceCategoriesPart);
                        }

                        List<XacmlJsonCategory> subjectCategoriesPart = decisionRequest.AccessSubject.Where(i => i.Id.Equals(refer)).ToList();

                        if (subjectCategoriesPart.Count > 0)
                        {
                            if (jsonMultiRequestPart.AccessSubject == null)
                            {
                                jsonMultiRequestPart.AccessSubject = new List<XacmlJsonCategory>();
                            }

                            jsonMultiRequestPart.AccessSubject.AddRange(subjectCategoriesPart);
                        }

                        List<XacmlJsonCategory> actionCategoriesPart = decisionRequest.Action.Where(i => i.Id.Equals(refer)).ToList();

                        if (actionCategoriesPart.Count > 0)
                        {
                            if (jsonMultiRequestPart.Action == null)
                            {
                                jsonMultiRequestPart.Action = new List<XacmlJsonCategory>();
                            }

                            jsonMultiRequestPart.Action.AddRange(actionCategoriesPart);
                        }
                    }

                    XacmlContextResponse partResponse = await Authorize(XacmlJsonXmlConverter.ConvertRequest(jsonMultiRequestPart));
                    XacmlJsonResponse xacmlJsonResponsePart = XacmlJsonXmlConverter.ConvertResponse(partResponse);

                    if (multiResponse.Response == null)
                    {
                        multiResponse.Response = new List<XacmlJsonResult>();
                    }

                    multiResponse.Response.Add(xacmlJsonResponsePart.Response.First());
                }

                return multiResponse;
            }
        }

        private async Task<XacmlContextResponse> Authorize(XacmlContextRequest decisionRequest)
        {
            decisionRequest = await Enrich(decisionRequest);

            XacmlPolicy policy = await GetPolicyAsync(decisionRequest);

            PolicyDecisionPoint pdp = new PolicyDecisionPoint();
            XacmlContextResponse xacmlContextResponse = pdp.Authorize(decisionRequest, policy);

            return xacmlContextResponse;
        }

        /// <inheritdoc/>
        public async Task<bool> GetDecisionForUnvalidateRequest(XacmlJsonRequestRoot xacmlJsonRequest, ClaimsPrincipal user)
        {
            XacmlJsonResponse response = await GetDecisionForRequest(xacmlJsonRequest);
            return DecisionHelper.ValidatePdpDecision(response.Response, user);
        }

        private async Task<XacmlContextRequest> Enrich(XacmlContextRequest request)
        {
            await EnrichResourceAttributes(request);

            return request;
        }

        private async Task EnrichResourceAttributes(XacmlContextRequest request)
        {
            XacmlContextAttributes resourceContextAttributes = request.GetResourceAttributes();
            XacmlResourceAttributes resourceAttributes = GetResourceAttributeValues(resourceContextAttributes);

            bool resourceAttributeComplete = false;

            if (!string.IsNullOrEmpty(resourceAttributes.OrgValue) &&
                !string.IsNullOrEmpty(resourceAttributes.AppValue) &&
                !string.IsNullOrEmpty(resourceAttributes.InstanceValue) &&
                !string.IsNullOrEmpty(resourceAttributes.ResourcePartyValue) &&
                !string.IsNullOrEmpty(resourceAttributes.TaskValue))
            {
                // The resource attributes are complete
                resourceAttributeComplete = true;
            }
            else if (!string.IsNullOrEmpty(resourceAttributes.OrgValue) &&
                !string.IsNullOrEmpty(resourceAttributes.AppValue) &&
                string.IsNullOrEmpty(resourceAttributes.InstanceValue) &&
                !string.IsNullOrEmpty(resourceAttributes.ResourcePartyValue) &&
                string.IsNullOrEmpty(resourceAttributes.TaskValue))
            {
                // The resource attributes are complete
                resourceAttributeComplete = true;
            }
            else if (!string.IsNullOrEmpty(resourceAttributes.OrgValue) &&
            !string.IsNullOrEmpty(resourceAttributes.AppValue) &&
            !string.IsNullOrEmpty(resourceAttributes.InstanceValue) &&
            !string.IsNullOrEmpty(resourceAttributes.ResourcePartyValue) &&
            !string.IsNullOrEmpty(resourceAttributes.AppResourceValue) &&
            resourceAttributes.AppResourceValue.Equals("events"))
            {
                // The resource attributes are complete
                resourceAttributeComplete = true;
            }
            else if (!string.IsNullOrEmpty(resourceAttributes.ResourceRegistryId) &&
                     !string.IsNullOrEmpty(resourceAttributes.ResourcePartyValue))
            {
                // The resource attributes are complete
                resourceAttributeComplete = true;
            }

            if (!resourceAttributeComplete)
            {
                Instance instanceData = GetTestInstance(resourceAttributes.InstanceValue);

                if (string.IsNullOrEmpty(resourceAttributes.OrgValue))
                {
                    resourceContextAttributes.Attributes.Add(GetOrgAttribute(instanceData));
                }

                if (string.IsNullOrEmpty(resourceAttributes.AppValue))
                {
                    resourceContextAttributes.Attributes.Add(GetAppAttribute(instanceData));
                }

                if (string.IsNullOrEmpty(resourceAttributes.TaskValue))
                {
                    resourceContextAttributes.Attributes.Add(GetProcessElementAttribute(instanceData));
                }

                if (string.IsNullOrEmpty(resourceAttributes.ResourcePartyValue))
                {
                    resourceContextAttributes.Attributes.Add(GetPartyAttribute(instanceData));
                }

                resourceAttributes.ResourcePartyValue = instanceData.InstanceOwner.PartyId;
            }

            await EnrichSubjectAttributes(request, resourceAttributes.ResourcePartyValue);
        }

        private static XacmlAttribute GetOrgAttribute(Instance instance)
        {
            XacmlAttribute attribute = new XacmlAttribute(new Uri(OrgAttributeId), false);
            attribute.AttributeValues.Add(new XacmlAttributeValue(new Uri(XacmlConstants.DataTypes.XMLString), instance.Org));
            return attribute;
        }

        private static XacmlAttribute GetAppAttribute(Instance instance)
        {
            XacmlAttribute attribute = new XacmlAttribute(new Uri(AppAttributeId), false);
            attribute.AttributeValues.Add(new XacmlAttributeValue(new Uri(XacmlConstants.DataTypes.XMLString), instance.AppId.Split('/')[1]));
            return attribute;
        }

        private static XacmlAttribute GetProcessElementAttribute(Instance instance)
        {
            XacmlAttribute attribute = new XacmlAttribute(new Uri(TaskAttributeId), false);
            attribute.AttributeValues.Add(new XacmlAttributeValue(new Uri(XacmlConstants.DataTypes.XMLString), instance.Process.CurrentTask.ElementId));
            return attribute;
        }

        private static XacmlAttribute GetPartyAttribute(Instance instance)
        {
            XacmlAttribute attribute = new XacmlAttribute(new Uri(PartyAttributeId), false);

            // When Party attribute is missing from input it is good to return it so PEP can get this information
            attribute.IncludeInResult = true;
            attribute.AttributeValues.Add(new XacmlAttributeValue(new Uri(XacmlConstants.DataTypes.XMLString), instance.InstanceOwner.PartyId));
            return attribute;
        }

        private static XacmlAttribute GetRoleAttribute(List<Role> roles)
        {
            XacmlAttribute attribute = new XacmlAttribute(new Uri(AltinnRoleAttributeId), false);
            foreach (Role role in roles)
            {
                attribute.AttributeValues.Add(new XacmlAttributeValue(new Uri(XacmlConstants.DataTypes.XMLString), role.Value));
            }

            return attribute;
        }

        private static void AddIfValueDoesNotExist(XacmlContextAttributes resourceAttributes, string attributeId, string attributeValue, string newAttributeValue)
        {
            if (string.IsNullOrEmpty(attributeValue))
            {
                resourceAttributes.Attributes.Add(GetAttribute(attributeId, newAttributeValue));
            }
        }

        private static XacmlAttribute GetAttribute(string attributeId, string attributeValue)
        {
            XacmlAttribute attribute = new XacmlAttribute(new Uri(attributeId), false);
            if (attributeId.Equals(XacmlRequestAttribute.PartyAttribute))
            {
                // When Party attribute is missing from input it is good to return it so PEP can get this information
                attribute.IncludeInResult = true;
            }

            attribute.AttributeValues.Add(new XacmlAttributeValue(new Uri(XacmlConstants.DataTypes.XMLString), attributeValue));
            return attribute;
        }

        private async Task EnrichSubjectAttributes(XacmlContextRequest request, string resourceParty)
        {
            // If there is no resource party then it is impossible to enrich roles
            if (string.IsNullOrEmpty(resourceParty))
            {
                return;
            }

            XacmlContextAttributes subjectContextAttributes = request.GetSubjectAttributes();

            int subjectUserId = 0;
            int resourcePartyId = Convert.ToInt32(resourceParty);

            foreach (XacmlAttribute xacmlAttribute in subjectContextAttributes.Attributes)
            {
                if (xacmlAttribute.AttributeId.OriginalString.Equals(UserAttributeId))
                {
                    subjectUserId = Convert.ToInt32(xacmlAttribute.AttributeValues.First().Value);
                }
            }

            if (subjectUserId == 0)
            {
                return;
            }

            List<Role> roleList = await GetDecisionPointRolesForUser(subjectUserId, resourcePartyId) ?? new List<Role>();

            subjectContextAttributes.Attributes.Add(GetRoleAttribute(roleList));
        }

        private static XacmlResourceAttributes GetResourceAttributeValues(XacmlContextAttributes resourceContextAttributes)
        {
            XacmlResourceAttributes resourceAttributes = new XacmlResourceAttributes();

            foreach (XacmlAttribute attribute in resourceContextAttributes.Attributes)
            {
                if (attribute.AttributeId.OriginalString.Equals(XacmlRequestAttribute.OrgAttribute))
                {
                    resourceAttributes.OrgValue = attribute.AttributeValues.First().Value;
                }

                if (attribute.AttributeId.OriginalString.Equals(XacmlRequestAttribute.AppAttribute))
                {
                    resourceAttributes.AppValue = attribute.AttributeValues.First().Value;
                }

                if (attribute.AttributeId.OriginalString.Equals(XacmlRequestAttribute.InstanceAttribute))
                {
                    resourceAttributes.InstanceValue = attribute.AttributeValues.First().Value;
                }

                if (attribute.AttributeId.OriginalString.Equals(XacmlRequestAttribute.PartyAttribute))
                {
                    resourceAttributes.ResourcePartyValue = attribute.AttributeValues.First().Value;
                }

                if (attribute.AttributeId.OriginalString.Equals(XacmlRequestAttribute.TaskAttribute))
                {
                    resourceAttributes.TaskValue = attribute.AttributeValues.First().Value;
                }

                if (attribute.AttributeId.OriginalString.Equals(XacmlRequestAttribute.AppResourceAttribute))
                {
                    resourceAttributes.AppResourceValue = attribute.AttributeValues.First().Value;
                }

                if (attribute.AttributeId.OriginalString.Equals(XacmlRequestAttribute.ResourceRegistryAttribute))
                {
                    resourceAttributes.ResourceRegistryId = attribute.AttributeValues.First().Value;
                }

                if (attribute.AttributeId.OriginalString.Equals(XacmlRequestAttribute.OrganizationNumberAttribute))
                {
                    resourceAttributes.OrganizationNumber = attribute.AttributeValues.First().Value;
                }
            }

            return resourceAttributes;
        }

        private static Task<List<Role>> GetDecisionPointRolesForUser(int coveredByUserId, int offeredByPartyId)
        {
            string rolesPath = GetRolesPath(coveredByUserId, offeredByPartyId);

            List<Role> roles = new List<Role>();
            if (File.Exists(rolesPath))
            {
                string content = File.ReadAllText(rolesPath);
                roles = (List<Role>)JsonConvert.DeserializeObject(content, typeof(List<Role>));
            }

            return Task.FromResult(roles);
        }

        private static string GetRolesPath(int userId, int resourcePartyId)
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PepWithPDPAuthorizationMock).Assembly.Location).LocalPath);
            var fullRolePath = Path.Combine(unitTestFolder, "..", "..", "..", "Data", "Roles", "user_" + userId, "party_" + resourcePartyId, "roles.json");
            return fullRolePath;
        }

        private async Task<XacmlPolicy> GetPolicyAsync(XacmlContextRequest request)
        {
            XacmlPolicy xacmlPolicy = ParsePolicy("policy.xml", GetPolicyPath(request));
            return await Task.FromResult(xacmlPolicy);
        }

        private static string GetPolicyPath(XacmlContextRequest request)
        {
            var resourceId = string.Empty;
            var partyId = string.Empty;
            foreach (var attributes in request.Attributes)
            {
                if (attributes.Category.OriginalString.Equals(XacmlConstants.MatchAttributeCategory.Resource))
                {
                    foreach (var attribute in attributes.Attributes)
                    {
                        switch (attribute.AttributeId.OriginalString)
                        {
                            case XacmlRequestAttribute
                                .ResourceRegistryAttribute:
                                resourceId = attribute.AttributeValues.First().Value;
                                break;
                            case XacmlRequestAttribute.PartyAttribute:
                                partyId = attribute.AttributeValues.First().Value;
                                break;
                        }
                    }
                }
            }

            return GetResourceAccessPolicyPath(resourceId);
        }

        private static XacmlPolicy ParsePolicy(string policyDocumentTitle, string policyPath)
        {
            XmlDocument policyDocument = new XmlDocument();
            policyDocument.Load(Path.Combine(policyPath, policyDocumentTitle));
            XacmlPolicy policy;
            using (XmlReader reader = XmlReader.Create(new StringReader(policyDocument.OuterXml)))
            {
                policy = XacmlParser.ParseXacmlPolicy(reader);
            }

            return policy;
        }

        private static string GetResourceAccessPolicyPath(string ressursid)
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PepWithPDPAuthorizationMock).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, "..", "..", "..", "Data", "Xacml", "3.0", "ResourceRegistry", $"{ressursid}");
        }

        private static string GetInstancePath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PepWithPDPAuthorizationMock).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, "..", "..", "..", "Data", "Instances");
        }

        private Instance GetTestInstance(string instanceId)
        {
            string partyPart = instanceId.Split('/')[0];
            string instancePart = instanceId.Split('/')[1];

            string content = File.ReadAllText(Path.Combine(GetInstancePath(), $"{partyPart}/{instancePart}.json"));
            Instance instance = (Instance)JsonConvert.DeserializeObject(content, typeof(Instance));
            return instance;
        }
    }

#pragma warning restore SA1600 // ElementsMustBeDocumented
}
