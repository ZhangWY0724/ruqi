using Microsoft.Data.Sqlite;
using Ruqi.Core.Models;

namespace Ruqi.Core.Data;

public sealed class RuqiDatabase
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public RuqiDatabase(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        await pragma.ExecuteNonQueryAsync(ct);
        return conn;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var conn = await OpenConnectionAsync(ct);

            await using (var wal = conn.CreateCommand())
            {
                wal.CommandText = "PRAGMA journal_mode = WAL;";
                await wal.ExecuteNonQueryAsync(ct);
            }

            await using (var ddl = conn.CreateCommand())
            {
                ddl.CommandText = """
                    CREATE TABLE IF NOT EXISTS CatalogItems (
                        Id TEXT PRIMARY KEY,
                        IsBuiltIn INTEGER NOT NULL DEFAULT 0,
                        AppName TEXT NOT NULL,
                        Category TEXT NOT NULL,
                        IconKey TEXT NOT NULL DEFAULT '',
                        BrandColor TEXT NOT NULL DEFAULT '#5B6CF9',
                        TierName TEXT NULL,
                        DefaultCycleType INTEGER NOT NULL DEFAULT 0,
                        DefaultInterval INTEGER NOT NULL DEFAULT 1,
                        ReferencePriceCents INTEGER NULL,
                        SearchKeywords TEXT NOT NULL DEFAULT ''
                    );

                    CREATE TABLE IF NOT EXISTS AccountProfiles (
                        Id TEXT PRIMARY KEY,
                        AppName TEXT NOT NULL,
                        Nickname TEXT NULL,
                        Identifier TEXT NULL
                    );

                    CREATE INDEX IF NOT EXISTS IX_AccountProfiles_AppName ON AccountProfiles(AppName);

                    CREATE TABLE IF NOT EXISTS PaymentChannels (
                        Id TEXT PRIMARY KEY,
                        Name TEXT NOT NULL UNIQUE,
                        IsBuiltIn INTEGER NOT NULL DEFAULT 0,
                        SortOrder INTEGER NOT NULL DEFAULT 50,
                        IsArchived INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Subscriptions (
                        Id TEXT PRIMARY KEY,
                        AppName TEXT NOT NULL,
                        Category TEXT NOT NULL,
                        IconKey TEXT NOT NULL DEFAULT '',
                        BrandColor TEXT NOT NULL DEFAULT '#5B6CF9',
                        TierName TEXT NULL,
                        AmountCents INTEGER NOT NULL,
                        Currency TEXT NOT NULL DEFAULT 'CNY',
                        CycleType INTEGER NOT NULL,
                        CycleInterval INTEGER NOT NULL DEFAULT 1,
                        AnchorDay INTEGER NOT NULL,
                        AnchorMonth INTEGER NOT NULL,
                        NextBillingDate TEXT NOT NULL,
                        ExpiryDate TEXT NULL,
                        AccountId TEXT NULL,
                        AccountDisplayCache TEXT NULL,
                        PaymentChannelName TEXT NULL,
                        Status INTEGER NOT NULL DEFAULT 0,
                        RemindBeforeEnabled INTEGER NOT NULL DEFAULT 1,
                        RemindBeforeDays INTEGER NOT NULL DEFAULT 3,
                        RemindOnDayEnabled INTEGER NOT NULL DEFAULT 1,
                        RemindTime TEXT NOT NULL DEFAULT '09:00',
                        Note TEXT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS BillingOccurrences (
                        Id TEXT PRIMARY KEY,
                        SubscriptionId TEXT NOT NULL,
                        ScheduledDate TEXT NOT NULL,
                        ExpectedAmountCents INTEGER NOT NULL,
                        Kind INTEGER NOT NULL,
                        Status INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions(Id) ON DELETE CASCADE
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_BillingOccurrences_Unique
                        ON BillingOccurrences(SubscriptionId, ScheduledDate, Kind);

                    CREATE TABLE IF NOT EXISTS BillingRecords (
                        Id TEXT PRIMARY KEY,
                        OccurrenceId TEXT NULL,
                        SubscriptionId TEXT NULL,
                        PaidAmountCents INTEGER NOT NULL,
                        Currency TEXT NOT NULL DEFAULT 'CNY',
                        PaidDate TEXT NOT NULL,
                        AppNameSnapshot TEXT NOT NULL,
                        CategorySnapshot TEXT NOT NULL,
                        IconKeySnapshot TEXT NOT NULL DEFAULT '',
                        BrandColorSnapshot TEXT NOT NULL DEFAULT '#5B6CF9',
                        TierNameSnapshot TEXT NULL,
                        AccountDisplaySnapshot TEXT NULL,
                        PaymentChannelSnapshot TEXT NULL,
                        Note TEXT NULL,
                        CreatedAt TEXT NOT NULL,
                        FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions(Id) ON DELETE SET NULL
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_BillingRecords_OccurrenceId
                        ON BillingRecords(OccurrenceId)
                        WHERE OccurrenceId IS NOT NULL;

                    CREATE INDEX IF NOT EXISTS IX_BillingRecords_PaidDate ON BillingRecords(PaidDate);

                    CREATE TABLE IF NOT EXISTS AppSettings (
                        Key TEXT PRIMARY KEY,
                        Value TEXT NOT NULL
                    );
                    """;
                await ddl.ExecuteNonQueryAsync(ct);
            }

            await SeedDefaultChannelsAsync(conn, ct);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task SeedDefaultChannelsAsync(SqliteConnection conn, CancellationToken ct)
    {
        foreach (var ch in BuiltInSeeds.GetDefaultPaymentChannels())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO PaymentChannels (Id, Name, IsBuiltIn, SortOrder, IsArchived)
                VALUES ($id, $name, 1, $sortOrder, 0);
                """;
            cmd.Parameters.AddWithValue("$id", ch.Id);
            cmd.Parameters.AddWithValue("$name", ch.Name);
            cmd.Parameters.AddWithValue("$sortOrder", ch.SortOrder);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public static string ToIsoDate(DateOnly date) => date.ToString("yyyy-MM-dd");
    public static DateOnly FromIsoDate(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd");
    public static string ToTimeText(TimeOnly time) => time.ToString("HH:mm");
    public static TimeOnly FromTimeText(string text) => TimeOnly.ParseExact(text, "HH:mm");
    public static string ToIsoTimestamp(DateTimeOffset dto) => dto.ToString("O");
    public static DateTimeOffset FromIsoTimestamp(string text) => DateTimeOffset.Parse(text);
}
