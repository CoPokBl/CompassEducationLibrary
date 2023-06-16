using System;
using System.Collections.Generic;
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
    
    private string LoginUrl => "https://{school}.compass.education/login.aspx?sessionstate=disabled"
        .Replace("{school}", _schoolPrefix);
    private string GetClassesUrl {
        get {
            const string url = "https://{school}.compass.education/Services/Calendar.svc/GetCalendarEventsByUser" +
                               "?sessionstate=readonly&" +
                               "includeEvents=false&" +
                               "includeAllPd=false&" +
                               "includeExams=false&" +
                               "includeVolunteeringEvent=false";
            return url.Replace("{school}", _schoolPrefix);
        }
    }
    private string GetNewsItemsUrl => "https://{school}.compass.education/Services/NewsFeed.svc/GetMyNewsFeedPaged?sessionstate=readonly"
        .Replace("{school}", _schoolPrefix);
    private string GetUserInfoUrl => "https://{school}.compass.education/Services/User.svc/GetUserDetailsBlobByUserId?sessionstate=readonly"
        .Replace("{school}", _schoolPrefix);
    private string GetClassNamesUrl => "https://{school}.compass.education/Services/Communications.svc/GetClassTeacherDetailsByStudent?sessionstate=readonly"
        .Replace("{school}", _schoolPrefix);
    private string MainUrl => "https://{school}.compass.education"
        .Replace("{school}", _schoolPrefix);
    private string GetTasksUrl => "https://{school}.compass.education/Services/LearningTasks.svc/GetAllLearningTasksByUserId?sessionstate=readonly"
        .Replace("{school}", _schoolPrefix);
    private string GetTasksByClassUrl => "https://rowvillesc-vic.compass.education/Services/LearningTasks.svc/GetAllLearningTasksByActivityId?sessionstate=readonly"
        .Replace("{school}", _schoolPrefix);
    private string GetLessonUrl => "https://{school}.compass.education/Services/Activity.svc/GetLessonsByInstanceId?sessionstate=readonly"
        .Replace("{school}", _schoolPrefix);

    private string GetLessonPlanUrl =>
        "https://{school}.compass.education/Services/FileAssets.svc/DownloadFile?sessionstate=readonly&id="
            .Replace("{school}", _schoolPrefix);
    
    

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
            HttpResponseMessage response;
            try {
                response = await client.PostAsync(LoginUrl, content);
            }
            catch (Exception e) {
                Log("Error sending auth request: " + e);
                return false;
            }
            Log("Authentication response: " + response.StatusCode);
            
            // Print all the headers
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers) {
                Log(header.Key + ": " + string.Join(", ", header.Value));
            }
            
            // await File.WriteAllTextAsync("response.html", await response.Content.ReadAsStringAsync());
            
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
            try {
                _userId = html.Substring(start, end - start);
            }
            catch (Exception) {
                Log("Failed to get user ID from HTML");
                Log(html);
                return false;
            }
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
    public async Task<IEnumerable<CompassClass>> GetClassesOld(DateTime? startDate = null, DateTime? endDate = null, int limit = 25, int page = 1) {
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
        Log(GetClassesUrl);
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
        Log(GetClassNamesUrl);
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
                classItem.GetProperty("managerId").TryGetInt32(out int manId) ? namesMap.ContainsKey(manId) ? namesMap[manId] : "Unknown" : "Unknown" : "Unknown"
        }).OrderByDescending(c => c.StartTime).Reverse();
    }

    /// <summary>
    /// Gets the classes that the user has during the given time period.
    /// </summary>
    /// <param name="getMoreInfo">
    /// Whether or not to get the following fields:
    /// Teacher, Teacher Image Link, Name and Room
    /// Disabling this will make the request much faster
    /// </param>
    /// <param name="startDate">The earliest date to get classes for</param>
    /// <param name="endDate">The latest date to get classes for</param>
    /// <param name="limit">The maximum amount of classes to get</param>
    /// <param name="page">The page to get</param>
    /// <returns>A list of the classes</returns>
    /// <exception cref="CompassException">When you are not logged in</exception>
    public async Task<IEnumerable<CompassClass>> GetClasses(bool getMoreInfo = true, DateTime? startDate = null, DateTime? endDate = null, int limit = 25, int page = 1) {
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
        Log(GetClassesUrl);
        HttpResponseMessage response = await client.PostAsync(GetClassesUrl, content);
        string json = await response.Content.ReadAsStringAsync();
        Log("Response: " + json);
        
        if (!response.IsSuccessStatusCode) {
            Log(response.StatusCode.ToString());
            return null!;
        }

        Log("Parsing response");
        JsonDocument classesDoc = JsonDocument.Parse(json);
        JsonElement root = classesDoc.RootElement;
        JsonElement.ArrayEnumerator classElements = root.GetProperty("d").EnumerateArray();
        
        List<CompassClass> classes = new();
        foreach (JsonElement classElement in classElements) {
            CompassClass compassClass = new() {
                Id = classElement.GetProperty("title").GetString(),
                StartTime = classElement.GetProperty("start").GetDateTime().ToUniversalTime(),
                EndTime = classElement.GetProperty("finish").GetDateTime().ToUniversalTime(),
                RollMarked = classElement.GetProperty("rollMarked").GetBoolean(),
                ActivityType = CompassClass.TypeIntToEnum(classElement.GetProperty("activityType").GetInt32()),
                LessonId = classElement.GetProperty("instanceId").GetString(),
                HtmlRoom = "Unknown",
                Name = "Unknown",
                Room = "Unknown",
                Teacher = "Unknown",
                TeacherImageLink = "Unknown"
            };
            try {
                compassClass.HtmlRoom = classElement.GetProperty("longTitleWithoutTime").GetString()!.Split('-')[^2];
            }
            catch (Exception) {
                compassClass.HtmlRoom = "Unknown";
            }

            if (!getMoreInfo) {
                classes.Add(compassClass);
                continue;
            }
            // Get more info
            Log("Getting more info for " + compassClass.Id);
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("Cookie", _cookie);
            body = "{" +
                   $"\"instanceId\":\"{compassClass.LessonId}\"," +
                   "}";
            content = new StringContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            HttpResponseMessage infoResponse = await client.PostAsync(GetLessonUrl, content);
            string infoJson = await infoResponse.Content.ReadAsStringAsync();
            JsonDocument infoDoc = JsonDocument.Parse(infoJson);
            JsonElement infoRoot = infoDoc.RootElement;
            bool go = true;
            try {
                while (go) {
                    go = false;
                    JsonElement data = infoRoot.GetProperty("d");
                    if (data.ValueKind == JsonValueKind.Null) {
                        break;
                    }

                    JsonElement instances = data.GetProperty("Instances");
                    if (data.ValueKind == JsonValueKind.Null) {
                        break;
                    }

                    JsonElement info = instances.EnumerateArray().First();

                    compassClass.Teacher = info.GetProperty("ManagerTextReadable").GetString();
                    compassClass.TeacherImageLink = MainUrl + info.GetProperty("ManagerPhotoPath").GetString();
                    compassClass.Name = info.GetProperty("SubjectName").GetString();

                    JsonElement locDetails = info.GetProperty("LocationDetails");
                    if (locDetails.ValueKind == JsonValueKind.Null) {
                        break;
                    }

                    compassClass.Room = locDetails.GetProperty("longName").GetString();
                }
            }
            catch (Exception) {
                // Ignore any error because it means that that info is not available
            }
            
            
            classes.Add(compassClass);
        }
        return classes.OrderByDescending(c => c.StartTime);
    }

    public async Task<CompassLesson?> GetLesson(string instanceId) {
        HttpClient client = new();
        client.DefaultRequestHeaders.Add("Cookie", _cookie);
        string body = "{" +
                      $"\"instanceId\":\"{instanceId}\"," +
                      "}";
        StringContent content = new(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        HttpResponseMessage response = await client.PostAsync(GetLessonUrl, content);
        string json = await response.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("d", out JsonElement data)) {
            return null;
        }
        if (data.ValueKind == JsonValueKind.Null) {
            return null;
        }
        JsonElement instances = data.GetProperty("Instances");
        JsonElement info = instances.EnumerateArray().First(i => i.GetProperty("id").GetString() == instanceId);
        CompassLesson lesson = new() {
            Id = info.GetProperty("ActivityDisplayName").GetString()!,
            Name = info.GetProperty("SubjectName").GetString()!,
            Teacher = info.GetProperty("ManagerTextReadable").GetString()!,
            TeacherImageLink = MainUrl + info.GetProperty("ManagerPhotoPath").GetString(),
            StartTime = info.GetProperty("st").GetDateTime().ToLocalTime(),
            EndTime = info.GetProperty("fn").GetDateTime().ToLocalTime(),
            LessonId = instanceId,
            ActivityId = data.GetProperty("ActivityId").GetInt32()!
        };
        JsonElement[] locationsArray = info.GetProperty("locations").EnumerateArray().ToArray();
        if (locationsArray.Any()) {
            lesson.Room = locationsArray.First().GetProperty("LocationDetails")
                .GetProperty("longName").GetString()!;
        }
        else {
            lesson.Room = "Unknown";
        }

        // Get lesson plan
        client = new HttpClient();
        client.DefaultRequestHeaders.Add("Cookie", _cookie);
        string lessonPlanId = info.GetProperty("lp").GetProperty("fileAssetId").GetString()!;
        Log("Getting lesson plan for " + lessonPlanId);
        HttpResponseMessage planResponse = await client.GetAsync(GetLessonPlanUrl + lessonPlanId);
        string planText = await planResponse.Content.ReadAsStringAsync();
        if (planText.StartsWith("{\"h\":")) {  // It doesn't exist
            planText = "";
        }
        
        // Fix broken relative links
        planText = planText.Replace("src=\"/", "src=\"" + MainUrl + "/");
        planText = planText.Replace("href=\"/", "href=\"" + MainUrl + "/");
        
        lesson.LessonPlan = planText;
        
        return lesson;
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
                Status = presenceElement.GetProperty("status").ValueKind != JsonValueKind.Null ? presenceElement.GetProperty("status").GetInt32() : -1,
                StatusName = presenceElement.GetProperty("statusName").GetString(),
                TeachingTime = presenceElement.GetProperty("teachingTime").GetBoolean(),
                TimePeriodName = presenceElement.GetProperty("name").GetString(),
                Present = (presenceElement.GetProperty("status").ValueKind != JsonValueKind.Null ? presenceElement.GetProperty("status").GetInt32() : -1) == 1
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
    
    public async Task<IEnumerable<CompassLearningTask>> GetLearningTasks(int start = 0, int limit = 25, int page = 1, bool showHiddenTasks = false) {
        if (!_isLoggedIn) throw new CompassException("Not logged in");

        HttpClient client = new ();
        client.DefaultRequestHeaders.Add("Cookie", _cookie);
        Log("Cookie: " + _cookie);
        string body = "{" +
                      "\"forceTaskId\":0," +
                      $"\"userId\":{_userId}," +
                      "\"sort\":\"[{\\\"property\\\":\\\"groupName\\\",\\\"direction\\\":\\\"ASC\\\"},{\\\"property\\\":\\\"dueDateTimestamp\\\",\\\"direction\\\":\\\"DESC\\\"}]\"," +
                      $"\"start\":{start}," +
                      $"\"limit\":{limit}," +
                      $"\"page\":{page}," +
                      $"\"showHiddenTasks\":{showHiddenTasks.ToString().ToLower()}" +
                      "}";
        Log("Sending request: " + body);
        StringContent content = new (body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        HttpResponseMessage response = await client.PostAsync(GetTasksUrl, content);
        string json = await response.Content.ReadAsStringAsync();
        Log("Response: " + json);
        
        if (!response.IsSuccessStatusCode) {
            Log(response.StatusCode.ToString());
            return null;
        }

        Log("Parsing response");
        return ParseLearningTasksFromJson(json);
    }
    
    public async Task<IEnumerable<CompassLearningTask>> GetLearningTasksByClass(int activityId, int start = 0, int limit = 25, int page = 1) {
        if (!_isLoggedIn) throw new CompassException("Not logged in");

        HttpClient client = new ();
        client.DefaultRequestHeaders.Add("Cookie", _cookie);
        Log("Cookie: " + _cookie);
        string body = "{" +
                      $"\"activityId\":\"{activityId}\"," +
                      "\"sort\":\"[{\\\"property\\\":\\\"groupName\\\",\\\"direction\\\":\\\"ASC\\\"},{\\\"property\\\":\\\"dueDateTimestamp\\\",\\\"direction\\\":\\\"DESC\\\"}]\"," +
                      $"\"start\":{start}," +
                      $"\"limit\":{limit}," +
                      $"\"page\":{page}" +
                      "}";
        Log("Sending request: " + body);
        StringContent content = new (body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        HttpResponseMessage response = await client.PostAsync(GetTasksByClassUrl, content);
        string json = await response.Content.ReadAsStringAsync();
        Log("Response: " + json);
        
        if (!response.IsSuccessStatusCode) {
            Log(response.StatusCode.ToString());
            return null!;
        }

        Log("Parsing response");
        return ParseLearningTasksFromJson(json);
    }

    private IEnumerable<CompassLearningTask> ParseLearningTasksFromJson(string json) {
        JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement.ArrayEnumerator tasksElement = root.GetProperty("d").GetProperty("data").EnumerateArray();

        List<CompassLearningTask> list = new();
        foreach (JsonElement taskElement in tasksElement) {
            
            // Get attachments
            CompassLearningTaskAttachment[] attachments;
            if (taskElement.GetProperty("attachments").ValueKind == JsonValueKind.Null) {
                attachments = Array.Empty<CompassLearningTaskAttachment>();
            }
            else {
                JsonElement.ArrayEnumerator attachmentsElement = taskElement.GetProperty("attachments").EnumerateArray();
                attachments = attachmentsElement.Select(attachmentElement => new CompassLearningTaskAttachment {
                        Id = attachmentElement.GetProperty("id").GetString()!, 
                        Name = attachmentElement.GetProperty("name").GetString()!, 
                        Url = $"{MainUrl}/Services/FileAssets.svc/DownloadFile?id={attachmentElement.GetProperty("id").GetString()}&originalFileName={Uri.EscapeDataString(attachmentElement.GetProperty("fileName").GetString()!)}", 
                        FileName = attachmentElement.GetProperty("fileName").GetString()!,
                    })
                    .ToArray();
            }
            
            // Get submissions
            List<CompassLearningTaskSubmission> submissions = new();
            JsonElement.ArrayEnumerator studentsElement = taskElement.GetProperty("students").EnumerateArray();
            JsonElement studentElement = studentsElement.First();
            JsonElement submissionsElement = studentElement.GetProperty("submissions");
            if (submissionsElement.ValueKind == JsonValueKind.Array) {
                JsonElement.ArrayEnumerator submissionsElementArray = submissionsElement.EnumerateArray();
                foreach (JsonElement s in submissionsElementArray) {
                    CompassLearningTaskSubmission sub = new() {
                        Type = CompassLearningTaskSubmission.CompassLearningTaskSubmissionTypeFromNumber(
                            s.GetProperty("submissionFileType").GetInt32()),
                        FileName = s.GetProperty("fileName").GetString()!,
                        SubmissionTimestamp = s.GetProperty("timestamp").GetDateTime(),
                        Id = s.GetProperty("fileId").GetString()!
                    };
                    sub.DownloadUrl = $"{MainUrl}/Services/FileAssets.svc/DownloadFile?id={s.GetProperty("fileId").GetString()}" +
                                      $"&originalFileName={Uri.EscapeDataString(sub.FileName)}";
                    submissions.Add(sub);
                }
            }

            list.Add(new CompassLearningTask {
                ClassShortName = taskElement.GetProperty("activityName").GetString(),
                ClassId = taskElement.GetProperty("activityId").GetInt32(),
                ClassName = taskElement.GetProperty("subjectName").GetString(),
                CreationDateTime = taskElement.GetProperty("createdTimestamp").GetDateTime().ToLocalTime(),
                DueDate = taskElement.GetProperty("dueDateTimestamp").ValueKind != JsonValueKind.Null ? taskElement.GetProperty("dueDateTimestamp").GetDateTime().ToLocalTime() : DateTime.MaxValue,
                Name = taskElement.GetProperty("name").GetString(),
                Description = taskElement.GetProperty("description").GetString(),
                Attachments = attachments,
                Submissions = submissions.ToArray(),
                SubmissionStatus = CompassLearningTask.CompassLearningTaskSubmissionStatusFromNumber(studentElement.GetProperty("submissionStatus").GetInt32())
            });
        }

        return list;
    }

}