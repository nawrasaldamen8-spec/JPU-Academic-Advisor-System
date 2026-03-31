using Specialization_map.Core.Entities;
using Specialization_map.Core.Interfaces.IServices;
using Specialization_map.Core.DTOs;
using System.Text.RegularExpressions;



namespace Specialization_map.Services
{
    public class GraphService : IGraphService
    {
        private class WeightedCourse
        {
            public Course Course { get; set; } = null!;
            public int Weight { get; set; }
            public double Credits { get; set; }
        }

        /// <summary>
        /// Calculates the level for each course in the provided list based on their dependencies.
        /// </summary>
        /// <remarks>A course's level typically represents its position in a dependency hierarchy, where
        /// courses with no prerequisites have the lowest level. The method assumes that course codes are unique within
        /// the list.</remarks>
        /// <param name="courses">The list of courses for which to calculate levels. Each course should have a unique code and may have
        /// dependencies on other courses in the list. Cannot be null.</param>
        /// <returns>A dictionary mapping each course code to its calculated level. The dictionary contains one entry for each
        /// course in the input list.</returns>
        private Dictionary<string, int> CalculateAllLevels(List<Course> courses)
        {
            var levels = new Dictionary<string, int>();
            foreach (var course in courses)
            {
                CalculateLevel(course.Code, courses, levels);
            }
            return levels;
        }

        /// <summary>
        /// Calculates the prerequisite level of a course based on its dependencies.
        /// </summary>
        /// <remarks>A higher level indicates that the course depends on more layers of prerequisites.
        /// This method uses recursion and memoization to efficiently compute levels for courses with complex
        /// prerequisite chains.</remarks>
        /// <param name="code">The course code for which to calculate the level.</param>
        /// <param name="all">A list of all available courses, including their prerequisite information.</param>
        /// <param name="levels">A dictionary used to cache and store the calculated levels for course codes. Must not be null.</param>
        /// <returns>The prerequisite level of the specified course. Returns 0 if the course has no prerequisites or is not
        /// found.</returns>
        private int CalculateLevel(string code, List<Course> all, Dictionary<string, int> levels)
        {
            if (levels.ContainsKey(code)) return levels[code];

            var course = all.FirstOrDefault(c => c.Code == code);
            if (course == null || !course.Prerequisites.Any())
            {
                levels[code] = 0;
                return 0;
            }

            int maxPrereqLevel = -1;
            foreach (var pCode in course.Prerequisites)
            {
                int pLevel = CalculateLevel(pCode, all, levels);
                if (pLevel > maxPrereqLevel) maxPrereqLevel = pLevel;
            }

            int currentLevel = maxPrereqLevel + 1;
            levels[code] = currentLevel;
            return currentLevel;
        }

        /// <summary>
        /// Converts a list of courses into a graph representation suitable for visualization or further processing.
        /// </summary>
        /// <remarks>The resulting graph organizes courses by their calculated levels, which may be used
        /// to display course progression or dependencies. The method does not modify the input list.</remarks>
        /// <param name="courses">The list of courses to be converted into graph nodes and edges. Each course represents a node, and
        /// prerequisites are represented as edges.</param>
        /// <returns>A GraphResponseDTO containing nodes for each course and edges representing prerequisite relationships.</returns>
        public GraphResponseDTO ConvertPlanToGraph(List<Course> courses)
        {
            var response = new GraphResponseDTO();
            var levels = CalculateAllLevels(courses);
            var levelCounters = new Dictionary<int, int>();

            foreach (var course in courses)
            {
                int level = levels[course.Code];
                levelCounters[level] = levelCounters.GetValueOrDefault(level, 0);

                response.Nodes.Add(CreateNode(course, level, levelCounters[level]));

                response.Edges.AddRange(CreateEdgesForCourse(course));

                levelCounters[level]++;
            }

            return response;
        }

        /// <summary>
        /// Returns a list of courses that the student is eligible to take based on their current course statuses and
        /// prerequisites.
        /// </summary>
        /// <param name="courses">The list of all available courses to evaluate for eligibility. Each course should include its current status
        /// and prerequisite information.</param>
        /// <returns>A list of courses that have not been taken and for which all prerequisites have been satisfied. The list
        /// will be empty if no courses are eligible.</returns>
        public List<Course> GetEligibleCourses(List<Course> courses)
        {
            return courses
                .Where(c => c.Status == "Not Taken")
                .Where(c => CanTakeCourse(c, courses))
                .ToList();
        }

        private bool CanTakeCourse(Course course, List<Course> allCourses)
        {
            return course.Prerequisites.All(pCode =>
                allCourses.Any(ac => ac.Code == pCode && ac.Status == "Completed")
            );
        }

