namespace Specialization_map.Core.Entities
{
    public class OfferedCourse
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Section { get; set; }
        public string Days { get; set; } // ح ث خ أو ن ر
        public string TimeRange { get; set; } // 08:00 - 09:00
        public int Credits { get; set; }
    }
}
