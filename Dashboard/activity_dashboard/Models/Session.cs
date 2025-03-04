using System;

namespace activity_dashboard.Models
{
    // public class Session
    // {
    //     public int Id { get; set; }               // Assuming 'id' is an integer primary key
    //     public string? Title { get; set; }         // Title of the session
    //     public string? Topic { get; set; }         // Topic of the session
    //     public DateTime TimeStart { get; set; }   // Start time of the session
    //     public DateTime TimeEnd { get; set; }     // End time of the session
    //     public int Duration { get; set; }         // Duration of the session in minutes (adjust type if needed)
    // }
    public class Session
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public DateTime DateUsed { get; set; }
    public int DurationSec { get; set; }

    // Optionally, you can add a calculated property for Duration in minutes:
    public int DurationMinutes => DurationSec;
}

}
