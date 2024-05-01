using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventHubSolution.BackendServer.Migrations
{
    /// <inheritdoc />
    public partial class AddEventField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "Events",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrash",
                table: "Events",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IsTrash",
                table: "Events");
        }
    }
}
