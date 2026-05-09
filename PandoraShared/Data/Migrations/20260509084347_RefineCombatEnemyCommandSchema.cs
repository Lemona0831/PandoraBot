using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandoraShared.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefineCombatEnemyCommandSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "public",
                table: "enemy_drops",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "rarity",
                schema: "public",
                table: "enemy_drops",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tag",
                schema: "public",
                table: "enemy_drops",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "weight",
                schema: "public",
                table: "enemy_drops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "allow_duplicate",
                schema: "public",
                table: "enemy_drop_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "created_by_discord_id",
                schema: "public",
                table: "combat_participants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "normalized_display_name",
                schema: "public",
                table: "combat_participants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "public",
                table: "combat_participants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "target_participant_id",
                schema: "public",
                table: "combat_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "admin_display_name",
                schema: "public",
                table: "admin_logs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "target_display_name",
                schema: "public",
                table: "admin_logs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_enemy_drops_enemy_id_is_active",
                schema: "public",
                table: "enemy_drops",
                columns: new[] { "enemy_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_enemy_drops_enemy_id_item_name",
                schema: "public",
                table: "enemy_drops",
                columns: new[] { "enemy_id", "item_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_combat_participants_combat_session_id_normalized_display_na~",
                schema: "public",
                table: "combat_participants",
                columns: new[] { "combat_session_id", "normalized_display_name" });

            migrationBuilder.CreateIndex(
                name: "ix_combat_logs_target_participant_id",
                schema: "public",
                table: "combat_logs",
                column: "target_participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_admin_logs_action_type",
                schema: "public",
                table: "admin_logs",
                column: "action_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_enemy_drops_enemy_id_is_active",
                schema: "public",
                table: "enemy_drops");

            migrationBuilder.DropIndex(
                name: "ix_enemy_drops_enemy_id_item_name",
                schema: "public",
                table: "enemy_drops");

            migrationBuilder.DropIndex(
                name: "ix_combat_participants_combat_session_id_normalized_display_na~",
                schema: "public",
                table: "combat_participants");

            migrationBuilder.DropIndex(
                name: "ix_combat_logs_target_participant_id",
                schema: "public",
                table: "combat_logs");

            migrationBuilder.DropIndex(
                name: "ix_admin_logs_action_type",
                schema: "public",
                table: "admin_logs");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "public",
                table: "enemy_drops");

            migrationBuilder.DropColumn(
                name: "rarity",
                schema: "public",
                table: "enemy_drops");

            migrationBuilder.DropColumn(
                name: "tag",
                schema: "public",
                table: "enemy_drops");

            migrationBuilder.DropColumn(
                name: "weight",
                schema: "public",
                table: "enemy_drops");

            migrationBuilder.DropColumn(
                name: "allow_duplicate",
                schema: "public",
                table: "enemy_drop_settings");

            migrationBuilder.DropColumn(
                name: "created_by_discord_id",
                schema: "public",
                table: "combat_participants");

            migrationBuilder.DropColumn(
                name: "normalized_display_name",
                schema: "public",
                table: "combat_participants");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "public",
                table: "combat_participants");

            migrationBuilder.DropColumn(
                name: "target_participant_id",
                schema: "public",
                table: "combat_logs");

            migrationBuilder.DropColumn(
                name: "admin_display_name",
                schema: "public",
                table: "admin_logs");

            migrationBuilder.DropColumn(
                name: "target_display_name",
                schema: "public",
                table: "admin_logs");
        }
    }
}
