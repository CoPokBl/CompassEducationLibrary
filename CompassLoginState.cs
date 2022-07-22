namespace CompassApi; 

public class CompassLoginState {
    public string Cookie { get; set; }
    public string UserId { get; set; }
    public string SchoolPrefix { get; set; }

    internal CompassLoginState() { }
}