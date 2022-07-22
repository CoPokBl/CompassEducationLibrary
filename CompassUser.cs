using System;

namespace CompassApi; 

public class CompassUser {
    public string StudentCode { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string House { get; set; }
    public string HomeGroup { get; set; }
    public long Id { get; set; }
    public string PhotoUrl { get; set; }
    public string SquarePhotoUrl { get; set; }
    public string PreferredFirstName { get; set; }
    public string PreferredLastName { get; set; }
    public string SchoolWebsite { get; set; }
    public string SchoolCompassUrl { get; set; }
    public string YearLevel { get; set; }
    public int YearLevelId { get; set; }
    public CompassUserPresenceEntry[] Presence { get; set; }
}

public class CompassUserPresenceEntry {
    public bool AttendanceOverride { get; set; }
    public string TimePeriodName { get; set; }
    public DateTime TimePeriodStart { get; set; }
    public DateTime TimePeriodEnd { get; set; }
    public bool TeachingTime { get; set; }
    public int Status { get; set; }
    public bool Present { get; set; }
    public string StatusName { get; set; }
}
