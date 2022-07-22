using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace CompassApi;

public class CompassClient {

    private readonly Func<string, Task> _logger;
    private string _cookie;
    private string _userId;
    private bool _isLoggedIn;
    private readonly string _schoolPrefix;

    private const string LoginUrlConst = "https://{school}.compass.education/login.aspx?sessionstate=disabled";
    private string LoginUrl => LoginUrlConst.Replace("{school}", _schoolPrefix);

    private const string GetClassesUrlConst =
        "https://{school}.compass.education/Services/Calendar.svc/GetCalendarEventsByUser" +
        "?sessionstate=readonly&" +
        "includeEvents=false&" +
        "includeAllPd=false&" +
        "includeExams=false&" +
        "includeVolunteeringEvent=false";
    private string GetClassesUrl => GetClassesUrlConst.Replace("{school}", _schoolPrefix);

    private const string GetNewsItemsUrlConst = "https://{school}.compass.education/Services/NewsFeed.svc/GetMyNewsFeedPaged?sessionstate=readonly";
    private string GetNewsItemsUrl => GetNewsItemsUrlConst.Replace("{school}", _schoolPrefix);

    private const string GetUserInfoUrlConst =
        "https://{school}.compass.education/Services/User.svc/GetUserDetailsBlobByUserId?sessionstate=readonly";
    private string GetUserInfoUrl => GetUserInfoUrlConst.Replace("{school}", _schoolPrefix);

    private const string GetClassNamesConst = "https://{school}.compass.education/Services/Communications.svc/GetClassTeacherDetailsByStudent?sessionstate=readonly";
    private string GetClassNamesUrl => GetClassNamesConst.Replace("{school}", _schoolPrefix);
    
    private const string MainUrlConst = "https://{school}.compass.education";
    private string MainUrl => MainUrlConst.Replace("{school}", _schoolPrefix);

