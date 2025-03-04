using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using activity_dashboard.Filters;

[TypeFilter(typeof(AuthenticationFilter))]  // Add this line
public class TestConnectionController : Controller
{
    private readonly ApplicationDbContext _context;

    public TestConnectionController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index(DateTime? startDate, DateTime? endDate)
    { 
        if (!startDate.HasValue || !endDate.HasValue)
        {
            startDate = DateTime.Today;
            endDate = DateTime.Today;
        }

        // Start with the base query
        var sessions = _context.Sessions.AsQueryable();

        // Apply date range filter (assuming DateUsed is a DATE type)
        sessions = sessions.Where(s => s.DateUsed >= startDate.Value && s.DateUsed <= endDate.Value);

        var chartData = sessions
            .GroupBy(s => s.Topic)
            .Select(g => new
            {
                Topic = g.Key,
                TotalDuration = g.Sum(s => s.DurationSec)
            })
            .ToList();

        ViewBag.ChartData = System.Text.Json.JsonSerializer.Serialize(chartData);
        return View();
    }

    
    [HttpGet]
    public IActionResult GetSessions(string topic = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        
        // Start with the base query
        var sessions = _context.Sessions.AsQueryable();
        if (startDate.HasValue && endDate.HasValue)
        {
            sessions = sessions.Where(s => s.DateUsed >= startDate.Value && s.DateUsed <= endDate.Value);
        }

        // If a topic is provided
        if (!string.IsNullOrEmpty(topic))
        {
            // Filter by topic, sort from recent to oldest, and return only the selected properties.
            var filteredSessions = sessions
                .Where(s => s.Topic == topic)
                .OrderByDescending(s => s.DateUsed)
                .Select(s => new
                {
                    s.Title,
                    s.Topic,
                    s.DateUsed,
                    DurationSec = s.DurationSec
                })
                .ToList();
            return Json(filteredSessions);
        }

        else
        {
            // No topic provided, return grouped data by topic
            var groupedSessions = sessions
                .GroupBy(s => s.Topic)
                .Select(g => new
                {
                    Topic = g.Key,
                    TotalDuration = g.Sum(s => s.DurationSec) // Total duration in seconds
                })
                .ToList();
            return Json(groupedSessions);
        }
    }
}
