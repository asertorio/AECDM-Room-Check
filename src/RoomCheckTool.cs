using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using Newtonsoft.Json.Linq;
using System.Numerics;

namespace AECDMRoomCheck;

/// <summary>
/// Tool for mapping electrical elements to rooms using AECDM API
/// </summary>
public class RoomCheckTool
{
    private const string BASE_URL = "https://developer.api.autodesk.com/aec/beta/graphql";
    private readonly string _accessToken;
    private readonly GraphQLHttpClient _client;

    public RoomCheckTool(string accessToken)
    {
        _accessToken = accessToken;
        _client = new GraphQLHttpClient(BASE_URL, new NewtonsoftJsonSerializer());
        _client.HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
    }

    /// <summary>
    /// Gets all electrical elements to room mappings for a specific model
    /// </summary>
    /// <param name="elementGroupId">The ID of the element group (model) to analyze</param>
    /// <param name="regionHeader">Optional region header for API routing</param>
    /// <returns>List of room-to-element mappings</returns>
    public async Task<List<RoomElementMapping>> GetElectricalElementsInRooms(
        string elementGroupId,
        string? regionHeader = null)
    {
        if (!string.IsNullOrWhiteSpace(regionHeader))
        {
            _client.HttpClient.DefaultRequestHeaders.Remove("region");
            _client.HttpClient.DefaultRequestHeaders.Add("region", regionHeader);
        }

        // Step 1: Get all electrical equipment elements
        Console.WriteLine("Fetching electrical elements...");
        var electricalElements = await GetElementsByCategory(elementGroupId, "Electrical Equipment");
        Console.WriteLine($"Found {electricalElements.Count} electrical elements");

        // Step 2: Get all rooms
        // Note: In Revit/BIM, rooms might be categorized as "Rooms" or "Spaces"
        // We'll try "Rooms" first
        Console.WriteLine("Fetching rooms...");
        var rooms = await GetElementsByCategory(elementGroupId, "Rooms");

        // If no rooms found, try "Spaces" as an alternative
        if (rooms.Count == 0)
        {
            Console.WriteLine("No 'Rooms' found, trying 'Spaces' category...");
            rooms = await GetElementsByCategory(elementGroupId, "Spaces");
        }

        Console.WriteLine($"Found {rooms.Count} rooms/spaces");

        if (rooms.Count == 0)
        {
            throw new InvalidOperationException(
                "No rooms or spaces found. Please verify the model contains room elements and the category name is correct.");
        }

        // Step 3: Check which electrical elements are inside which rooms
        Console.WriteLine("Analyzing spatial containment...");
        var mappings = await AnalyzeElementContainment(electricalElements, rooms);
        Console.WriteLine($"Generated {mappings.Count} room-element mappings");

        return mappings;
    }

