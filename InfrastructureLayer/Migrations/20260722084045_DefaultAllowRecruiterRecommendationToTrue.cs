using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <summary>
    /// Đổi default AllowRecruiterRecommendation từ false sang true (candidate mặc định bật consent
    /// đề xuất hồ sơ cho HR) + backfill toàn bộ dòng CandidateProfiles đã có sẵn về true, vì AlterColumn
    /// chỉ áp default cho dòng insert mới, không tự cập nhật dữ liệu cũ.
    /// </summary>
    public partial class DefaultAllowRecruiterRecommendationToTrue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "AllowRecruiterRecommendation",
                table: "CandidateProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.Sql(@"UPDATE ""CandidateProfiles"" SET ""AllowRecruiterRecommendation"" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "AllowRecruiterRecommendation",
                table: "CandidateProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            // Không rollback dữ liệu đã backfill về false — Down chỉ đảo schema default, không đảo data.
        }
    }
}
