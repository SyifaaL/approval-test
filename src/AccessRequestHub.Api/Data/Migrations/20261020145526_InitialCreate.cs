using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessRequestHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PolicyVersions",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyVersions", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ManagerId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsAuditor = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientRequestId = table.Column<string>(type: "TEXT", nullable: false),
                    RequesterId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AccessLevel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Justification = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PolicyVersion = table.Column<string>(type: "TEXT", nullable: false),
                    IsHighRisk = table.Column<bool>(type: "INTEGER", nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRequests", x => x.Id);
                    table.CheckConstraint("CK_AccessRequests_AccessLevel", "AccessLevel IN ('Read', 'Admin')");
                    table.CheckConstraint("CK_AccessRequests_Environment", "Environment IN ('NonProduction', 'Production')");
                    table.CheckConstraint("CK_AccessRequests_Justification_NotEmpty", "length(trim(Justification)) > 0");
                    table.CheckConstraint("CK_AccessRequests_RejectionReason_RequiredWhenRejected", "Status <> 'Rejected' OR (RejectionReason IS NOT NULL AND length(trim(RejectionReason)) > 0)");
                    table.CheckConstraint("CK_AccessRequests_Status", "Status IN ('PendingManager', 'PendingSystemOwner', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_AccessRequests_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessRequests_PolicyVersions_PolicyVersion",
                        column: x => x.PolicyVersion,
                        principalTable: "PolicyVersions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessRequests_Users_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    RequestVersionAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvents_AccessRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "AccessRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_ApplicationId",
                table: "AccessRequests",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_PolicyVersion",
                table: "AccessRequests",
                column: "PolicyVersion");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_RequesterId_ClientRequestId",
                table: "AccessRequests",
                columns: new[] { "RequesterId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name",
                table: "Applications",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_OwnerUserId",
                table: "Applications",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorUserId",
                table: "AuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_RequestId_RequestVersionAfter",
                table: "AuditEvents",
                columns: new[] { "RequestId", "RequestVersionAfter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerId",
                table: "Users",
                column: "ManagerId");

            migrationBuilder.Sql(
                """
                CREATE TRIGGER TR_AuditEvents_NoUpdate
                BEFORE UPDATE ON AuditEvents
                BEGIN
                    SELECT RAISE(ABORT, 'AuditEvents is append-only: UPDATE is not allowed');
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER TR_AuditEvents_NoDelete
                BEFORE DELETE ON AuditEvents
                BEGIN
                    SELECT RAISE(ABORT, 'AuditEvents is append-only: DELETE is not allowed');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_AuditEvents_NoUpdate;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_AuditEvents_NoDelete;");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "AccessRequests");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "PolicyVersions");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
