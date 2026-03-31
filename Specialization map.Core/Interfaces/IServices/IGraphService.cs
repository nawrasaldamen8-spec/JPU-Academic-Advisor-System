using Specialization_map.Core.DTOs;
using Specialization_map.Core.Entities;

namespace Specialization_map.Core.Interfaces.IServices
{
    /// <summary>
    /// Defines methods for converting course plans to graph representations, determining eligible courses, and
    /// generating suggested course schedules.
    /// </summary>
    /// <remarks>Implementations of this interface provide functionality for academic planning scenarios, such
    /// as visualizing course dependencies and assisting with schedule optimization. Methods may rely on course
    /// prerequisites and credit constraints to produce results.</remarks>
    public interface IGraphService
    {
        GraphResponseDTO ConvertPlanToGraph(List<Course> courses);
        List<Course> GetEligibleCourses(List<Course> courses);
        List<SuggestedScheduleDto> GetThreeSuggestedSchedules(List<Course> allCourses, int targetCredits);
    }
}
