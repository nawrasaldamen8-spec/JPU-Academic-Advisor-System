namespace Specialization_map.Core.DTOs
{
    public class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class NodeData
    {
        public string Label { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    public class NodeDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "customNode";
        public Position Position { get; set; } = new();
        public NodeData Data { get; set; } = new();
    }

    public class EdgeDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Animated { get; set; }
    }

    public class GraphResponseDTO
    {
        public List<NodeDTO> Nodes { get; set; } = new();
        public List<EdgeDTO> Edges { get; set; } = new();
    }
}
