using System;

namespace CompassApi; 

public class CompassClass {
    public string Name { get; set; }
    public string Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool RollMarked { get; set; }
    public string Room { get; set; }
    public string HtmlRoom { get; set; }
    public CompassClassType ActivityType { get; set; }
    public string Teacher { get; set; }
    public string TeacherImageLink { get; set; }
    public string? LessonId { get; set; }

    public static CompassClassType TypeIntToEnum(int type) {
        return type switch {
            1 => CompassClassType.Normal,
            2 => CompassClassType.Event,
            5 => CompassClassType.Exempt,
            7 => CompassClassType.WeekNumber,
            10 => CompassClassType.DueTask,
            _ => CompassClassType.Unknown
        };
    }
}

public enum CompassClassType {
    Normal,
    Unknown,
    Exempt,
    WeekNumber,
    DueTask,
    Event
}