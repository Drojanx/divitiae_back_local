using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace divitiae_api.Migrations
{
    /// <inheritdoc />
    public partial class TasksAndEnvOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOwner",
                table: "UserToWorkEnvRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOwner",
                table: "UserToWorkEnvRoles");
        }
    }
}
