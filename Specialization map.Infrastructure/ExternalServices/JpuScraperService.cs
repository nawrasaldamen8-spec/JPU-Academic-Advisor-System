using HtmlAgilityPack;
using Specialization_map.Core.DTOs;
using Specialization_map.Core.Entities;
using Specialization_map.Core.Interfaces.IServices;
using System.Net;
using System.Text.RegularExpressions;

namespace Specialization_map.Infrastructure.ExternalServices
{
    public class JpuScraperService : IScraperService
    {
        private readonly string _baseUrl = "https://onlineregistration.jpu.edu.jo";

        /// <summary>
        /// Retrieves the student profile information for the current session asynchronously.
        /// </summary>
        /// <remarks>This method fetches and parses the student profile page from the online registration
        /// system. Ensure that the provided session credentials are valid and have not expired.</remarks>
        /// <param name="sessionId">The session identifier used to authenticate the request. Cannot be null or empty.</param>
        /// <param name="cookieSession1">The session cookie value required for accessing the student profile page. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a StudentProfileDto object with
        /// the student's profile information.</returns>
        public async Task<StudentProfileDto> GetStudentInfoAsync(string sessionId, string cookieSession1)
        {
            var html = await FetchHtmlAsync("https://onlineregistration.jpu.edu.jo/ershad/students/main.aspx", sessionId, cookieSession1);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var profile = new StudentProfileDto
            {
                Name = doc.DocumentNode.SelectSingleNode("//span[@id='Students_menu1_lblname']")?.InnerText.Trim() ?? "",
                StudentId = doc.DocumentNode.SelectSingleNode("//span[@id='Students_menu1_lblStuid']")?.InnerText.Trim() ?? "",
                Major = doc.DocumentNode.SelectSingleNode("//span[@id='Students_menu1_lblmajor']")?.InnerText.Trim() ?? "",
                Email = doc.DocumentNode.SelectSingleNode("//span[@id='Label2']")?.InnerText.Trim() ?? "",

                ProfileImageUrl = _baseUrl + doc.DocumentNode.SelectSingleNode("//img[@id='Students_menu1_Image1']")?
                                  .GetAttributeValue("src", "").Replace("..", "")
            };

            return profile;
        }

        
        private async Task<string> FetchHtmlAsync(string url, string sessionId, string cookieSession1)
        {
            var cookieContainer = new CookieContainer();
            var uri = new Uri(_baseUrl);
            cookieContainer.Add(uri, new Cookie("ASP.NET_SessionId", sessionId));
            cookieContainer.Add(uri, new Cookie("cookiesession1", cookieSession1));

            using var handler = new HttpClientHandler { CookieContainer = cookieContainer };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0...");

            return await client.GetStringAsync(url);
        }

        /// <summary>
        /// Attempts to authenticate a student using the specified student ID and password, and retrieves session
        /// identifiers upon successful login.
        /// </summary>
        /// <remarks>This method performs an HTTP login to the online registration portal and parses the
        /// resulting cookies for session management. The returned session identifiers can be used for subsequent
        /// authenticated requests. Ensure that the credentials are correct and that the target site is accessible
        /// before calling this method.</remarks>
        /// <param name="studentId">The student ID used to log in to the online registration system. Cannot be null or empty.</param>
        /// <param name="password">The password associated with the specified student ID. Cannot be null or empty.</param>
        /// <returns>A tuple containing the session ID and the 'cookiesession1' value if login is successful. Both values are
        /// returned as strings.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown if the login attempt fails due to invalid credentials or if the online registration site is
        /// unavailable.</exception>
        public async Task<(string sessionId, string cookieSession1)> LoginAsync(string studentId, string password)
        {
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler { CookieContainer = cookieContainer, AllowAutoRedirect = true };
            using var client = new HttpClient(handler);

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            string loginUrl = "https://onlineregistration.jpu.edu.jo/ershad/students/login.aspx";

            var initialResponse = await client.GetAsync(loginUrl);
            var initialPage = await initialResponse.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(initialPage);

            var viewState = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATE']")?.GetAttributeValue("value", "");
            var eventValidation = doc.DocumentNode.SelectSingleNode("//input[@id='__EVENTVALIDATION']")?.GetAttributeValue("value", "");
            var viewStateGen = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATEGENERATOR']")?.GetAttributeValue("value", "");

            var loginFields = new Dictionary<string, string>
            {
                { "__VIEWSTATE", viewState ?? "" },
                { "__EVENTVALIDATION", eventValidation ?? "" },
                { "__VIEWSTATEGENERATOR", viewStateGen ?? "" },
                { "__EVENTTARGET", "" }, 
                { "__EVENTARGUMENT", "" },
                { "Login1$txtstuid", studentId },
                { "Login1$txtstupass", password },
                { "Login1$Button1", "تسجيل الدخول" }
            };

            var content = new FormUrlEncodedContent(loginFields);
            var response = await client.PostAsync(loginUrl, content);

            var mainPageHtml = await client.GetStringAsync("https://onlineregistration.jpu.edu.jo/ershad/students/main.aspx");

            bool isSuccess = mainPageHtml.Contains(studentId) || mainPageHtml.Contains("Main.aspx") || mainPageHtml.Contains("lblStuid");

            if (!isSuccess)
            {
                throw new UnauthorizedAccessException("فشل تسجيل الدخول: تأكد من الرقم الجامعي وكلمة السر، أو أن الموقع متاح.");
            }

            var cookies = cookieContainer.GetCookies(new Uri("https://onlineregistration.jpu.edu.jo"));
            string sId = cookies["ASP.NET_SessionId"]?.Value ?? "";
            string cId = cookies["cookiesession1"]?.Value ?? "";

            return (sId, cId);
        }