    /// <summary>
    /// Gets elements from the element group filtered by category
    /// </summary>
    private async Task<List<BIMElement>> GetElementsByCategory(string elementGroupId, string category)
    {
        var query = new GraphQLRequest
        {
            Query = @"
            query GetElementsByElementGroupWithFilter ($elementGroupId: ID!, $filter: String!) {
              elementsByElementGroup(elementGroupId: $elementGroupId, pagination: {limit:500}, filter: {query:$filter}) {
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
            }",
            Variables = new
            {
                elementGroupId = elementGroupId,
                filter = $"'property.name.category'=='{category}' and 'property.name.Element Context'=='Instance'"
            }
        };

        var response = await _client.SendQueryAsync<object>(query);

        if (response.Data == null)
        {
            if (response.Errors != null && response.Errors.Length > 0)
            {
                throw new Exception($"GraphQL Error: {response.Errors[0].Message}");
            }
            return new List<BIMElement>();
        }

        JObject jsonData = JObject.FromObject(response.Data);
        JArray? elements = (JArray?)jsonData.SelectToken("elementsByElementGroup.results");

        if (elements == null)
        {
            return new List<BIMElement>();
        }

        var elementsList = new List<BIMElement>();

        foreach (var element in elements)
        {
            try
            {
                var bimElement = new BIMElement
                {
                    Id = element.SelectToken("id")?.ToString() ?? string.Empty,
                    Name = element.SelectToken("name")?.ToString() ?? string.Empty,
                    Category = category
                };

                JArray? properties = (JArray?)element.SelectToken("properties.results");
                if (properties != null)
                {
                    // Extract bounding box information from properties
                    bimElement.BoundingBox = ExtractBoundingBoxFromProperties(properties);
                    bimElement.Properties = ExtractProperties(properties);
                }

                elementsList.Add(bimElement);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing element: {ex.Message}");
            }
        }

        return elementsList;
    }

    /// <summary>
    /// Extracts bounding box information from element properties
    /// </summary>
    private BoundingBox3D ExtractBoundingBoxFromProperties(JArray properties)
    {
        var boundingBox = new BoundingBox3D();

        foreach (JToken property in properties)
        {
            try
            {
                string? propName = property.SelectToken("name")?.ToString();
                string? propValue = property.SelectToken("value")?.ToString();

                if (string.IsNullOrEmpty(propName) || string.IsNullOrEmpty(propValue))
                    continue;

                // Look for bounding box properties
                // Common property names in Revit: "BoundingBox.Min", "BoundingBox.Max"
                // or individual coordinate properties
                switch (propName.ToLower())
                {
                    case "boundingbox.min.x":
                    case "bounding box min x":
                        if (float.TryParse(propValue, out float minX)) boundingBox.MinX = minX;
                        break;
                    case "boundingbox.min.y":
                    case "bounding box min y":
                        if (float.TryParse(propValue, out float minY)) boundingBox.MinY = minY;
                        break;
                    case "boundingbox.min.z":
                    case "bounding box min z":
                        if (float.TryParse(propValue, out float minZ)) boundingBox.MinZ = minZ;
                        break;
                    case "boundingbox.max.x":
                    case "bounding box max x":
                        if (float.TryParse(propValue, out float maxX)) boundingBox.MaxX = maxX;
                        break;
                    case "boundingbox.max.y":
                    case "bounding box max y":
                        if (float.TryParse(propValue, out float maxY)) boundingBox.MaxY = maxY;
                        break;
                    case "boundingbox.max.z":
                    case "bounding box max z":
                        if (float.TryParse(propValue, out float maxZ)) boundingBox.MaxZ = maxZ;
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing property for bounding box: {ex.Message}");
            }
        }

        return boundingBox;
    }

    /// <summary>
    /// Extracts all properties from element
    /// </summary>
    private Dictionary<string, string> ExtractProperties(JArray properties)
    {
        var props = new Dictionary<string, string>();

        foreach (JToken property in properties)
        {
            try
            {
                string? name = property.SelectToken("name")?.ToString();
                string? value = property.SelectToken("value")?.ToString();

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                {
                    props[name] = value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing property: {ex.Message}");
            }
        }

        return props;
    }

    /// <summary>
    /// Analyzes which electrical elements are contained within which rooms
    /// </summary>
    private async Task<List<RoomElementMapping>> AnalyzeElementContainment(
        List<BIMElement> electricalElements,
        List<BIMElement> rooms)
    {
        var mappings = new List<RoomElementMapping>();

        foreach (var room in rooms)
        {
            if (!room.BoundingBox.IsValid())
            {
                Console.WriteLine($"Warning: Room '{room.Name}' has invalid bounding box, skipping...");
                continue;
            }

            foreach (var element in electricalElements)
            {
                if (!element.BoundingBox.IsValid())
                {
                    Console.WriteLine($"Warning: Element '{element.Name}' has invalid bounding box, skipping...");
                    continue;
                }

                // Check if element is contained within room using geometric containment
                if (IsElementContainedInRoom(element.BoundingBox, room.BoundingBox))
                {
                    mappings.Add(new RoomElementMapping
                    {
                        RoomId = room.Id,
                        RoomName = room.Name,
                        ElementId = element.Id,
                        ElementName = element.Name,
                        ElementCategory = element.Category,
                        ContainmentType = DetermineContainmentType(element.BoundingBox, room.BoundingBox)
                    });
                }
            }
        }

        return mappings;
    }

    /// <summary>
    /// Determines if an element is contained within a room using bounding box comparison
    /// Uses center point containment - if the element's center point is inside the room's bounding box
    /// </summary>
    private bool IsElementContainedInRoom(BoundingBox3D elementBox, BoundingBox3D roomBox)
    {
        // Calculate the center point of the element
        var centerX = (elementBox.MinX + elementBox.MaxX) / 2f;
        var centerY = (elementBox.MinY + elementBox.MaxY) / 2f;
        var centerZ = (elementBox.MinZ + elementBox.MaxZ) / 2f;

        // Check if the center point is inside the room's bounding box
        return centerX >= roomBox.MinX && centerX <= roomBox.MaxX &&
               centerY >= roomBox.MinY && centerY <= roomBox.MaxY &&
               centerZ >= roomBox.MinZ && centerZ <= roomBox.MaxZ;
    }

    /// <summary>
    /// Determines the type of containment (full, partial, or overlap)
    /// </summary>
    private string DetermineContainmentType(BoundingBox3D elementBox, BoundingBox3D roomBox)
    {
        // Check if element is fully contained
        bool fullyContained =
            elementBox.MinX >= roomBox.MinX && elementBox.MaxX <= roomBox.MaxX &&
            elementBox.MinY >= roomBox.MinY && elementBox.MaxY <= roomBox.MaxY &&
            elementBox.MinZ >= roomBox.MinZ && elementBox.MaxZ <= roomBox.MaxZ;

        if (fullyContained)
            return "Fully Contained";

        // Check if element overlaps (at least center point is inside)
        return "Center Point Inside";
    }
}

/// <summary>
/// Represents a BIM element with its properties and bounding box
/// </summary>
public class BIMElement
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public BoundingBox3D BoundingBox { get; set; } = new BoundingBox3D();
    public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// Represents a 3D bounding box
/// </summary>
public class BoundingBox3D
{
    public float MinX { get; set; }
    public float MinY { get; set; }
    public float MinZ { get; set; }
    public float MaxX { get; set; }
    public float MaxY { get; set; }
    public float MaxZ { get; set; }

    public bool IsValid()
    {
        // Check if the bounding box has been initialized with non-default values
        return !(MinX == 0 && MinY == 0 && MinZ == 0 && MaxX == 0 && MaxY == 0 && MaxZ == 0) &&
               MaxX > MinX && MaxY > MinY && MaxZ > MinZ;
    }

    public double Volume => Math.Abs((MaxX - MinX) * (MaxY - MinY) * (MaxZ - MinZ));

    public Vector3 Center => new Vector3((MinX + MaxX) / 2, (MinY + MaxY) / 2, (MinZ + MaxZ) / 2);

    public override string ToString()
    {
        return $"Min({MinX:F2}, {MinY:F2}, {MinZ:F2}) - Max({MaxX:F2}, {MaxY:F2}, {MaxZ:F2})";
    }
}

/// <summary>
/// Represents a mapping between a room and an electrical element
/// </summary>
public class RoomElementMapping
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string ElementId { get; set; } = string.Empty;
    public string ElementName { get; set; } = string.Empty;
    public string ElementCategory { get; set; } = string.Empty;
    public string ContainmentType { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Room: {RoomName} | Element: {ElementName} | Type: {ContainmentType}";
    }
}
