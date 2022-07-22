using System;

namespace CompassApi; 

public class CompassNewsItem {
    public string Title { get; set; }
    public string Content { get; set; }
    public string Content2 { get; set; }
    public DateTime PostDateTime { get; set; }
    public bool Priority { get; set; }
    public string SenderUsername { get; set; }
    public string SenderProfilePictureUrl { get; set; }
    public bool CreatedByAdmin { get; set; }
    public DateTime EmailSendDate { get; set; }
    public DateTime Finish { get; set; }
    public bool Locked { get; set; }
    public CompassNewsItemAttachment[] Attachments { get; set; }
}

public class CompassNewsItemAttachment {
    public bool IsImage { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string OriginalFileName { get; set; }
}