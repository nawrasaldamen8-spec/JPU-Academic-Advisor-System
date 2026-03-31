namespace Specialization_map.Core.Entities
{
    public class Course
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; 
        public string Credits { get; set; } = "3.00";
        public string Grade { get; set; } = string.Empty;
        public string Status { get; set; } = "Not Taken";
        public List<string> Prerequisites { get; set; } = new();
        public string Mode { get; set; } = string.Empty;
    }
}
