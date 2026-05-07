using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandoraShared.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPandoraSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "admin_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_discord_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    before_value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    after_value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    message = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "characters",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    discord_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_sheet_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_sheet_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_document_title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    imported_character_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    normalized_display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    strength = table.Column<int>(type: "integer", nullable: false),
                    dexterity = table.Column<int>(type: "integer", nullable: false),
                    constitution = table.Column<int>(type: "integer", nullable: false),
                    intelligence = table.Column<int>(type: "integer", nullable: false),
                    wisdom = table.Column<int>(type: "integer", nullable: false),
                    charisma = table.Column<int>(type: "integer", nullable: false),
                    current_hp = table.Column<int>(type: "integer", nullable: false),
                    max_hp = table.Column<int>(type: "integer", nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_characters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "combat_sessions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    guild_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    channel_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by_discord_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    memo = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_combat_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enemies",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enemy_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    strength = table.Column<int>(type: "integer", nullable: false),
                    dexterity = table.Column<int>(type: "integer", nullable: false),
                    constitution = table.Column<int>(type: "integer", nullable: false),
                    intelligence = table.Column<int>(type: "integer", nullable: false),
                    wisdom = table.Column<int>(type: "integer", nullable: false),
                    charisma = table.Column<int>(type: "integer", nullable: false),
                    max_hp = table.Column<int>(type: "integer", nullable: false),
                    encounter_tag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    memo = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enemies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "character_selections",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    discord_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_selections", x => x.id);
                    table.ForeignKey(
                        name: "fk_character_selections_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "public",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roll_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    character_display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    stat_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dice1 = table.Column<int>(type: "integer", nullable: false),
                    dice2 = table.Column<int>(type: "integer", nullable: false),
                    modifier = table.Column<int>(type: "integer", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    result_tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roll_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_roll_logs_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "public",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "combat_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    combat_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_discord_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    before_value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    after_value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    message = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_combat_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_combat_logs_combat_sessions_combat_session_id",
                        column: x => x.combat_session_id,
                        principalSchema: "public",
                        principalTable: "combat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "combat_participants",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    combat_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    current_hp = table.Column<int>(type: "integer", nullable: false),
                    max_hp = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    memo = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_combat_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_combat_participants_combat_sessions_combat_session_id",
                        column: x => x.combat_session_id,
                        principalSchema: "public",
                        principalTable: "combat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enemy_drop_settings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enemy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    drop_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    drop_slots = table.Column<int>(type: "integer", nullable: false),
                    memo = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enemy_drop_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_enemy_drop_settings_enemies_enemy_id",
                        column: x => x.enemy_id,
                        principalSchema: "public",
                        principalTable: "enemies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enemy_drops",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enemy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    probability = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    min_quantity = table.Column<int>(type: "integer", nullable: false),
                    max_quantity = table.Column<int>(type: "integer", nullable: false),
                    memo = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enemy_drops", x => x.id);
                    table.ForeignKey(
                        name: "fk_enemy_drops_enemies_enemy_id",
                        column: x => x.enemy_id,
                        principalSchema: "public",
                        principalTable: "enemies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_logs_created_at",
                schema: "public",
                table: "admin_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_admin_logs_target_type_target_id",
                schema: "public",
                table: "admin_logs",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_character_selections_character_id",
                schema: "public",
                table: "character_selections",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "ix_character_selections_discord_user_id",
                schema: "public",
                table: "character_selections",
                column: "discord_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_characters_discord_user_id_source_sheet_id",
                schema: "public",
                table: "characters",
                columns: new[] { "discord_user_id", "source_sheet_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_characters_normalized_display_name",
                schema: "public",
                table: "characters",
                column: "normalized_display_name");

            migrationBuilder.CreateIndex(
                name: "ix_combat_logs_combat_session_id",
                schema: "public",
                table: "combat_logs",
                column: "combat_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_combat_logs_created_at",
                schema: "public",
                table: "combat_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_combat_participants_combat_session_id",
                schema: "public",
                table: "combat_participants",
                column: "combat_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_combat_participants_combat_session_id_display_name",
                schema: "public",
                table: "combat_participants",
                columns: new[] { "combat_session_id", "display_name" });

            migrationBuilder.CreateIndex(
                name: "ix_combat_sessions_created_at",
                schema: "public",
                table: "combat_sessions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_combat_sessions_guild_id_channel_id_status",
                schema: "public",
                table: "combat_sessions",
                columns: new[] { "guild_id", "channel_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_enemies_enemy_code",
                schema: "public",
                table: "enemies",
                column: "enemy_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enemies_normalized_name",
                schema: "public",
                table: "enemies",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "ix_enemy_drop_settings_enemy_id",
                schema: "public",
                table: "enemy_drop_settings",
                column: "enemy_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enemy_drops_enemy_id",
                schema: "public",
                table: "enemy_drops",
                column: "enemy_id");

            migrationBuilder.CreateIndex(
                name: "ix_roll_logs_character_id",
                schema: "public",
                table: "roll_logs",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "ix_roll_logs_created_at",
                schema: "public",
                table: "roll_logs",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "character_selections",
                schema: "public");

            migrationBuilder.DropTable(
                name: "combat_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "combat_participants",
                schema: "public");

            migrationBuilder.DropTable(
                name: "enemy_drop_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "enemy_drops",
                schema: "public");

            migrationBuilder.DropTable(
                name: "roll_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "combat_sessions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "enemies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "characters",
                schema: "public");
        }
    }
}
