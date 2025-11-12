using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Internal;

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
                // Skip non-visible rows
                try
                {
                    if (row.GetCssValue("display") == "none") continue;
                }
                catch (StaleElementReferenceException)
                {
                    // Row went stale; skip this row iteration
                    continue;
                }

                IList<IWebElement> cells;
                try
                {
                    cells = row.FindElements(By.XPath("./td|./th"));
                }
                catch (StaleElementReferenceException)
                {
                    // DOM changed; skip this row
                    continue;
                }

                if (cells == null || cells.Count == 0) continue;

                IWebElement titleCell = null, instructorCell = null, meetingCell = null, statusCell = null, fundCell = null, addCell = null;
                try
                {
                    titleCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Title");
                    instructorCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Instructor");
                    meetingCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Meeting Times");
                    statusCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Status");
                    fundCell = cells.FirstOrDefault(td => td.GetAttribute("data-content") == "Attributes");
                    addCell = cells.FirstOrDefault(td => td.GetAttribute("data-property") == "add");
                }
                catch (StaleElementReferenceException)
                {
                    // Cells went stale while reading attributes; skip this row
                    continue;
                }

                if (titleCell != null && instructorCell != null)
                {
                    var parsed = new ParsedClass();

                    try { parsed.Title = (titleCell.Text ?? string.Empty).Trim(); } catch { parsed.Title = string.Empty; }
                    try { parsed.Instructor = (instructorCell.Text ?? string.Empty).Trim(); } catch { parsed.Instructor = string.Empty; }

                    // Meeting times (robust snapshot via JS with retries)
                    if (meetingCell != null)
                    {
                        parsed.MeetingTimes = TryGetMeetingTimesSnapshot(meetingCell);
                    }

                    // Status
                    if (statusCell != null)
                    {
                        try
                        {
                            var div = statusCell.FindElements(By.CssSelector("div.status-full")).FirstOrDefault();
                            parsed.Status = (div != null ? div.Text : statusCell.Text)?.Trim();
                        }
                        catch (StaleElementReferenceException)
                        {
                            parsed.Status = string.Empty;
                        }
                    }

                    // Fund
                    if (fundCell != null)
                    {
                        try
                        {
                            var span = fundCell.FindElements(By.TagName("span")).FirstOrDefault();
                            parsed.Fund = (span != null ? span.Text : fundCell.Text)?.Trim();
                        }
                        catch (StaleElementReferenceException)
                        {
                            parsed.Fund = string.Empty;
                        }
                    }

                    // Add button
                    if (addCell != null)
                    {
                        try
                        {
                            parsed.AddButton = addCell.FindElements(By.TagName("button")).FirstOrDefault();
                        }
                        catch (StaleElementReferenceException)
                        {
                            parsed.AddButton = null;
                        }
                    }

                    classes.Add(parsed);
                }
            }

            return classes;
        }

        // Snapshot meeting times using JS to avoid chained WebElement traversals (stale-prone).
        private static List<string> TryGetMeetingTimesSnapshot(IWebElement meetingCell)
        {
            // Up to 3 retries for transient staleness
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var wraps = meetingCell as IWrapsDriver;
                    var driver = wraps != null ? wraps.WrappedDriver as IJavaScriptExecutor : null;

                    if (driver != null)
                    {
                        var result = driver.ExecuteScript(@"
                            var cell = arguments[0];
                            if (!cell) return [];
                            var meetings = Array.prototype.slice.call(cell.querySelectorAll('div.meeting'));
                            return meetings.map(function(m) {
                                var sched = m.querySelector('div.meeting-schedule');
                                if (!sched) return '';
                                var days = Array.prototype.slice.call(sched.querySelectorAll('ul li.ui-state-highlight div'))
                                    .map(function(d){ return (d.textContent||'').trim(); })
                                    .join('');
                                var time = Array.prototype.slice.call(sched.querySelectorAll('span:not([class])'))
                                    .map(function(s){ return (s.textContent||'').trim(); })
                                    .join(' ');
                                var text = (days + ' ' + time).trim();
                                return text;
                            });
                        ", meetingCell);

                        var list = new List<string>();
                        var enumerable = result as System.Collections.IEnumerable;
                        if (enumerable != null)
                        {
                            foreach (var item in enumerable)
                            {
                                var t = item != null ? item.ToString().Trim() : string.Empty;
                                if (!string.IsNullOrEmpty(t)) list.Add(t);
                            }
                        }
                        // If we got something (including possibly empty list), return it
                        return list;
                    }

                    // Fallback if JS executor unavailable
                    return FallbackMeetingTimesFromText(meetingCell);
                }
                catch (StaleElementReferenceException)
                {
                    Thread.Sleep(50);
                }
                catch
                {
                    // Any other unexpected issue: try fallback
                    try { return FallbackMeetingTimesFromText(meetingCell); }
                    catch { /* ignore */ }
                }
            }

            // Final fallback
            try { return FallbackMeetingTimesFromText(meetingCell); } catch { return new List<string>(); }
        }

        private static List<string> FallbackMeetingTimesFromText(IWebElement meetingCell)
        {
            try
            {
                var txt = (meetingCell.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(txt)) return new List<string>();
                // Split lines; keep non-empty lines as separate occurrences
                var lines = txt
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                return lines;
            }
            catch (StaleElementReferenceException)
            {
                return new List<string>();
            }
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