        /// <summary>
        /// Retrieves the list of courses included in a student's academic plan for the specified session.
        /// </summary>
        /// <param name="sessionId">The identifier of the academic session for which to retrieve the student's plan. Cannot be null or empty.</param>
        /// <param name="cookieSession1">The session cookie used to authenticate the request. Cannot be null or empty.</param>
        /// <returns>A list of courses representing the student's academic plan for the given session. Returns an empty list if
        /// no courses are found.</returns>
        public async Task<List<Course>> GetStudentPlanAsync(string sessionId, string cookieSession1)
        {
            var html = await FetchPlanHtmlAsync(sessionId, cookieSession1);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var rows = doc.DocumentNode.SelectNodes("//tr");
            if (rows == null) return new List<Course>();

            var courses = rows
                .Select(ParseCourseRow)
                .Where(c => c != null)
                .GroupBy(c => c.Code)
                .Select(g => g.First())
                .ToList();

            return courses;
        }

        private async Task<string> FetchPlanHtmlAsync(string sessionId, string cookieSession1)
        {
            var cookieContainer = new CookieContainer();
            var uri = new Uri(_baseUrl);
            cookieContainer.Add(uri, new Cookie("ASP.NET_SessionId", sessionId));
            cookieContainer.Add(uri, new Cookie("cookiesession1", cookieSession1));

            using var handler = new HttpClientHandler { CookieContainer = cookieContainer };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0...");

            var response = await client.GetAsync($"{_baseUrl}/ershad/students/planinfo.aspx");
            return await response.Content.ReadAsStringAsync();
        }

        private Course? ParseCourseRow(HtmlNode row)
        {
            var cells = row.SelectNodes("td");
            if (cells == null || cells.Count < 9) return null;

            var cellsText = cells.Select(c => c.InnerText.Trim().Replace("&nbsp;", "")).ToList();

            var fullCode = ExtractCourseCode(cellsText);
            if (string.IsNullOrEmpty(fullCode) || fullCode.Length < 5 || cellsText[6].Contains("مجموع"))
                return null;

            return new Course
            {
                Code = fullCode,
                Name = cellsText[6],
                Category = cellsText[4],
                Credits = cellsText[7],
                Grade = cellsText[8],
                Status = MapCourseStatus(cellsText[8], cellsText[9]),
                Prerequisites = ExtractPrerequisites(cells[12].InnerText),
                Mode = cellsText.Count > 13 ? cellsText[13] : "Unknown"
            };
        }

        private string MapCourseStatus(string grade, string action)
        {
            if (action.Contains("ناجح") || (!string.IsNullOrEmpty(grade) && char.IsDigit(grade[0])))
                return "Completed";

            if (grade.Contains("مسجله") || action.Contains("مسجله"))
                return "In-Progress";

            return "Not Taken";
        }

        private string ExtractCourseCode(List<string> cellsText)
        {
            if (Regex.IsMatch(cellsText[1], @"^\d+$") && cellsText[1].Length <= 3)
                return cellsText[3] + cellsText[2] + cellsText[1];

            return cellsText.FirstOrDefault(t => Regex.IsMatch(t, @"^\d{5,8}$")) ?? "";
        }

        private List<string> ExtractPrerequisites(string cellHtml)
        {
            var matches = Regex.Matches(cellHtml, @"\d{5,8}");
            return matches.Select(m => m.Value).Distinct().ToList();
        }
    }
}