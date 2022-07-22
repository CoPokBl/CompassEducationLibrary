# Compass Education Library

## Usage

Instantiate CompassClient:

```csharp
CompassClient client = new CompassClient("<school prefix>");
```

Make sure that `<school prefix>` is the text at the start of the URL for your school.  
For example, if you access Compass at https://myveryawesomeschool.compass.education/ 
then the school prefix is `myveryawesomeschool`.  

If you want debugging for some reason then you can do this:

```csharp
CompassClient client = new CompassClient("<school prefix>", LoggingFunction);
```

Where `LoggingFunction` is a function that takes a string and returns a Task.
For example you might have this:

```csharp
private static Task Log(string msg) {
    Console.WriteLine(msg);
    return Task.CompletedTask;
}
```

From there the next step is to call `client.Authenticate(username, password)` 
to log in to Compass. Authentication returns true if successful or false otherwise. 

From there you can call any client methods to get relevant data.

## Examples
Here are examples of all the methods that are available. Please 
not that you must call `client.Authenticate(username, password)` before 
hand and these examples may not cover every argument.

Print all the classes that the user has that day:
```csharp
IEnumerable<CompassClass> classes = client.GetClasses().Result;
foreach (CompassClass compassClass in classes) {
    Console.WriteLine($"{compassClass.Room} {compassClass.Name} (Roll Marked: {compassClass.RollMarked}): {compassClass.StartTime.ToShortTimeString()} - {compassClass.EndTime.ToShortTimeString()}");
}
```

Print the last 5 news items:
```csharp
IEnumerable<CompassNewsItem> news = client.GetNewsItems(limit: 5).Result;
foreach (CompassNewsItem compassNewsItem in news) {
    Console.WriteLine($"\n{compassNewsItem.Title} - {compassNewsItem.SenderUsername}\n" +
                      $"{compassNewsItem.PostDateTime.ToLongDateString()} (Attachments: {compassNewsItem.Attachments.Length}) (Priority: {compassNewsItem.Priority})\n");
}
```

Print user information:
```csharp
CompassUser user = client.GetUserProfile().Result;

Console.WriteLine($"Name: {user.FullName}");
Console.WriteLine($"Email: {user.Email}");
Console.WriteLine($"ID: {user.Id}");
Console.WriteLine($"Year: {user.YearLevel}");
Console.WriteLine($"Home Group: {user.HomeGroup}");
Console.WriteLine($"School: {user.SchoolWebsite}");
```

Print user attendance information:
```csharp
CompassUser user = client.GetUserProfile().Result;

foreach (CompassUserPresenceEntry presenceEntry in user.Presence) {
    Console.WriteLine(
        $"{presenceEntry.TimePeriodName} " +
        $"({presenceEntry.TimePeriodStart.ToShortTimeString()} - {presenceEntry.TimePeriodEnd.ToShortTimeString()}):" +
        $" {presenceEntry.Status} {presenceEntry.StatusName}");
}
```