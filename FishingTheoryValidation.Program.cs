using System;
using MySql.Data.MySqlClient;
using System.Data;

namespace FishingTheoryValidation
{
    class Program
    {
        static void Main(string[] args)
        {
            string connectionString = "Server=127.0.0.1;Database=sst;User=root;Password=;";
            
            Console.WriteLine(new string('=', 80));
            Console.WriteLine("🎣 Fishing Theory Validation - Quick Analysis");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine();
            
            try
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                Console.WriteLine("✓ Connected to MySQL");
                Console.WriteLine();
                
                // Step 1: Check data availability
                Console.WriteLine("Step 1: Checking data availability...");
                using (var cmd = new MySqlCommand(@"
                    SELECT COUNT(*) as total, 
                           MIN(alertDate) as min_date, 
                           MAX(alertDate) as max_date
                    FROM alertlist 
                    WHERE alertDate BETWEEN '2025-10-01' AND '2026-01-31'", connection))
               {
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        Console.WriteLine($"  Alertlist records: {reader["total"]}");
                        Console.WriteLine($"  Date range: {reader["min_date"]} to {reader["max_date"]}");
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine("Step 2: Finding success cases (simplified query)...");
                
                // Simplified success case query - just count first
                using (var cmd = new MySqlCommand(@"
                    SELECT COUNT(DISTINCT a.StockID, a.alertDate) as count
                    FROM alertlist a
                    WHERE a.alertDate BETWEEN '2025-10-01' AND '2025-10-31'
                      AND a.maxPLVR BETWEEN 10 AND 50", connection))
                {
                    var count = cmd.ExecuteScalar();
                    Console.WriteLine($"  Potential candidates in Oct 2025: {count}");
                }
                
                Console.WriteLine();
                Console.WriteLine("Step 3: Sample alertlist data...");
                using (var cmd = new MySqlCommand(@"
                    SELECT StockID, alertDate, maxPLVR, pLVRatePosCnt, p5VRatePosCnt,
                           panVol5CntPos, panVol5CntNeg
                    FROM alertlist
                    WHERE alertDate BETWEEN '2025-10-01' AND '2025-10-10'
                    ORDER BY maxPLVR DESC
                    LIMIT 10", connection))
                {
                    using var reader = cmd.ExecuteReader();
                    Console.WriteLine("  StockID | Date       | maxPLVR | BuyCnt | Pos/Neg");
                    Console.WriteLine("  " + new string('-', 60));
                    while (reader.Read())
                    {
                        Console.WriteLine($"  {reader["StockID"],-8} | {reader["alertDate"]:yyyy-MM-dd} | " +
                            $"{reader["maxPLVR"],7} | {reader["pLVRatePosCnt"],6} | " +
                            $"{reader["panVol5CntPos"]}/{reader["panVol5CntNeg"]}");
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine(new string('=', 80));
                Console.WriteLine("✓ Quick analysis completed");
                Console.WriteLine();
                Console.WriteLine("Note: Full analysis may take 1-2 minutes due to complex JOINs.");
                Console.WriteLine("Consider running the full Python script in background.");
                Console.WriteLine(new string('=', 80));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
