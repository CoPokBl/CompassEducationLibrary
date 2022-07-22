using System;

namespace CompassApi; 

public class CompassLearningTask {
    public long Id { get; set; }
    public long ClassId { get; set; }
    public string ClassShortName { get; set; }
    public string ClassName { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreationDateTime { get; set; }
    public CompassLearningTaskAttachment[] Attachments { get; set; }
}

public class CompassLearningTaskAttachment {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string FileName { get; set; }
}