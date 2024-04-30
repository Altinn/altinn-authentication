using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.SystemRegister.Models;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

#nullable enable
public class SystemRegisterRepositoryDbTests : DbTestBase
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

        Guid? registeredSystemId = await Repository.CreateRegisteredSystem(
            new RegisteredSystem
            {
                Description = "Test",
                SystemTypeId = friendlyId,
                SystemVendor = "Awesome"
            });

        Assert.NotEqual(Guid.Empty, registeredSystemId);

        RegisteredSystem? isitthere = await Repository.GetRegisteredSystemById(friendlyId);
        Assert.True(isitthere is not null);
        Assert.Equal("Awesome", isitthere.SystemVendor);
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

        Guid? registeredSystemId = await Repository.CreateRegisteredSystem(
            new RegisteredSystem
            {
                Description = "Test",
                SystemTypeId = friendlyId,
                SystemVendor = "Awesome"
            });

        string friendlyId2 = "Second_Test_System_String_Human_Readable_Id";

        Guid? registeredSystemId2 = await Repository.CreateRegisteredSystem(
            new RegisteredSystem
            {
                Description = "Test",
                SystemTypeId = friendlyId2,
                SystemVendor = "Awesome"
            });

        List<RegisteredSystem> res = await Repository.GetAllActiveSystems();
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

        Guid? registeredSystemId = await Repository.CreateRegisteredSystem(
            new RegisteredSystem
            {
                Description = "Test",
                SystemTypeId = friendlyId,
                SystemVendor = "Awesome"
            });

        Assert.NotEqual(Guid.Empty, registeredSystemId);

        Guid? guid = await Repository.RetrieveGuidFromStringId(friendlyId);
        Assert.True(guid is not null);
                
        var succeed = await Repository.RenameRegisteredSystemByGuid((Guid)guid, friendlyId2);
        Assert.Equal(1, succeed);

        var there = await Repository.GetRegisteredSystemById(friendlyId2);        
        Assert.Equal(friendlyId2, there?.SystemTypeId);         
    }
}