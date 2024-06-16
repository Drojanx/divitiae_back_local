using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class AppTaskDTO
    {
        public AppTaskDTO() { } 
        public string Id { get; set; } = string.Empty;
        public TaskUserDTO CreatedBy { get; set; } = new TaskUserDTO();
        public long CreatedOn { get; set; }
        public TaskEnvironmentDTO Environment { get; set; } = new TaskEnvironmentDTO();
        public TaskUserDTO AssignedUser { get; set; } = new TaskUserDTO();
        public TaskWorkspaceDTO? Workspace { get; set; }
        public TaskAppDTO? App { get; set; }
        public TaskItemDTO? Item { get; set; }
        public long DueDate { get; set; }
        public string Information { get; set; } = string.Empty;
        public List<TaskCommentDTO> Comments { get; set; } = new List<TaskCommentDTO>();
        public bool Finished { get; set; }

    }
}
