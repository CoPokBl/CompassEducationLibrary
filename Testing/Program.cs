using CompassApi;

Task Log(string msg) {
    Console.WriteLine(msg);
    return Task.CompletedTask;
}

CompassClient client = new ("rowvillesc-vic", Log);
client.Authenticate("har0155", "ZaneH13!!").Wait();
IEnumerable<CompassClass> classes = client.GetClasses(new DateTime(2022, 11, 16), new DateTime(2022, 11, 17)).Result;
if (classes == null) {
    Console.WriteLine("No classes found");
    return;
}
foreach (CompassClass compassClass in classes) {
    Console.WriteLine($"{compassClass.Room} {compassClass.Name} {compassClass.Id} {compassClass.Teacher} " +
                      $"(Roll Marked: {compassClass.RollMarked}): " +
                      $"{compassClass.StartTime.ToShortTimeString()} - {compassClass.EndTime.ToShortTimeString()}");
}

return;

IEnumerable<CompassNewsItem> news = client.GetNewsItems(limit: 5).Result;
if (news == null) {
    Console.WriteLine("No news items found");
    return;
}
foreach (CompassNewsItem compassNewsItem in news) {
    Console.WriteLine($"\n{compassNewsItem.Title} - {compassNewsItem.SenderUsername}\n" +
                      $"{compassNewsItem.PostDateTime.ToLongDateString()} (Attachments: {compassNewsItem.Attachments.Length}) (Priority: {compassNewsItem.Priority})\n");
}

CompassUser user = client.GetUserProfile().Result;
if (user == null) {
    Console.WriteLine("No user profile found");
    return;
}
Console.WriteLine($"Name: {user.FullName}");
Console.WriteLine($"Email: {user.Email}");
Console.WriteLine($"ID: {user.Id}");
Console.WriteLine($"Year: {user.YearLevel}");
Console.WriteLine($"Home Group: {user.HomeGroup}");
Console.WriteLine($"School: {user.SchoolWebsite}");

foreach (CompassUserPresenceEntry presenceEntry in user.Presence) {
    Console.WriteLine(
        $"{presenceEntry.TimePeriodName} " +
        $"({presenceEntry.TimePeriodStart.ToShortTimeString()} - {presenceEntry.TimePeriodEnd.ToShortTimeString()}):" +
        $" {(presenceEntry.Present ? "Present" : "Absent")} (Notes: {presenceEntry.StatusName})");
}

IEnumerable<CompassLearningTask> tasks = client.GetLearningTasks().Result;
if (tasks == null) {
    Console.WriteLine("No learning tasks found");
    return;
}
foreach (CompassLearningTask task in tasks) {
    Console.WriteLine($"{task.Name} ({task.ClassName}):\n" +
                      //$"{task.Description}\n" +
                      $"{task.DueDate.ToLongDateString()}\n");
}