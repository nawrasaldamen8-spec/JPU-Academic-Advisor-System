using Specialization_map.Core.Entities;

namespace Specialization_map.Core.DTOs
{
    public class SuggestedScheduleDto
    {
        public string StrategyName { get; set; }
        public string Description { get; set; }
        public List<CourseDto> Courses { get; set; } = new();
        public double TotalCredits { get; set; }
    }

    public class CourseDto
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Credits { get; set; }
        public string Status { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public string Mode { get; set; }
    }

}