    /// <summary>
    /// Initializes a new instance of the <see cref="CompassClient"/> class.
    /// </summary>
    /// <param name="schoolPrefix">The prefix of the school you wish to login to the compass of.</param>
    /// <param name="logger">The logging function to use, defaults to none</param>
    public CompassClient(string schoolPrefix, Func<string, Task> logger = null) {
        _schoolPrefix = schoolPrefix;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompassClient"/> class.
    /// </summary>
    /// <param name="loginState">The already existing login state to use.</param>
    /// /// <param name="logger">The logging function to use, defaults to none</param>
    public CompassClient(CompassLoginState loginState, Func<string, Task> logger = null) {
        _schoolPrefix = loginState.SchoolPrefix;
        _cookie = loginState.Cookie;
        _userId = loginState.UserId;
        _isLoggedIn = true;
        _logger = logger;
    }
    
    private async void Log(string message) {
        if (_logger != null) {
            await _logger.Invoke(message);
        }
    }

    /// <summary>
    /// Get an object that contains enough information to log in to Compass.
    /// Use the object to save and keep the login between sessions.
    /// </summary>
    /// <returns>The login state object</returns>
    /// <exception cref="CompassException">When you have not already logged in</exception>
    public CompassLoginState GetLoginState() {
        if (!_isLoggedIn) {
            throw new CompassException("Not logged in");
        }
        return new CompassLoginState {
            Cookie = _cookie,
            UserId = _userId,
            SchoolPrefix = _schoolPrefix
        };
    }

    /// <summary>
    /// Authenticates the user.
    /// </summary>
    /// <param name="username">The username to login to Compass with</param>
    /// <param name="password">The password to login to Compass with</param>
    /// <returns>A boolean value indicating if the login was successful</returns>
    public async Task<bool> Authenticate(string username, string password) {
        
        {
            HttpClientHandler handler = new () {
                AllowAutoRedirect = false
            };
            
            HttpClient client = new (handler);
            FormUrlEncodedContent content = new (new[] {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("__EVENTTARGET", "button1"),
            });
            Log(await content.ReadAsStringAsync());
            HttpResponseMessage response = await client.PostAsync(LoginUrl, content);
            Log("Authentication response: " + response.StatusCode);
            
            // Print all the headers
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers) {
                Log(header.Key + ": " + string.Join(", ", header.Value));
            }
            
            await File.WriteAllTextAsync("response.html", await response.Content.ReadAsStringAsync());
            
            if (response.Headers.Contains("Set-Cookie")) {
                Log("Setting cookies");
                if (response.Headers.GetValues("Set-Cookie").Count() != 1) {
                    foreach (string setCookieHeader in response.Headers.GetValues("Set-Cookie")) {
                        Log("Set-Cookie: " + setCookieHeader);
                    }
                }
            
                // Get the cookie from the Set-Cookie header.
                IEnumerable<string> setCookieHeaders = response.Headers.GetValues("Set-Cookie");
                Dictionary<string, string> cookies = new ();
                foreach (string header in setCookieHeaders) {
                    // Split the header into key=value pairs.
                    string[] keyValuePairs = header.Split(';');
                    string[] keyAndValue = keyValuePairs[0].Split('=');
                    if (keyAndValue.Length == 2) {
                        cookies[keyAndValue[0]] = keyAndValue[1];
                        Log("Cookie: " + keyAndValue[0] + "=" + keyAndValue[1]);
                    }
                    else {
                        Log("Invalid cookie header: " + header);
                    }
                }

                // Combine the cookies into a single cookie string.
                _cookie = cookies.Aggregate("", (current, pair) => current + pair.Key + "=" + pair.Value + "; ");
            }
            else {
                Log("No set cookie header found.");
            }
        }

        
        // Get the user ID from the HTML response of the home page.
        {
            HttpClientHandler handler = new () {
                AllowAutoRedirect = false
            };
            
            HttpClient client = new (handler);
            client.DefaultRequestHeaders.Add("Cookie", _cookie);
            HttpResponseMessage response = await client.GetAsync(MainUrl);
            string html = await response.Content.ReadAsStringAsync();
            
            // Get userid from Compass.organisationUserId = userid;
            int start = html.IndexOf("Compass.organisationUserId = ", StringComparison.Ordinal) + "Compass.organisationUserId = ".Length;
            int end = html.IndexOf(";", start, StringComparison.Ordinal);
            _userId = html.Substring(start, end - start);
            Log("User ID: " + _userId);
        }

        _isLoggedIn = true;
        return true;
    }

