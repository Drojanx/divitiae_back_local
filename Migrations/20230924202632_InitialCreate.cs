using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace divitiae_api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleUID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    PasswordSalt = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkEnvironments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkEnvironments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserToWorkEnvRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserToWorkEnvRoles", x => new { x.UserId, x.WorkEnvironmentId });
                    table.ForeignKey(
                        name: "FK_UserToWorkEnvRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserToWorkEnvRoles_WorkEnvironments_WorkEnvironmentId",
                        column: x => x.WorkEnvironmentId,
                        principalTable: "WorkEnvironments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkenvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspaces_WorkEnvironments_WorkenvironmentId",
                        column: x => x.WorkenvironmentId,
                        principalTable: "WorkEnvironments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkspace",
                columns: table => new
                {
                    UsersId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspacesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkspace", x => new { x.UsersId, x.WorkspacesId });
                    table.ForeignKey(
                        name: "FK_UserWorkspace_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWorkspace_Workspaces_WorkspacesId",
                        column: x => x.WorkspacesId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WsAppsRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WsAppsRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WsAppsRelations_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserToWorkEnvRoles_WorkEnvironmentId",
                table: "UserToWorkEnvRoles",
                column: "WorkEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkspace_WorkspacesId",
                table: "UserWorkspace",
                column: "WorkspacesId");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_WorkenvironmentId",
                table: "Workspaces",
                column: "WorkenvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WsAppsRelations_WorkspaceId",
                table: "WsAppsRelations",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserToWorkEnvRoles");

            migrationBuilder.DropTable(
                name: "UserWorkspace");

            migrationBuilder.DropTable(
                name: "WsAppsRelations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Workspaces");

            migrationBuilder.DropTable(
                name: "WorkEnvironments");
        }
    }
}
