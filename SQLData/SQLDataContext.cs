using divitiae_api.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace divitiae_api.SQLData
{
    public class SQLDataContext : DbContext
    {
        public SQLDataContext(DbContextOptions<SQLDataContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } 
        public DbSet<WorkEnvironment> WorkEnvironments { get; set; } 
        public DbSet<UserToWorkEnvRole> UserToWorkEnvRoles { get; set; }
        //public DbSet<UserToWorkspaceAccess> UserToWorkspaceAccess { get; set; }
        public DbSet<Workspace> Workspaces { get; set; } 
        public DbSet<WsAppsRelation> WsAppsRelations { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserToWorkEnvRole>()
                .HasKey(uwe => new { uwe.UserId, uwe.WorkEnvironmentId });

            modelBuilder.Entity<UserToWorkEnvRole>()
                .HasOne(uwe => uwe.User)
                .WithMany(u => u.UserToWorkEnvRole)
                .HasForeignKey(uwe => uwe.UserId);

            modelBuilder.Entity<UserToWorkEnvRole>()
                .HasOne(uwe => uwe.WorkEnvironment)
                .WithMany(w => w.UserToWorkEnvRole)
                .HasForeignKey(uwe => uwe.WorkEnvironmentId);

            //modelBuilder.Entity<UserToWorkspaceAccess>()
            //    .HasKey(uwe => new { uwe.UserId, uwe.WorkspaceId });

            //modelBuilder.Entity<UserToWorkspaceAccess>()
            //    .HasOne(uwe => uwe.User)
            //    .WithMany(u => u.UserToWorkspaceAccess)
            //    .HasForeignKey(uwe => uwe.UserId);

            //modelBuilder.Entity<UserToWorkspaceAccess>()
            //    .HasOne(uwe => uwe.Workspace)
            //    .WithMany(w => w.UserToWorkspaceAccess)
            //    .HasForeignKey(uwe => uwe.WorkspaceId);


            base.OnModelCreating(modelBuilder);
        }
    }
}