using AECDMRoomCheck;

namespace AECDMRoomCheck.Examples;

/// <summary>
/// Example usage of the RoomCheckTool
/// </summary>
public class UsageExample
{
    public static async Task RunExample()
    {
        // Step 1: Set up authentication
        // You'll need to obtain an access token from Autodesk authentication
        string accessToken = "YOUR_ACCESS_TOKEN_HERE";

        // Step 2: Create the tool instance
        var roomCheckTool = new RoomCheckTool(accessToken);

        // Step 3: Specify the element group (model) ID to analyze
        // You can get this from the GetElementGroupsByProject method in the AECDM API
        string elementGroupId = "YOUR_ELEMENT_GROUP_ID_HERE";

        // Optional: Specify region header if needed
        string? regionHeader = null; // or "US", "EMEA", etc.

        try
        {
            // Step 4: Get the electrical elements to room mappings
            Console.WriteLine("Starting room check analysis...");
            var mappings = await roomCheckTool.GetElectricalElementsInRooms(
                elementGroupId,
                regionHeader
            );

            // Step 5: Process the results
            Console.WriteLine($"\n=== RESULTS ===");
            Console.WriteLine($"Total mappings found: {mappings.Count}\n");

            // Group by room
            var groupedByRoom = mappings.GroupBy(m => m.RoomName);

            foreach (var roomGroup in groupedByRoom)
            {
                Console.WriteLine($"Room: {roomGroup.Key}");
                Console.WriteLine($"  Electrical elements: {roomGroup.Count()}");

                foreach (var mapping in roomGroup)
                {
                    Console.WriteLine($"    - {mapping.ElementName} ({mapping.ContainmentType})");
                }
                Console.WriteLine();
            }

            // Step 6: Export to another format if needed
            ExportToJson(mappings, "room-electrical-mapping.json");
            ExportToCsv(mappings, "room-electrical-mapping.csv");

            Console.WriteLine("Analysis complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Export mappings to JSON format for use in other skills
    /// </summary>
    private static void ExportToJson(List<RoomElementMapping> mappings, string filePath)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(mappings, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(filePath, json);
        Console.WriteLine($"Exported to JSON: {filePath}");
    }

    /// <summary>
    /// Export mappings to CSV format
    /// </summary>
    private static void ExportToCsv(List<RoomElementMapping> mappings, string filePath)
    {
        using var writer = new StreamWriter(filePath);

        // Write header
        writer.WriteLine("Room ID,Room Name,Element ID,Element Name,Element Category,Containment Type");

        // Write data
        foreach (var mapping in mappings)
        {
            writer.WriteLine($"\"{mapping.RoomId}\",\"{mapping.RoomName}\",\"{mapping.ElementId}\",\"{mapping.ElementName}\",\"{mapping.ElementCategory}\",\"{mapping.ContainmentType}\"");
        }

        Console.WriteLine($"Exported to CSV: {filePath}");
    }

    /// <summary>
    /// Example: Filter mappings by containment type
    /// </summary>
    public static List<RoomElementMapping> FilterFullyContainedOnly(List<RoomElementMapping> mappings)
    {
        return mappings.Where(m => m.ContainmentType == "Fully Contained").ToList();
    }

    /// <summary>
    /// Example: Get all elements in a specific room
    /// </summary>
    public static List<RoomElementMapping> GetElementsInRoom(List<RoomElementMapping> mappings, string roomName)
    {
        return mappings.Where(m => m.RoomName.Equals(roomName, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Example: Generate a summary report
    /// </summary>
    public static string GenerateSummaryReport(List<RoomElementMapping> mappings)
    {
        var report = new System.Text.StringBuilder();

        report.AppendLine("=== ELECTRICAL ELEMENTS TO ROOMS MAPPING SUMMARY ===");
        report.AppendLine();

        // Total statistics
        var totalRooms = mappings.Select(m => m.RoomId).Distinct().Count();
        var totalElements = mappings.Select(m => m.ElementId).Distinct().Count();

        report.AppendLine($"Total Rooms with Electrical Elements: {totalRooms}");
        report.AppendLine($"Total Electrical Elements Mapped: {totalElements}");
        report.AppendLine($"Total Mappings: {mappings.Count}");
        report.AppendLine();

        // Per-room breakdown
        report.AppendLine("=== PER-ROOM BREAKDOWN ===");
        var groupedByRoom = mappings.GroupBy(m => m.RoomName).OrderByDescending(g => g.Count());

        foreach (var roomGroup in groupedByRoom)
        {
            report.AppendLine($"\n{roomGroup.Key}: {roomGroup.Count()} element(s)");

            var fullyContained = roomGroup.Count(m => m.ContainmentType == "Fully Contained");
            var centerInside = roomGroup.Count(m => m.ContainmentType == "Center Point Inside");

            report.AppendLine($"  - Fully Contained: {fullyContained}");
            report.AppendLine($"  - Center Point Inside: {centerInside}");
        }

        return report.ToString();
    }
}
