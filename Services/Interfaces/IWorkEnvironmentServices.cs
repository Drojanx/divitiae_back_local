using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace divitiae_api.Services.Interfaces
{
    public interface IWorkEnvironmentServices
    {
        Task InsertEnvironment(WorkEnvironment environment);
        //Task<WorkEnvironment> InsertWelcomeEnvironment(WorkEnvironment we, string userName);
        Task UpdateEnvironment(WorkEnvironment environment);
        Task DeleteEnvironment(string id);
        Task<List<WorkEnvironment>> GetAllEnvironments();
        Task<WorkEnvironment> GetEnvironmentById(string id);
        Task<WorkEnvironmentDTO> GetEnvironmentDTO(string userId, string id);
        //Task<WorkEnvironmentDTO> CanAccessWorkEnvironment(string userId, string weId);
        //Task CanModifyWorkEnvironment(string userId, string weId);

    }
}
