using System;

namespace CompassApi; 

public class CompassClass {
    public string Name { get; set; }
    public string Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool RollMarked { get; set; }
    public string Room { get; set; }
}