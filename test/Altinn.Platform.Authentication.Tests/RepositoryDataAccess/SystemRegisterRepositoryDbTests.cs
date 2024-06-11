using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

#nullable enable
public class SystemRegisterRepositoryDbTests(DbFixture dbFixture) 
    : DbTestBase(dbFixture)
{
    protected ISystemRegisterRepository Repository => Services.GetRequiredService<ISystemRegisterRepository>();

    protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddPersistanceLayer();
        base.ConfigureServices(services);
    }

    /// <summary>
    /// Inserts a new Product / Registered System, which define the needed
    /// Rights to be delegated by the end user to the 
    /// Product (identified by ClientId in Idporten) .
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task SystemRegister_InsertRegisteredSystem()
    {
        string friendlyId = "Awesome_Test_System_String_Human_Readable_Id";
        string[] defaultRights = [];

        Guid? registeredSystemId = await Repository.CreateRegisteredSystem(
            new RegisterSystemRequest
            {
                FriendlyProductName = "Test",
                CustomSystemId = friendlyId,
                SystemVendorOrgNumber = "8934q343453",
                Rights = new List<Right>() 
                { 
                    new Right() 
                    {
                        Resources = new List<AttributePair>()
                        {
                            new AttributePair()
                            {
                                Id = "urn:altinn:resource",
                                Value = "Test"
                            }
                        }
                    } 
                }
            }, 
            defaultRights);

        Assert.NotEqual(Guid.Empty, registeredSystemId);

        RegisterSystemResponse? isitthere = await Repository.GetRegisteredSystemById(friendlyId);
        Assert.True(isitthere is not null);
        Assert.Equal("Awesome", isitthere.SystemVendorOrgName);
    }

    /// <summary>
    /// Retrieves the list of all available ( not inactivated / soft deleted)
    /// Products / Registered Systems that can be selected for Integration.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task SystemRegister_GetAllActiveSystems()
    {
        string friendlyId = "Awesome_Test_System_String_Human_Readable_Id";
        string[] defaultRights = [];

        Guid? registeredSystemId = await Repository.CreateRegisteredSystem(
            new RegisterSystemRequest
            {
                FriendlyProductName = "Test",
                CustomSystemId = friendlyId,
                SystemVendorOrgNumber = "8974234234"
            },
            defaultRights);

        string friendlyId2 = "Second_Test_System_String_Human_Readable_Id";

        Guid? registeredSystemId2 = await Repository.CreateRegisteredSystem(
            new RegisterSystemRequest
            {
                FriendlyProductName = "Test",
                CustomSystemId = friendlyId2,
                SystemVendorOrgNumber = "8974234234"
            },
            defaultRights);

        List<RegisterSystemResponse> res = await Repository.GetAllActiveSystems();
        Assert.NotEmpty(res);
    }

    /// <summary>
    /// Test renaming a Product
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task SystemRegister_RenameRegisteredSystemById()
    {
        string friendlyId = "Awesome_Test_System_String_Human_Readable_Id";
        string friendlyId2 = "Awesome_Test_System_String_Human_Readable_Id2";
        string[] defaultRights = [];

        Guid? registeredSystemId = await Repository.CreateRegisteredSystem(
            new RegisterSystemRequest
            {
                FriendlyProductName = "Test",
                CustomSystemId = friendlyId,
                SystemVendorOrgNumber = "8974234234"
            },
            defaultRights);

        Assert.NotEqual(Guid.Empty, registeredSystemId);

        Guid? guid = await Repository.RetrieveGuidFromStringId(friendlyId);
        Assert.True(guid is not null);
                
        var succeed = await Repository.RenameRegisteredSystemByGuid((Guid)guid, friendlyId2);
        Assert.Equal(1, succeed);

        var there = await Repository.GetRegisteredSystemById(friendlyId2);        
        Assert.Equal(friendlyId2, there?.CustomSystemId);         
    }

    [Fact] 
    public async Task SystemRegister_SetDeleteRegisteredSystemById()
    {
        string friendlyId = "Awesome_Test_System_String_Human_Readable_Id";
        string[] defaultRights = [];

        Guid? registeredSystemId = await Repository.CreateRegisteredSystem(
            new RegisterSystemRequest
            {
                FriendlyProductName = "Test",
                CustomSystemId = friendlyId,
                SystemVendorOrgNumber = "8974234234"
            },
            defaultRights);

        Assert.NotEqual(Guid.Empty, registeredSystemId);

        RegisterSystemResponse? isitthere = await Repository.GetRegisteredSystemById(friendlyId);
        Assert.True(isitthere is not null);
        Assert.Equal("Awesome", isitthere.SystemVendorOrgName);

        await Repository.SetDeleteRegisteredSystemById(friendlyId);

        var there = await Repository.GetRegisteredSystemById(friendlyId);        
        Assert.True(there?.SoftDeleted);
    }

    [Fact]
    public async Task SystemRegister_GetDefaultRightsForRegisteredSystem()
    {
        string friendlyId = "Awesome_Test_System_String_Human_Readable_Id";
        string[] rights = [];

        Guid? registeredSystemId = await Repository.CreateRegisteredSystem(
            new RegisterSystemRequest
            {
                FriendlyProductName = "Test",
                CustomSystemId = friendlyId,
                SystemVendorOrgNumber = "8974234234",
                Rights = new List<Right>()
                {
                    new Right()
                    {
                        Resources = new List<AttributePair>()
                        {
                            new AttributePair()
                            {
                                Id = "urn:altinn:resource",
                                Value = "Test"
                            }
                        }
                    }
                }
            },
            rights);

        var defRight = Repository.GetRightsForRegisteredSystem(friendlyId);
        Assert.NotNull(defRight);        
    }   
}