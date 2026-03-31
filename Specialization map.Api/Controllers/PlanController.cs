using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Specialization_map.Core.DTOs;
using Specialization_map.Core.Interfaces.IServices;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
namespace Specialization_map.Api.Controllers
{
    /// <summary>
    /// Handles API requests related to student academic plans, including authentication, profile retrieval, plan
    /// visualization, and schedule suggestions.
    /// </summary>
    /// <remarks>This controller provides endpoints for logging in, retrieving student profile information,
    /// generating a graph representation of the academic plan, and suggesting course schedules based on a target credit
    /// load. All endpoints except for login require authentication. The controller relies on injected services for
    /// scraping student data and generating plan graphs.</remarks>
    [Authorize] 
    [ApiController]
    [Route("api/[controller]")]
    public class PlanController : ControllerBase
    {
        private readonly IScraperService _scraperService;
        private readonly IGraphService _graphService;
        private readonly IConfiguration _configuration;

        public PlanController(IScraperService scraperService, IGraphService graphService, IConfiguration configuration)
        {
            _scraperService = scraperService;
            _graphService = graphService;
            _configuration = configuration;
        }
        /// <summary>
        /// Authenticates a user with the provided credentials and returns a JWT token if successful.
        /// </summary>
        /// <param name="request">The login request containing the user's student ID and password. Cannot be null.</param>
        /// <returns>An HTTP 200 response containing a JWT token if authentication is successful; otherwise, an HTTP 401 response
        /// if credentials are invalid, or an HTTP 400 response for other errors.</returns>
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginRequest request)
        {
            try
            {
                var (sId, cId) = await _scraperService.LoginAsync(request.StudentId, request.Password);

                var token = GenerateJwtToken(sId, cId, request.StudentId);

                return Ok(new
                {
                    Token = token,
                    Messge = "DO NOT SHARE IT"
                });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { Message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { Message = ex.Message }); }
        }

        /// <summary>
        /// Retrieves the current user's profile information.
        /// </summary>
        /// <returns>An <see cref="IActionResult"/> containing the user's profile data if the session is valid; otherwise, an
        /// unauthorized result.</returns>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var (sId, cId) = GetSessionFromClaims();
                var profile = await _scraperService.GetStudentInfoAsync(sId, cId);
                return Ok(profile);
            }
            catch (Exception) { return Unauthorized("انتهت جلسة."); }
        }

        /// <summary>
        /// Retrieves the student's academic plan as a graph structure for visualization or analysis.
        /// </summary>
        /// <remarks>This endpoint is intended for use by authenticated users. The returned graph data can
        /// be used to display course dependencies or progress in a visual format.</remarks>
        /// <returns>An <see cref="IActionResult"/> containing the graph representation of the student's plan if successful;
        /// otherwise, a bad request result with an error message.</returns>
        [HttpGet("graph")]
        public async Task<IActionResult> GetPlanGraph()
        {
            try
            {
                var (sId, cId) = GetSessionFromClaims();
                var courses = await _scraperService.GetStudentPlanAsync(sId, cId);
                var graphData = _graphService.ConvertPlanToGraph(courses);
                return Ok(graphData);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        /// <summary>
        /// Retrieves up to three suggested course schedules based on the student's current plan and the specified
        /// target number of credits.
        /// </summary>
        /// <remarks>Use this endpoint to help students plan their course schedules by providing
        /// recommendations that align with their credit goals. The suggestions are generated based on the student's
        /// existing plan and the specified credit target.</remarks>
        /// <param name="targetCredits">The desired total number of credits for the suggested schedules. Must be between 1 and 18. Defaults to 15 if
        /// not specified.</param>
        /// <returns>An HTTP 200 response containing a collection of suggested course schedules if successful; otherwise, an HTTP
        /// 400 response with an error message.</returns>
        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions([FromQuery][Range(1, 18)] int targetCredits = 15)
        {
            try
            {
                var (sId, cId) = GetSessionFromClaims();
                var courses = await _scraperService.GetStudentPlanAsync(sId, cId);
                var suggestions = _graphService.GetThreeSuggestedSchedules(courses, targetCredits);
                return Ok(suggestions);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        private (string sId, string cId) GetSessionFromClaims()
        {
            var sId = User.FindFirst("SessionId")?.Value;
            var cId = User.FindFirst("CookieSession")?.Value;

            if (string.IsNullOrEmpty(sId) || string.IsNullOrEmpty(cId))
                throw new UnauthorizedAccessException();

            return (sId, cId);
        }

        private string GenerateJwtToken(string sId, string cId, string studentId)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("ThisIsMyVerySecretKey1234567890_JPU_Project");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.Name, studentId),
                    new Claim("SessionId", sId),
                    new Claim("CookieSession", cId)
                }),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}