# AECDM Room Check Tool

A C# skill for mapping electrical elements to rooms in AEC (Architecture, Engineering, Construction) projects using the Autodesk AECDM (AEC Data Model) API.

## Overview

This tool retrieves electrical elements and rooms from a BIM model and determines which electrical elements are contained within which rooms using geometric containment analysis. The results can be used by other skills for scheduling, reporting, or further analysis.

## Features

- **Electrical Elements Retrieval**: Queries all electrical equipment from a model using AECDM GraphQL API
- **Room/Space Retrieval**: Fetches all rooms or spaces from the model
- **Geometric Containment Analysis**: Determines if an element is inside a room by checking if its center point falls within the room's bounding box
- **Flexible Output**: Returns structured data (room name, element name, containment type) that can be easily consumed by other tools
- **Single Model Processing**: Analyzes one model at a time for focused analysis

## Architecture

### Key Components

1. **RoomCheckTool**: Main class that orchestrates the workflow
   - Authenticates with AECDM API using access token
   - Retrieves electrical elements and rooms via GraphQL queries
   - Performs spatial containment analysis

2. **BIMElement**: Represents a building element with properties and 3D bounding box

3. **BoundingBox3D**: 3D bounding box with min/max coordinates for spatial analysis

4. **RoomElementMapping**: Output data structure containing:
   - Room ID and Name
   - Element ID and Name
   - Element Category
   - Containment Type (Fully Contained or Center Point Inside)

## Usage

### Basic Usage

```csharp
using AECDMRoomCheck;

// 1. Create tool instance with access token
var tool = new RoomCheckTool(accessToken);

// 2. Run analysis on a specific model
var mappings = await tool.GetElectricalElementsInRooms(elementGroupId);

// 3. Process results
foreach (var mapping in mappings)
{
    Console.WriteLine($"Room: {mapping.RoomName} | Element: {mapping.ElementName}");
}
```

### Export Results

Results can be exported to JSON or CSV for use in other skills:

```csharp
// Export to JSON
var json = System.Text.Json.JsonSerializer.Serialize(mappings);
File.WriteAllText("output.json", json);

// Export to CSV
using var writer = new StreamWriter("output.csv");
writer.WriteLine("Room Name,Element Name,Containment Type");
foreach (var m in mappings)
{
    writer.WriteLine($"{m.RoomName},{m.ElementName},{m.ContainmentType}");
}
```

## How It Works

### 1. Element Retrieval

The tool uses GraphQL queries to retrieve elements by category:

```graphql
query GetElementsByElementGroupWithFilter ($elementGroupId: ID!, $filter: String!) {
  elementsByElementGroup(elementGroupId: $elementGroupId, filter: {query:$filter}) {
    results {
      id
      name
      properties {
        results {
          name
          value
        }
      }
    }
  }
}
```

Filters used:
- **Electrical Elements**: `category == 'Electrical Equipment' AND Element Context == 'Instance'`
- **Rooms**: `category == 'Rooms' AND Element Context == 'Instance'` (or 'Spaces' if Rooms not found)

### 2. Geometric Containment

The tool determines containment using center point analysis:

1. Extract bounding box coordinates from element properties:
   - MinX, MinY, MinZ
   - MaxX, MaxY, MaxZ

2. Calculate element's center point:
   ```csharp
   centerX = (MinX + MaxX) / 2
   centerY = (MinY + MaxY) / 2
   centerZ = (MinZ + MaxZ) / 2
   ```

3. Check if center point is within room bounds:
   ```csharp
   isInside = (centerX >= roomMinX && centerX <= roomMaxX &&
               centerY >= roomMinY && centerY <= roomMaxY &&
               centerZ >= roomMinZ && centerZ <= roomMaxZ)
   ```

### 3. Containment Types

- **Fully Contained**: Element's entire bounding box is within room bounds
- **Center Point Inside**: Element's center point is within room bounds (partial containment)

## Requirements

- .NET 8.0 or higher
- NuGet Packages:
  - GraphQL.Client (v6.0.0)
  - GraphQL.Client.Serializer.Newtonsoft (v6.0.0)
  - Newtonsoft.Json (v13.0.3)

## API Dependencies

This tool uses the Autodesk AEC Data Model (AECDM) GraphQL API:
- **Base URL**: `https://developer.api.autodesk.com/aec/beta/graphql`
- **Authentication**: Bearer token (OAuth 2.0)
- **Region Support**: Optional region header for geographic API routing

## Data Structure

### Input
- `elementGroupId`: The ID of the model to analyze (from `GetElementGroupsByProject`)
- `accessToken`: Autodesk authentication token

### Output
```csharp
public class RoomElementMapping
{
    public string RoomId { get; set; }
    public string RoomName { get; set; }
    public string ElementId { get; set; }
    public string ElementName { get; set; }
    public string ElementCategory { get; set; }
    public string ContainmentType { get; set; }
}
```

## Notes

### Room Category Names

In Revit/BIM systems, rooms can be categorized as:
- `Rooms` (typical for architectural models)
- `Spaces` (used in MEP models)

The tool automatically tries both categories if the first one returns no results.

### Bounding Box Extraction

Bounding box coordinates must be present in element properties. Common property names:
- `BoundingBox.Min.X/Y/Z`
- `BoundingBox.Max.X/Y/Z`

If these properties are not available in your model, you may need to modify the `ExtractBoundingBoxFromProperties` method to match your property naming convention.

### Limitations

- Processes one model at a time
- Uses simplified geometric containment (center point or full box comparison)
- Does not handle complex geometry intersections
- Bounding box-based analysis (rooms and elements must have bounding box data)

## Future Enhancements

Potential improvements for future versions:
- Support for multiple models simultaneously
- More sophisticated containment algorithms (mesh intersection)
- Support for other electrical element types (fixtures, lighting, panels, etc.)
- Distance-based proximity analysis
- Visualization output (3D viewer integration)
- Batch processing capabilities

## Integration with Other Skills

This tool is designed to output data that can be consumed by other skills:

1. **Scheduling Skills**: Generate room-by-room electrical equipment schedules
2. **Export Skills**: Create formatted reports, Excel spreadsheets, or BIM coordination documents
3. **Validation Skills**: Check code compliance (e.g., required outlets per room)
4. **Cost Estimation Skills**: Calculate material costs by room
5. **Visualization Skills**: Generate 3D markup or color-coded floor plans

## Example Output

```
=== RESULTS ===
Total mappings found: 15

Room: Office 101
  Electrical elements: 3
    - Power Outlet 1 (Fully Contained)
    - Light Switch 1 (Fully Contained)
    - Ceiling Light 1 (Center Point Inside)

Room: Conference Room 201
  Electrical elements: 5
    - Power Outlet 2 (Fully Contained)
    - Power Outlet 3 (Fully Contained)
    - Light Switch 2 (Fully Contained)
    - Ceiling Light 2 (Center Point Inside)
    - Projector Power (Fully Contained)
```

## License

This project is provided as-is for educational and development purposes.

## References

- [Autodesk Platform Services Documentation](https://aps.autodesk.com/)
- [AECDM API Reference](https://aps.autodesk.com/en/docs/aec/v1/reference/)
- [GraphQL Client for .NET](https://github.com/graphql-dotnet/graphql-client)