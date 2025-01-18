using System;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace BmiUtil
{
    class Program
    {
        private static string _connectionString;

        static void Main(string[] args)
        {
            Batteries.Init();
            LoadConfiguration();

            EnsureDatabase();

            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            string command = args[0].ToLower();
            if (command == "add")
            {
                HandleAddCommand(args);
            }
            else if (command == "stat")
            {
                HandleStatCommand();
            }
            else
            {
                ShowUsage();
                return;
            }
        }

        private static void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var configuration = builder.Build();
            _connectionString = configuration.GetConnectionString("BmiDatabase");

            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("Connection string is missing or invalid.");
            }
        }

        private static void EnsureDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string createTableQuery = @"
            CREATE TABLE IF NOT EXISTS BmiRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT,
                HeightCm REAL NOT NULL,
                WeightKg REAL NOT NULL,
                Bmi REAL NOT NULL
            );
            ";

            using var command = new SqliteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Expected format:");
            Console.WriteLine("Add an record -> \tBmiUtil.exe add <height_in_cm> <weight_in_kg> [<client_name>]");
            Console.WriteLine("View statistics -> \tBmiUtil.exe stat");
        }

        private static void HandleAddCommand(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("ERROR: not enough parameters for the command 'add'!!!");
                ShowUsage();
                return;
            }

            if (!double.TryParse(args[1], out double height) || !double.TryParse(args[2], out double weight))
            {
                Console.WriteLine("ERROR: height and weight parameters should be numbers!!!");
                ShowUsage();
                return;
            }

            string name = args.Length > 3 ? args[3] : "unknown";

            double bmi = weight / Math.Pow(height / 100, 2);

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                string insertQuery = @"
                    INSERT INTO BmiRecords (Name, HeightCm, WeightKg, Bmi)
                    VALUES (@Name, @HeightCm, @WeightKg, @Bmi);
                ";

                using var command = new SqliteCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@HeightCm", height);
                command.Parameters.AddWithValue("@WeightKg", weight);
                command.Parameters.AddWithValue("@Bmi", bmi);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when adding an record: {ex.Message}");
                return;
            }

            Console.WriteLine($"Record added: {name}, Height: {height} cm, Weight: {weight} kg, BMI: {bmi:F2}");
        }

        private static void HandleStatCommand()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                string statQuery = @"
                    SELECT
                        COUNT(*) AS TotalRecords,
                        SUM(CASE WHEN Bmi < 18.5 THEN 1 ELSE 0 END) AS Underweight,
                        SUM(CASE WHEN Bmi BETWEEN 18.5 AND 24.9 THEN 1 ELSE 0 END) AS Normal,
                        SUM(CASE WHEN Bmi >= 25 THEN 1 ELSE 0 END) AS Overweight,
                        (SELECT Name || ', ' || HeightCm FROM BmiRecords ORDER BY HeightCm DESC LIMIT 1) AS TallestClient,
                        (SELECT Name || ', ' || WeightKg FROM BmiRecords ORDER BY WeightKg DESC LIMIT 1) AS HeaviestClient
                    FROM BmiRecords;
                ";

                using var command = new SqliteCommand(statQuery, connection);
                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    Console.WriteLine($"total records: {reader["TotalRecords"]}");
                    Console.WriteLine($"underweight: {reader["Underweight"]}");
                    Console.WriteLine($"normal: {reader["Normal"]}");
                    Console.WriteLine($"overweight: {reader["Overweight"]}");
                    Console.WriteLine($"the highest client: {reader["TallestClient"]} см");
                    Console.WriteLine($"the heaviest client: {reader["HeaviestClient"]} кг");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when getting statistics: {ex.Message}");
            }
        }
    }
}
