using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure;

/// <summary>
/// Access boundary for the existing Hızlı Okuma database.
/// The first integration phase intentionally does not run migrations: the
/// existing schema is treated as production data and remains the source of truth.
/// </summary>
public sealed class SpeedReadingDbContext(DbContextOptions<SpeedReadingDbContext> options) : DbContext(options);
