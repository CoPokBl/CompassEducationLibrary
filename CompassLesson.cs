using System;

namespace CompassApi; 

public class CompassLesson {
    public string LessonId { get; set; }
    public string LessonPlan { get; set; }
    public string Name { get; set; }
    public string Id { get; set; }
    public int ActivityId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Room { get; set; }
    public string Teacher { get; set; }
    public string TeacherImageLink { get; set; }
}