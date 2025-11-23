using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseSub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailMetadataPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EmailMetadata_EmailAccountId_IsProcessed",
                table: "EmailMetadata",
                columns: new[] { "EmailAccountId", "IsProcessed" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailMetadata_IsProcessed_ProcessedAt",
                table: "EmailMetadata",
                columns: new[] { "IsProcessed", "ProcessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailMetadata_EmailAccountId_IsProcessed",
                table: "EmailMetadata");

            migrationBuilder.DropIndex(
                name: "IX_EmailMetadata_IsProcessed_ProcessedAt",
                table: "EmailMetadata");
        }
    }
}
