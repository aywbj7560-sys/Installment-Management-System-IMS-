using System.Text.RegularExpressions;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace IMS.Tests;

public class SchemaMappingTests
{
    private static ImsDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ImsDbContext>()
            .UseNpgsql("Host=unused;Database=ims_db;Username=unused")
            .Options);

    [Fact]
    public void AllTablesAndColumnsMatchTheSourceSchema()
    {
        using var context = CreateContext();
        var schema = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schema", "schema.sql"));
        var tables = Regex.Matches(schema, @"CREATE TABLE (\w+) \((.*?)\r?\n\);", RegexOptions.Singleline);
        Assert.Equal(11, tables.Count);
        Assert.Equal(tables.Count, context.Model.GetEntityTypes().Count());

        foreach (Match table in tables)
        {
            var tableName = table.Groups[1].Value;
            var entity = Assert.Single(context.Model.GetEntityTypes(), e => e.GetTableName() == tableName);
            Assert.Equal("public", entity.GetSchema());
            var store = StoreObjectIdentifier.Table(tableName, "public");
            var columns = Regex.Matches(table.Groups[2].Value,
                @"^\s+(\w+) (BIGINT|VARCHAR\(\d+\)|TEXT|BOOLEAN|TIMESTAMPTZ|NUMERIC\(15,2\)|INTEGER|DATE) ([^\r\n]+)",
                RegexOptions.Multiline);
            Assert.Equal(columns.Count, entity.GetProperties().Count());
            Assert.DoesNotContain(entity.GetProperties(), p => p.IsShadowProperty());

            foreach (Match column in columns)
            {
                var name = column.Groups[1].Value;
                var sqlType = column.Groups[2].Value;
                var definition = column.Groups[3].Value;
                var property = Assert.Single(entity.GetProperties(), p => p.GetColumnName(store) == name);
                Assert.Equal(!definition.Contains("NOT NULL") && !definition.Contains("PRIMARY KEY"), property.IsNullable);
                Assert.Equal(sqlType == "TIMESTAMPTZ" ? "timestamp with time zone" : sqlType.ToLowerInvariant(),
                    property.GetColumnType());

                if (sqlType.StartsWith("VARCHAR"))
                    Assert.Equal(int.Parse(Regex.Match(sqlType, @"\d+").Value), property.GetMaxLength());
                if (sqlType == "NUMERIC(15,2)")
                {
                    Assert.Equal(typeof(decimal), Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);
                    Assert.Equal(15, property.GetPrecision());
                    Assert.Equal(2, property.GetScale());
                }
                if (sqlType == "TIMESTAMPTZ")
                    Assert.Equal(typeof(DateTime), property.ClrType);
                if (sqlType == "DATE")
                    Assert.Equal(typeof(DateOnly), property.ClrType);
                if (definition.Contains("PRIMARY KEY"))
                {
                    Assert.Equal(property, Assert.Single(entity.FindPrimaryKey()!.Properties));
                    Assert.Equal(ValueGenerated.OnAdd, property.ValueGenerated);
                    Assert.Equal(NpgsqlValueGenerationStrategy.IdentityByDefaultColumn, property.GetValueGenerationStrategy());
                }
                if (definition.Contains("UNIQUE"))
                    Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.SequenceEqual(new[] { property }));

                var foreignKey = Regex.Match(definition, @"REFERENCES (\w+)\((\w+)\) ON DELETE (\w+)");
                if (foreignKey.Success)
                {
                    var mapped = Assert.Single(entity.GetForeignKeys(), fk => fk.Properties.Contains(property));
                    Assert.Equal(foreignKey.Groups[1].Value, mapped.PrincipalEntityType.GetTableName());
                    Assert.Equal(foreignKey.Groups[2].Value, Assert.Single(mapped.PrincipalKey.Properties).GetColumnName());
                    Assert.Equal(foreignKey.Groups[3].Value == "CASCADE" ? DeleteBehavior.Cascade : DeleteBehavior.Restrict,
                        mapped.DeleteBehavior);
                    Assert.Equal(tableName + "_" + name + "_fkey", mapped.GetConstraintName());
                }
            }

            foreach (Match unique in Regex.Matches(table.Groups[2].Value, @"CONSTRAINT (\w+) UNIQUE \(([^)]+)\)"))
            {
                var index = Assert.Single(entity.GetIndexes(), i => i.GetDatabaseName() == unique.Groups[1].Value);
                Assert.True(index.IsUnique);
                Assert.Equal(unique.Groups[2].Value.Split(", ", StringSplitOptions.None),
                    index.Properties.Select(p => p.GetColumnName(store)));
            }
        }

        Assert.Equal(12, context.Model.GetEntityTypes().Sum(e => e.GetForeignKeys().Count()));
    }

    [Theory]
    [InlineData(InstallmentStatus.Pending, "Pending")]
    [InlineData(InstallmentStatus.Paid, "Paid")]
    [InlineData(InstallmentStatus.PartiallyPaid, "Partially Paid")]
    [InlineData(InstallmentStatus.Overdue, "Overdue")]
    [InlineData(InstallmentStatus.Waived, "Waived")]
    public void InstallmentStatusesRoundTripExactly(InstallmentStatus status, string stored)
    {
        using var context = CreateContext();
        var converter = context.Model.FindEntityType(typeof(Installment))!
            .FindProperty(nameof(Installment.Status))!.GetTypeMapping().Converter!;
        Assert.Equal(stored, converter.ConvertToProvider(status));
        Assert.Equal(status, converter.ConvertFromProvider(stored));
    }

    [Fact]
    public void CustomerAndContractStatusesRoundTripExactly()
    {
        using var context = CreateContext();
        AssertStatuses<Customer, CustomerStatus>(context);
        AssertStatuses<Contract, ContractStatus>(context);
    }

    private static void AssertStatuses<TEntity, TStatus>(ImsDbContext context) where TStatus : struct, Enum
    {
        var converter = context.Model.FindEntityType(typeof(TEntity))!.FindProperty("Status")!.GetTypeMapping().Converter!;
        foreach (var status in Enum.GetValues<TStatus>())
        {
            Assert.Equal(status.ToString(), converter.ConvertToProvider(status));
            Assert.Equal(status, converter.ConvertFromProvider(status.ToString()));
        }
    }
}
