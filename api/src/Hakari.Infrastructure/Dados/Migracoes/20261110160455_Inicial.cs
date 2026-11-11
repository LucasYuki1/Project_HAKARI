using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hakari.Infrastructure.Dados.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "postes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Endereco = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Bairro = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    UltimaComunicacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Isolado = table.Column<bool>(type: "boolean", nullable: false),
                    UltimoBrilho = table.Column<int>(type: "integer", nullable: true),
                    UltimoRssi = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_postes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    perfil = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TermoAceitoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Matricula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "alertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PosteId = table.Column<int>(type: "integer", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Severidade = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detalhe = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AbertoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NormalizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alertas_postes_PosteId",
                        column: x => x.PosteId,
                        principalTable: "postes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "leituras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PosteId = table.Column<int>(type: "integer", nullable: false),
                    RecebidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Ldr = table.Column<int>(type: "integer", nullable: false),
                    LdrReferencia = table.Column<int>(type: "integer", nullable: false),
                    DistanciaCm = table.Column<int>(type: "integer", nullable: false),
                    Presenca = table.Column<bool>(type: "boolean", nullable: false),
                    Brilho = table.Column<int>(type: "integer", nullable: false),
                    Rssi = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leituras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leituras_postes_PosteId",
                        column: x => x.PosteId,
                        principalTable: "postes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ocorrencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PosteId = table.Column<int>(type: "integer", nullable: false),
                    Origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Severidade = table.Column<int>(type: "integer", nullable: false),
                    Categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AbertaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Solucao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AutorId = table.Column<int>(type: "integer", nullable: true),
                    ResponsavelId = table.Column<int>(type: "integer", nullable: true),
                    AlertaId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ocorrencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ocorrencias_alertas_AlertaId",
                        column: x => x.AlertaId,
                        principalTable: "alertas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ocorrencias_postes_PosteId",
                        column: x => x.PosteId,
                        principalTable: "postes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ocorrencias_usuarios_AutorId",
                        column: x => x.AutorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ocorrencias_usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "apoios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OcorrenciaId = table.Column<int>(type: "integer", nullable: false),
                    CidadaoId = table.Column<int>(type: "integer", nullable: false),
                    ApoiadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apoios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_apoios_ocorrencias_OcorrenciaId",
                        column: x => x.OcorrenciaId,
                        principalTable: "ocorrencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_apoios_usuarios_CidadaoId",
                        column: x => x.CidadaoId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "historico_ocorrencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OcorrenciaId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    RegistradoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_ocorrencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historico_ocorrencias_ocorrencias_OcorrenciaId",
                        column: x => x.OcorrenciaId,
                        principalTable: "ocorrencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alertas_PosteId_Estado",
                table: "alertas",
                columns: new[] { "PosteId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_apoios_CidadaoId",
                table: "apoios",
                column: "CidadaoId");

            migrationBuilder.CreateIndex(
                name: "IX_apoios_OcorrenciaId_CidadaoId",
                table: "apoios",
                columns: new[] { "OcorrenciaId", "CidadaoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historico_ocorrencias_OcorrenciaId",
                table: "historico_ocorrencias",
                column: "OcorrenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_leituras_PosteId_RecebidaEm",
                table: "leituras",
                columns: new[] { "PosteId", "RecebidaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_ocorrencias_AlertaId",
                table: "ocorrencias",
                column: "AlertaId");

            migrationBuilder.CreateIndex(
                name: "IX_ocorrencias_AutorId",
                table: "ocorrencias",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_ocorrencias_PosteId_Status",
                table: "ocorrencias",
                columns: new[] { "PosteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ocorrencias_ResponsavelId",
                table: "ocorrencias",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_postes_Bairro",
                table: "postes",
                column: "Bairro");

            migrationBuilder.CreateIndex(
                name: "IX_postes_Codigo",
                table: "postes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apoios");

            migrationBuilder.DropTable(
                name: "historico_ocorrencias");

            migrationBuilder.DropTable(
                name: "leituras");

            migrationBuilder.DropTable(
                name: "ocorrencias");

            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "postes");
        }
    }
}
