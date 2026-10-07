using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessRequestHub.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_versions",
                columns: table => new
                {
                    code = table.Column<string>(type: "TEXT", nullable: false),
                    effective_from = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_versions", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", nullable: false),
                    manager_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("CK_users_manager", "manager_id IS NULL OR manager_id <> id");
                    table.CheckConstraint("CK_users_role", "role IN ('Employee', 'Auditor')");
                    table.ForeignKey(
                        name: "fk_users_users_manager_id",
                        column: x => x.manager_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    system_owner_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_applications_users_system_owner_id",
                        column: x => x.system_owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "access_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    client_request_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    requester_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    environment = table.Column<string>(type: "TEXT", nullable: false),
                    access_level = table.Column<string>(type: "TEXT", nullable: false),
                    justification = table.Column<string>(type: "TEXT", nullable: false),
                    is_high_risk = table.Column<bool>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    policy_version_code = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_requests", x => x.id);
                    table.CheckConstraint("CK_access_requests_access_level", "access_level IN ('Read', 'Write', 'Admin')");
                    table.CheckConstraint("CK_access_requests_environment", "environment IN ('NonProduction', 'Production')");
                    table.CheckConstraint("CK_access_requests_high_risk_status", "is_high_risk = 1 OR status <> 'PendingSystemOwner'");
                    table.CheckConstraint("CK_access_requests_is_high_risk", "is_high_risk = (environment='Production' OR access_level='Admin')");
                    table.CheckConstraint("CK_access_requests_justification", "length(trim(justification)) > 0");
                    table.CheckConstraint("CK_access_requests_status", "status IN ('PendingManager', 'PendingSystemOwner', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_access_requests_version", "version >= 1");
                    table.ForeignKey(
                        name: "fk_access_requests_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_access_requests_policy_versions_policy_version_code",
                        column: x => x.policy_version_code,
                        principalTable: "policy_versions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_access_requests_users_requester_id",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    access_request_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    event_type = table.Column<string>(type: "TEXT", nullable: false),
                    from_status = table.Column<string>(type: "TEXT", nullable: true),
                    to_status = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    version_after = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                    table.CheckConstraint("CK_audit_events_reason", "(event_type NOT IN ('ManagerRejected', 'OwnerRejected')) OR (reason IS NOT NULL AND length(trim(reason)) > 0)");
                    table.ForeignKey(
                        name: "fk_audit_events_access_requests_access_request_id",
                        column: x => x.access_request_id,
                        principalTable: "access_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_requests_application_id",
                table: "access_requests",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_requests_policy_version_code",
                table: "access_requests",
                column: "policy_version_code");

            migrationBuilder.CreateIndex(
                name: "ix_access_requests_requester_id_client_request_id",
                table: "access_requests",
                columns: new[] { "requester_id", "client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_name",
                table: "applications",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_system_owner_id",
                table: "applications",
                column: "system_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_access_request_id",
                table: "audit_events",
                column: "access_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_actor_user_id",
                table: "audit_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_manager_id",
                table: "users",
                column: "manager_id");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_audit_events_prevent_update
                BEFORE UPDATE ON audit_events
                BEGIN
                    SELECT RAISE(ABORT, 'Updates are not allowed on audit_events.');
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_audit_events_prevent_delete
                BEFORE DELETE ON audit_events
                BEGIN
                    SELECT RAISE(ABORT, 'Deletes are not allowed on audit_events.');
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "access_requests");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "policy_versions");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
