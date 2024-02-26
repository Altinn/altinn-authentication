﻿using Altinn.Platform.Authentication.Core.SystemRegister.Models;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces;

/// <summary>
/// Interface to the System Register Repository
/// </summary>
public interface ISystemRegisterRepository
{
    /// <summary>
    /// Returns the list of currently available (is_deleted = false ) Registered Systems
    /// </summary>
    /// <returns>List of SystemRegister</returns>
    Task<List<RegisteredSystem>> GetAllActiveSystems();

    /// <summary>
    /// Inserts a new Registered System, using an optimistic choice for the ID
    /// if the ID already exists the Insert fails, 
    /// and the calling system need to choose a different ID.
    /// The ID is a human readable string.
    /// The RegisteredSystems primary key in the db is a "hidden" Guid,
    /// making it possible to change the human readable Id.
    /// </summary>
    /// <returns>Returns the hidden system Guid</returns>
    Task<Guid?> CreateRegisteredSystem(RegisteredSystem toBeInserted);

    /// <summary>
    /// Returns a single RegisteredSystem, even if it was set to deleted.
    /// </summary>
    /// <returns>The Registered System</returns>
    Task<RegisteredSystem?> GetRegisteredSystemById(string id);

    /// <summary>
    /// The registered systems may be renamed,
    /// this works because they also have a hidden internal id.
    /// This is useful if the first attempt to name them when
    /// registering collided with an existing name.
    /// </summary>
    /// <returns>True if renamed</returns>
    Task<bool> RenameRegisteredSystemById(string id, string newName);

    /// <summary>
    /// Set's the product's is_deleted column to True.
    /// This will break any existing integrations.
    /// </summary>
    /// <param name="id">The id</param>
    /// <returns>True if set to deleted</returns>
    Task<bool> SetDeleteRegisteredSystemById(string id);
}