        /// <summary>
        /// Generates three suggested course schedules based on the provided courses and target credit count.
        /// </summary>
        /// <remarks>The three suggested schedules differ in their prioritization: one emphasizes required
        /// major courses, another balances between major and general requirements, and the third focuses on completing
        /// elective requirements with minimal effort.</remarks>
        /// <param name="allCourses">The list of all available courses to consider when building suggested schedules.</param>
        /// <param name="targetCredits">The desired total number of credits for each suggested schedule. Must be a positive integer.</param>
        /// <returns>A list containing three suggested schedules, each represented by a SuggestedScheduleDto. Each schedule
        /// offers a different prioritization strategy based on the input courses and target credits.</returns>
        public List<SuggestedScheduleDto> GetThreeSuggestedSchedules(List<Course> allCourses, int targetCredits)
        {
            var eligible = GetWeightedEligibleCourses(allCourses);

            return new List<SuggestedScheduleDto>
        {
            BuildSchedule("المسار الاستراتيجي", "الأولوية لمواد التخصص الإجبارية تفتح مواد اخرى.",
                eligible.OrderByDescending(x => x.Weight).ToList(), targetCredits),

            BuildSchedule("المسار المتوازن", " بين مواد التخصص والمتطلبات العامة.",
                eligible.OrderBy(x => Guid.NewGuid()).ToList(), targetCredits),

            BuildSchedule("المسار الهادئ", "تركيز على إنهاء المتطلبات الاختيارية بأقل جهد.",
                eligible.OrderBy(x => x.Weight).ToList(), targetCredits)
        };
        }
        /// <summary>
        /// Builds a suggested course schedule based on the provided strategy name, description, course pool, and target
        /// credit count.
        /// </summary>
        /// <remarks>The method selects courses from the pool in order, adding them to the schedule until
        /// the total credits reach or slightly exceed the target. The resulting schedule may include a small tolerance
        /// above the target credit count.</remarks>
        /// <param name="name">The name of the scheduling strategy to be used in the suggested schedule.</param>
        /// <param name="desc">A description of the scheduling strategy or approach.</param>
        /// <param name="pool">A list of weighted courses from which to select courses for the schedule. Each course includes its
        /// associated credit value.</param>
        /// <param name="target">The target total number of credits for the suggested schedule. Must be a positive integer.</param>
        /// <returns>A SuggestedScheduleDto containing the selected courses, total credits, and strategy information. The total
        /// credits may slightly exceed the target to allow for minor flexibility.</returns>
        private SuggestedScheduleDto BuildSchedule(string name, string desc, List<WeightedCourse> pool, int target)
        {
            var schedule = new SuggestedScheduleDto { StrategyName = name, Description = desc };
            double currentTotal = 0;

            foreach (var item in pool)
            {
                if (currentTotal + item.Credits <= target + 1)
                {
                    schedule.Courses.Add(MapToCourseDto(item.Course));
                    currentTotal += item.Credits;
                }

                if (currentTotal >= target) break;
            }

            schedule.TotalCredits = currentTotal;
            return schedule;
        }

        /// <summary>
        /// Generates a list of weighted courses that are eligible based on the provided course list.
        /// </summary>
        /// <remarks>The weight for each course indicates how many other courses list it as a prerequisite. The credit
        /// value is parsed from the course's Credits property, defaulting to 3.0 if parsing fails.</remarks>
        /// <param name="all">The complete list of available courses to evaluate for eligibility and weighting.</param>
        /// <returns>A list of WeightedCourse objects representing eligible courses, each with an assigned weight and credit value.</returns>
        private List<WeightedCourse> GetWeightedEligibleCourses(List<Course> all)
        {
            return GetEligibleCourses(all).Select(c => new WeightedCourse
            {
                Course = c,
                Weight = all.Count(other => other.Prerequisites.Contains(c.Code)),
                Credits = double.TryParse(c.Credits, out var res) ? res : 3.0
            }).ToList();
        }

        private NodeDTO CreateNode(Course course, int level, int row) => new()
        {
            Id = course.Code,
            Position = new Position { X = level * 350, Y = row * 120 },
            Data = new NodeData
            {
                Label = course.Name,
                Status = course.Status,
                Category = course.Category,
            }
        };

        /// <summary>
        /// Creates a collection of edge representations for the specified course's prerequisites.
        /// </summary>
        /// <remarks>Each edge connects a prerequisite course to the specified course. The edge is marked
        /// as animated if the course status is "In-Progress".</remarks>
        /// <param name="course">The course for which to generate prerequisite edges. Cannot be null.</param>
        /// <returns>An enumerable collection of EdgeDTO objects representing the prerequisite relationships for the course. The
        /// collection is empty if the course has no prerequisites.</returns>
        private IEnumerable<EdgeDTO> CreateEdgesForCourse(Course course)
        {
            return course.Prerequisites.Select(p => new EdgeDTO
            {
                Id = $"e{p}-{course.Code}",
                Source = p,
                Target = course.Code,
                Animated = course.Status == "In-Progress"
            });
        }

        private CourseDto MapToCourseDto(Course c) => new()
        {
            Code = c.Code,
            Name = c.Name,
            Credits = c.Credits,
            Category = c.Category
        };
    }
}