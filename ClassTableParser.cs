using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;

namespace RegisterMe
{
    internal class ParsedClass
    {
        public string Title { get; set; }
        public string Instructor { get; set; }
        public List<string> MeetingTimes { get; set; } = new List<string>();
        public string Status { get; set; }
        public string Fund { get; set; }
        public IWebElement AddButton { get; set; }
    }

    internal static class ClassTableParser
    {
        public static List<ParsedClass> ParseTable(IWebElement table)
        {
            var classes = new List<ParsedClass>();
            var rows = table.FindElements(By.XPath(".//tbody/tr"));

            foreach (var row in rows)
            {
                if (row.GetCssValue("display") == "none") continue;
                var cells = row.FindElements(By.XPath("./td|./th"));
                if (cells.Count == 0) continue;

                // Find the course title and instructor
                var titleCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Title");
                var instructorCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Instructor");
                var meetingCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Meeting Times");
                var statusCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Status");
                var fundCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Attributes");
                var addCell = cells.FirstOrDefault(td => td.GetAttribute("data-property") == "add");

                if (titleCell != null && instructorCell != null)
                {
                    var parsed = new ParsedClass
                    {
                        Title = titleCell.Text.Trim(),
                        Instructor = instructorCell.Text.Trim()
                    };

                    // Meeting times
                    if (meetingCell != null)
                    {
                        var meetings = meetingCell.FindElements(By.CssSelector("div.meeting"));
                        foreach (var meeting in meetings)
                        {
                            var schedule = meeting.FindElement(By.CssSelector("div.meeting-schedule"));
                            var days = string.Join("", schedule.FindElements(By.CssSelector("ul li.ui-state-highlight div")).Select(d => d.Text));
                            var time = string.Join(" ", schedule.FindElements(By.XPath(".//span[not(@class)]")).Select(s => s.Text));
                            parsed.MeetingTimes.Add($"{days} {time}".Trim());
                        }
                    }

                    // Status
                    if (statusCell != null)
                    {
                        var div = statusCell.FindElements(By.CssSelector("div.status-full")).FirstOrDefault();
                        parsed.Status = div != null ? div.Text.Trim() : statusCell.Text.Trim();
                    }

                    // Fund
                    if (fundCell != null)
                    {
                        var span = fundCell.FindElements(By.TagName("span")).FirstOrDefault();
                        parsed.Fund = span != null ? span.Text.Trim() : fundCell.Text.Trim();
                    }

                    // Add button
                    if (addCell != null)
                    {
                        parsed.AddButton = addCell.FindElements(By.TagName("button")).FirstOrDefault();
                    }

                    classes.Add(parsed);
                }
            }

            return classes;
        }

        public static void PrintClasses(List<ParsedClass> classes)
        {
            foreach (var c in classes)
            {
                Console.WriteLine($"Course: {c.Title}");
                Console.WriteLine($"Instructor: {c.Instructor}");
                Console.WriteLine("Meeting Times:");
                foreach (var mt in c.MeetingTimes)
                    Console.WriteLine($"  {mt}");
                if (!string.IsNullOrEmpty(c.Status))
                    Console.WriteLine($"Status: {c.Status}");
                if (!string.IsNullOrEmpty(c.Fund))
                    Console.WriteLine($"Fund: {c.Fund}");
                Console.WriteLine(new string('-', 40));
            }
        }
    }
}