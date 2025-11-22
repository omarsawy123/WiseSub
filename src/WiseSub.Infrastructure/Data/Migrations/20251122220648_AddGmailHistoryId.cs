using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseSub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailHistoryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GmailHistoryId",
                table: "EmailAccounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GmailHistoryId",
                table: "EmailAccounts");
        }
    }
}
