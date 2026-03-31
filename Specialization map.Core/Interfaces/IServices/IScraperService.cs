using Specialization_map.Core.DTOs;
using Specialization_map.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Specialization_map.Core.Interfaces.IServices
{
    /// <summary>
    /// Defines methods for authenticating a student and retrieving student-related academic information from a remote
    /// source.
    /// </summary>
    /// <remarks>Implementations of this interface provide functionality to log in with student credentials
    /// and access student plans and profile data. Methods require valid session identifiers, which are typically
    /// obtained through a successful login. This interface is intended for use in scenarios where automated access to
    /// student academic data is required, such as integration with external systems or services.</remarks>
    public interface IScraperService
    {
        Task<(string sessionId, string cookieSession1)> LoginAsync(string studentId, string password);
        Task<List<Course>> GetStudentPlanAsync(string sessionId, string cookieSession1);
        Task<StudentProfileDto> GetStudentInfoAsync(string sessionId, string cookieSession1);
    }
}