    /// <summary>
    /// Gets the classes that the user has during the given time period.
    /// </summary>
    /// <param name="startDate">The earliest date to get classes for</param>
    /// <param name="endDate">The latest date to get classes for</param>
    /// <param name="limit">The maximum amount of classes to get</param>
    /// <param name="page">The page to get</param>
    /// <returns>A list of the classes</returns>
    /// <exception cref="CompassException">When you are not logged in</exception>
    public async Task<IEnumerable<CompassClass>> GetClasses(DateTime? startDate = null, DateTime? endDate = null, int limit = 25, int page = 1) {
        if (!_isLoggedIn) throw new CompassException("Not logged in");
        
        startDate ??= DateTime.Now;
        endDate ??= DateTime.Now;
        
        HttpClient client = new ();
        client.DefaultRequestHeaders.Add("Cookie", _cookie);
        Log("Cookie: " + _cookie);
        string body = "{" +
                      $"\"userId\":\"{_userId}\"," + 
                      "\"homePage\":false," +
                      "\"activityId\":null," +
                      "\"locationId\":null," +
                      "\"staffIds\":null," +
                      $"\"startDate\":\"{startDate.Value:yyyy-MM-dd}\"," +
                      $"\"endDate\":\"{endDate.Value:yyyy-MM-dd}\"," +
                      $"\"page\":{page}," +
                      "\"start\":0," +
                      $"\"limit\":{limit}" +
                      "}";
        Log("Sending request: " + body);
        StringContent content = new (body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        HttpResponseMessage response = await client.PostAsync(GetClassesUrl, content);
        string json = await response.Content.ReadAsStringAsync();
        Log("Response: " + json);
        
        if (!response.IsSuccessStatusCode) {
            Log(response.StatusCode.ToString());
            return null;
        }
        
        // Get class names
        client = new HttpClient();
        client.DefaultRequestHeaders.Add("Cookie", _cookie);
        Log("Cookie: " + _cookie);
        body = "{" +
                $"\"userId\":\"{_userId}\"," +
                "\"page\":1," +
                "\"start\":0," +
                "\"limit\":25" +
                "}";
        Log("Sending request: " + body);
        content = new StringContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        HttpResponseMessage namesResponse = await client.PostAsync(GetClassNamesUrl, content);
        string namesJson = await namesResponse.Content.ReadAsStringAsync();
        Log("Response: " + namesJson);

        Log("Parsing response");
        JsonDocument classesDoc = JsonDocument.Parse(json);
        JsonElement root = classesDoc.RootElement;
        JsonElement.ArrayEnumerator classElements = root.GetProperty("d").EnumerateArray();
        
        JsonDocument namesDoc = JsonDocument.Parse(namesJson);
        JsonElement.ArrayEnumerator names = namesDoc.RootElement.GetProperty("d").EnumerateArray();
        Dictionary<long, string> namesMap = new ();
        foreach (JsonElement name in names) {
            int id = name.GetProperty("ids")
                .EnumerateArray()
                .First()
                .GetInt32();
            string n = name.GetProperty("subjectName")
                .GetString();
            if (namesMap.ContainsKey(id)) {
                Log($"Duplicate class ID (Class {n}): {id}");
                continue;
            }
            namesMap.Add(id, n);
        }

        return classElements.Select(classItem => new CompassClass {
            Id = classItem.GetProperty("title").GetString(),
            StartTime = classItem.GetProperty("start").GetDateTime().ToLocalTime(),
            EndTime = classItem.GetProperty("finish").GetDateTime().ToLocalTime(),
            RollMarked = classItem.GetProperty("rollMarked").GetBoolean(),
            Room = classItem.GetProperty("longTitleWithoutTime").GetString()!.Split('-')[^2],
            Name = classItem.GetProperty("managerId").ValueKind != JsonValueKind.Null ? 
                classItem.GetProperty("managerId").TryGetInt32(out int manId) ? namesMap[manId] : "Unknown" : "Unknown"
        }).OrderByDescending(c => c.StartTime).Reverse();
    }

    /// <summary>
    /// Gets a list of the most recent news items.
    /// </summary>
    /// <param name="start">The amount of skip (Default: 0)</param>
    /// <param name="limit">The limit to how many are returned (Default: 25)</param>
    /// <returns>A list of Compass news items</returns>
    /// <exception cref="CompassException">When you are not logged in</exception>
    public async Task<IEnumerable<CompassNewsItem>> GetNewsItems(int start = 0, int limit = 25) {
        if (!_isLoggedIn) throw new CompassException("Not logged in");

        HttpClient client = new ();
        client.DefaultRequestHeaders.Add("Cookie", _cookie);
        Log("Cookie: " + _cookie);
        string body = "{" +
                      "\"activityId\":null," +
                      $"\"start\":{start}," +
                      $"\"limit\":{limit}" +
                      "}";
        Log("Sending request: " + body);
        StringContent content = new (body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        HttpResponseMessage response = await client.PostAsync(GetNewsItemsUrl, content);
        string json = await response.Content.ReadAsStringAsync();
        Log("Response: " + json);
        
        if (!response.IsSuccessStatusCode) {
            Log(response.StatusCode.ToString());
            return null;
        }

        Log("Parsing response");
        JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement.ArrayEnumerator newsElements = root.GetProperty("d").GetProperty("data").EnumerateArray();

        return (from newsElement in newsElements
            let attachments = newsElement.GetProperty("Attachments").EnumerateArray()
            select new CompassNewsItem {
                Attachments = attachments.Select(attachment => new CompassNewsItemAttachment {
                    Name = attachment.GetProperty("Name").GetString(), 
                    Url = MainUrl + attachment.GetProperty("UiLink").GetString(), 
                    IsImage = attachment.GetProperty("IsImage").GetBoolean(), 
                    OriginalFileName = attachment.GetProperty("OriginalFileName").GetString()
                }).ToArray(),
                Title = newsElement.GetProperty("Title").GetString(),
                Content = newsElement.GetProperty("Content1").GetString(),
                Content2 = newsElement.GetProperty("Content2").GetString(),
                CreatedByAdmin = newsElement.GetProperty("CreatedByAdmin").GetBoolean(),
                EmailSendDate = newsElement.GetProperty("EmailSentDate").GetDateTime().ToLocalTime(),
                Locked = newsElement.GetProperty("Locked").GetBoolean(),
                PostDateTime = newsElement.GetProperty("PostDateTime").GetDateTime().ToLocalTime(),
                Priority = newsElement.GetProperty("Priority").GetBoolean(),
                SenderUsername = newsElement.GetProperty("UserName").GetString(),
                SenderProfilePictureUrl = MainUrl + newsElement.GetProperty("UserImageUrl").GetString()
            }).OrderByDescending(n => n.PostDateTime);
    }

    /// <summary>
    /// Gets information about the logged in user.
    /// </summary>
    /// <returns>An object containing information on the logged in user.</returns>
    /// <exception cref="CompassException">If you are not logged in</exception>
    public async Task<CompassUser> GetUserProfile() {
        if (!_isLoggedIn) throw new CompassException("Not logged in");

        HttpClient client = new ();
        client.DefaultRequestHeaders.Add("Cookie", _cookie);
        Log("Cookie: " + _cookie);
        string body = "{" +
                      $"\"id\":{_userId}," +
                      $"\"targetUserId\":{_userId}" +
                      "}";
        Log("Sending request: " + body);
        StringContent content = new (body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        HttpResponseMessage response = await client.PostAsync(GetUserInfoUrl, content);
        string json = await response.Content.ReadAsStringAsync();
        Log("Response: " + json);
        
        if (!response.IsSuccessStatusCode) {
            Log(response.StatusCode.ToString());
            return null;
        }

        Log("Parsing response");
        JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement userElement = root.GetProperty("d");

        JsonElement.ArrayEnumerator presenceElements = userElement.GetProperty("userTimeLinePeriods").EnumerateArray();
        CompassUserPresenceEntry[] presence = presenceElements.Select(presenceElement => new CompassUserPresenceEntry {
                TimePeriodStart = presenceElement.GetProperty("start").GetDateTime().ToLocalTime(),
                TimePeriodEnd = presenceElement.GetProperty("finish").GetDateTime().ToLocalTime(),
                AttendanceOverride = presenceElement.GetProperty("attendanceOverride").GetBoolean(),
                Status = presenceElement.GetProperty("status").GetInt32(),
                StatusName = presenceElement.GetProperty("statusName").GetString(),
                TeachingTime = presenceElement.GetProperty("teachingTime").GetBoolean(),
                TimePeriodName = presenceElement.GetProperty("name").GetString()
            })
            .ToArray();

        CompassUser userObj = new () {
            StudentCode = userElement.GetProperty("userDisplayCode").GetString(),
            FullName = userElement.GetProperty("userFullName").GetString(),
            Email = userElement.GetProperty("userEmail").GetString(),
            House = userElement.GetProperty("userHouse").GetString(),
            HomeGroup = userElement.GetProperty("userFormGroup").GetString(),
            PhotoUrl = MainUrl + userElement.GetProperty("userPhotoPath").GetString(),
            SquarePhotoUrl = MainUrl + userElement.GetProperty("userSquarePhotoPath").GetString(),
            PreferredFirstName = userElement.GetProperty("userPreferredName").GetString(),
            PreferredLastName = userElement.GetProperty("userPreferredLastName").GetString(),
            SchoolWebsite = userElement.GetProperty("userSchoolId").GetString(),
            SchoolCompassUrl = userElement.GetProperty("userSchoolURL").GetString(),
            YearLevel = userElement.GetProperty("userYearLevel").GetString(),
            YearLevelId = userElement.GetProperty("userYearLevelId").GetInt32(),
            Presence = presence
        };
        return userObj;
    }

}